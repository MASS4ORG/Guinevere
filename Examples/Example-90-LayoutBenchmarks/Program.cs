using System.Diagnostics;
using Guinevere;

const int warmups = 100;
var sizes = args.Contains("--quick") ? new[] { 100, 1_000 } : new[] { 100, 1_000, 10_000 };
var articleScale = args.Contains("--quick") ? 100 : 1_000;

Console.WriteLine("| Scenario | Nodes | Mean ms | ns/node | Alloc B/op |");
Console.WriteLine("|---|---:|---:|---:|---:|");

if (args.Contains("--deep-only"))
{
    foreach (var depth in new[] { 64, 128, 256, 512, 1_024 }) Run($"deep-fit-{depth}", BuildDeep(depth));
    return;
}

foreach (var count in sizes)
{
    Run($"wide-fixed-{count}", BuildWide(count, wrap: false));
    Run($"wide-wrap-{count}", BuildWide(count, wrap: true));
    Run($"expand-minmax-{count}", BuildExpand(count));
}
if (!args.Contains("--quick")) Run("pangui-wide-no-wrap-simple-many", BuildWide(100_000, wrap: false));

foreach (var depth in new[] { 64, 128, 256, 512 }) Run($"deep-fit-{depth}", BuildDeep(depth));
Run(args.Contains("--quick") ? "pangui-fit-nesting-scaled" : "pangui-fit-nesting",
    BuildFitNesting(args.Contains("--quick") ? 10 : 100));
Run("text-measurements-1000", BuildText(1_000));
Run("pangui-expand-with-max", BuildExpandConstraint(articleScale, useMin: false));
Run("pangui-expand-with-min", BuildExpandConstraint(articleScale, useMin: true));
Run("pangui-flex-expand-equal", BuildEqualExpand(args.Contains("--quick") ? 1_500 : 15_000));
Run("pangui-flex-expand-weights", BuildExpandWeights(args.Contains("--quick") ? 500 : 5_000));
Run("pangui-nested-vertical-stack", BuildVerticalStack(args.Contains("--quick") ? 1_000 : 10_000));
Run("pangui-padding-and-margin", BuildPaddingAndMargin());
Run("pangui-percentage-and-ratio", BuildPercentageAndRatio(args.Contains("--quick") ? 1_000 : 10_000));
Run("pangui-perpendicular-expand-wrap", BuildPerpendicularExpandWrap(articleScale));
Unsupported("pangui-pixels-with-min-expand", "composable UnitValue constraints are not implemented");
RunCachedRead("no-change-cached-10000", BuildWide(10_000, wrap: false));
RunConstruction(10_000);

static void Run(string name, Fixture fixture)
{
    for (var i = 0; i < warmups; i++) fixture.Layout();
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    var iterations = 0;
    var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    var stopwatch = Stopwatch.StartNew();
    do
    {
        fixture.Layout();
        iterations++;
    } while (stopwatch.Elapsed < TimeSpan.FromSeconds(1) || iterations < 100);
    stopwatch.Stop();
    var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
    var meanMs = stopwatch.Elapsed.TotalMilliseconds / iterations;
    var nsPerNode = meanMs * 1_000_000d / fixture.NodeCount;
    Console.WriteLine($"| {name} | {fixture.NodeCount} | {meanMs:F4} | {nsPerNode:F2} | {allocated / iterations} |");
}

static void RunCachedRead(string name, Fixture fixture)
{
    fixture.Layout();
    var checksum = 0f;
    var stopwatch = Stopwatch.StartNew();
    const int iterations = 1_000_000;
    for (var i = 0; i < iterations; i++)
    {
        fixture.LayoutCached();
        checksum += fixture.Root.Rect.W;
    }
    stopwatch.Stop();
    GC.KeepAlive(checksum);
    var meanMs = stopwatch.Elapsed.TotalMilliseconds / iterations;
    Console.WriteLine($"| {name} | {fixture.NodeCount} | {meanMs:F6} | {meanMs * 1_000_000d / fixture.NodeCount:F4} | 0 |");
}

static void Unsupported(string name, string reason) =>
    Console.WriteLine($"| {name} (unsupported: {reason}) | 0 | — | — | — |");

static void RunConstruction(int count)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    var stopwatch = Stopwatch.StartNew();
    var fixture = BuildWide(count, wrap: false);
    stopwatch.Stop();
    var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
    GC.KeepAlive(fixture);
    Console.WriteLine($"| tree-build-{count} | {fixture.NodeCount} | {stopwatch.Elapsed.TotalMilliseconds:F4} | "
                      + $"{stopwatch.Elapsed.TotalMilliseconds * 1_000_000d / fixture.NodeCount:F2} | {allocated} |");
    Console.WriteLine($"Construction allocation: {allocated / fixture.NodeCount} bytes/node");
}

static Fixture BuildWide(int count, bool wrap)
{
    var gui = new BenchmarkGui(1_000, 1_000);
    var root = LayoutNode.CreateRoot(gui, 1_000, 1_000);
    root.Style.Direction = Axis.Horizontal;
    root.Style.Wrap = wrap;
    root.Style.Gap = 1f;
    for (var i = 0; i < count; i++)
        root.AddChild(new LayoutNode(i.ToString(), gui, root, 10f, 10f));
    return new Fixture(root, count + 1);
}

static Fixture BuildExpand(int count)
{
    var gui = new BenchmarkGui(10_000, 100);
    var root = LayoutNode.CreateRoot(gui, 10_000, 100);
    root.Style.Direction = Axis.Horizontal;
    for (var i = 0; i < count; i++)
    {
        var child = new LayoutNode(i.ToString(), gui, root, 0f, 100f);
        child.Style.MinWidth = i % 2 == 0 ? 2f : -1f;
        child.Style.MaxWidth = i % 3 == 0 ? 40f : -1f;
        root.AddChild(child);
    }
    return new Fixture(root, count + 1);
}

static Fixture BuildDeep(int depth)
{
    var gui = new BenchmarkGui(1_000, 1_000);
    var root = LayoutNode.CreateRoot(gui, 1_000, 1_000);
    var parent = root;
    for (var i = 0; i < depth; i++)
    {
        var child = new LayoutNode(i.ToString(), gui, parent);
        child.Style.PaddingLeft = 1f;
        child.Style.PaddingTop = 1f;
        parent.AddChild(child);
        parent = child;
    }
    return new Fixture(root, depth + 1);
}

static Fixture BuildFitNesting(int leafCount)
{
    var gui = new BenchmarkGui(1_000, 1_000);
    var root = LayoutNode.CreateRoot(gui, 1_000, 1_000);
    var nodes = 1;
    for (var a = 0; a < 10; a++)
    {
        var first = new LayoutNode($"a{a}", gui, root);
        first.Style.Direction = Axis.Horizontal;
        root.AddChild(first);
        nodes++;
        for (var b = 0; b < 10; b++)
        {
            var second = new LayoutNode($"b{a}-{b}", gui, first);
            second.Style.Direction = Axis.Vertical;
            first.AddChild(second);
            nodes++;
            for (var c = 0; c < 10; c++)
            {
                var third = new LayoutNode($"c{a}-{b}-{c}", gui, second);
                third.Style.Direction = Axis.Horizontal;
                second.AddChild(third);
                nodes++;
                for (var d = 0; d < leafCount; d++)
                {
                    third.AddChild(new LayoutNode($"d{a}-{b}-{c}-{d}", gui, third, 0f, 10f));
                    nodes++;
                }
            }
        }
    }
    return new Fixture(root, nodes);
}

static Fixture BuildText(int count)
{
    var gui = new BenchmarkGui(1_000, 1_000);
    var root = LayoutNode.CreateRoot(gui, 1_000, 1_000);
    root.Style.Wrap = true;
    root.Style.Direction = Axis.Horizontal;
    for (var i = 0; i < count; i++)
    {
        // Text shaping is intentionally outside layout timing; these are representative measured glyph runs.
        var width = 20f + i % 23 * 3.5f;
        root.AddChild(new LayoutNode(i.ToString(), gui, root, width, 16f));
    }
    return new Fixture(root, count + 1);
}

static Fixture BuildExpandConstraint(int count, bool useMin)
{
    var gui = new BenchmarkGui(100, 100_000);
    var root = LayoutNode.CreateRoot(gui, 100, 100_000);
    for (var i = 0; i < count; i++)
    {
        var row = new LayoutNode($"row-{i}", gui, root, 100f, 10f);
        row.Style.Direction = Axis.Horizontal;
        var first = new LayoutNode($"first-{i}", gui, row, 0f, 10f);
        var second = new LayoutNode($"second-{i}", gui, row, 0f, 10f);
        if (useMin) second.Style.MinWidth = 60f;
        else second.Style.MaxWidth = 40f;
        row.AddChild(first);
        row.AddChild(second);
        root.AddChild(row);
    }
    return new Fixture(root, count * 3 + 1);
}

static Fixture BuildEqualExpand(int count)
{
    var gui = new BenchmarkGui(10_000, 100);
    var root = LayoutNode.CreateRoot(gui, 10_000, 100);
    root.Style.Direction = Axis.Horizontal;
    for (var i = 0; i < count; i++) root.AddChild(new LayoutNode(i.ToString(), gui, root, 0f, 100f));
    return new Fixture(root, count + 1);
}

static Fixture BuildExpandWeights(int count)
{
    var gui = new BenchmarkGui(10_000, 100_000);
    var root = LayoutNode.CreateRoot(gui, 10_000, 100_000);
    for (var i = 0; i < count; i++)
    {
        var row = new LayoutNode($"row-{i}", gui, root, 100f, 10f);
        row.Style.Direction = Axis.Horizontal;
        var one = new LayoutNode($"one-{i}", gui, row, 0f, 10f);
        var two = new LayoutNode($"two-{i}", gui, row, 0f, 10f);
        two.Style.ExpandWidthPercentage = 2f;
        row.AddChild(one);
        row.AddChild(two);
        root.AddChild(row);
    }
    return new Fixture(root, count * 3 + 1);
}

static Fixture BuildVerticalStack(int count)
{
    var gui = new BenchmarkGui(200, 20_000);
    var root = LayoutNode.CreateRoot(gui, 200, 20_000);
    root.Style.PaddingTop = root.Style.PaddingRight = root.Style.PaddingBottom = root.Style.PaddingLeft = 10f;
    root.Style.Gap = 5f;
    for (var i = 0; i < count; i++) root.AddChild(new LayoutNode(i.ToString(), gui, root, 0f, 1f));
    return new Fixture(root, count + 1);
}

static Fixture BuildPaddingAndMargin()
{
    var fixture = BuildVerticalStack(100);
    foreach (var child in fixture.Root.ChildNodes)
        child.Style.MarginTop = child.Style.MarginRight = child.Style.MarginBottom = child.Style.MarginLeft = 2f;
    return fixture;
}

static Fixture BuildPercentageAndRatio(int count)
{
    var gui = new BenchmarkGui(1_000, 1_000);
    var root = LayoutNode.CreateRoot(gui, 1_000, 1_000);
    for (var i = 0; i < count; i++)
    {
        var child = new LayoutNode(i.ToString(), gui, root);
        child.Width(UnitValue.Percentage(0.5f));
        child.Height(UnitValue.Ratio(0.5f));
        root.AddChild(child);
    }
    return new Fixture(root, count + 1);
}

static Fixture BuildPerpendicularExpandWrap(int count)
{
    var gui = new BenchmarkGui(100, 100_000);
    var root = LayoutNode.CreateRoot(gui, 100, 100_000);
    root.Style.Direction = Axis.Horizontal;
    root.Style.Wrap = true;
    var widths = new[] { 10f, 20f, 30f, 40f, 50f, 60f };
    for (var i = 0; i < count; i++)
    {
        foreach (var width in widths)
        {
            root.AddChild(new LayoutNode($"fixed-{i}-{width}", gui, root, width, width));
            root.AddChild(new LayoutNode($"expand-{i}-{width}", gui, root, 1f, 0f));
        }
    }
    return new Fixture(root, count * widths.Length * 2 + 1);
}

sealed record Fixture(LayoutNode Root, int NodeCount)
{
    public void Layout()
    {
        Root.InvalidateLayout();
        Root.CalculateLayout();
    }

    public void LayoutCached() => Root.CalculateLayout();
}

sealed class BenchmarkGui(float width, float height) : Gui
{
    public override Rect ScreenRect => new(0f, 0f, width, height);
}
