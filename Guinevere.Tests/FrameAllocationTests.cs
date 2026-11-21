using Guinevere.Tests.Controls;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>
/// Holds the steady-state allocation of an unchanged frame of text, styled nodes, a table and a tree to a limit per
/// scenario. Each scenario draws both passes of a 560×400 frame, 60 warm-up frames and then 300 measured ones; a change
/// that raises a number moves its limit on purpose and says why.
/// </summary>
public class FrameAllocationTests
{
    const int Rows = 18;
    const int Columns = 3;
    const string LayoutSheet = """
        Row { flex-direction = row; width = expand; height = 20; }
        Cell { width = 30; height = 20; }
        """;
    const string VisualSheet = LayoutSheet + """

        Cell { background-color = #303030; border-radius = 4; }
        """;

    static readonly string[] Labels =
        [.. Enumerable.Range(0, Rows * Columns).Select(static i => $"r{i / Columns}c{i % Columns}")];

    /// <summary>An empty frame allocates almost nothing.</summary>
    [Fact]
    public void AnEmptyFrameAllocatesAlmostNothing() => AssertWithin(Measure(static _ => { }), 64);

    /// <summary>A grid of plain nodes, each drawing one distinct short text, stays near the text floor.</summary>
    [Fact]
    public void APlainTextGridStaysWithinItsAllocationLimit() => AssertWithin(Measure(PlainGrid), 15_000);

    /// <summary>The same grid of styled nodes under a layout-only sheet adds little to the plain grid.</summary>
    [Fact]
    public void AStyledGridWithLayoutOnlyStylesStaysWithinItsAllocationLimit() =>
        AssertWithin(Measure(StyledGrid, LayoutSheet), 25_000);

    /// <summary>A sheet that also paints a background and radius adds the box drawing to the styled grid.</summary>
    [Fact]
    public void AStyledGridWithVisualStylesStaysWithinItsAllocationLimit() =>
        AssertWithin(Measure(StyledGrid, VisualSheet), 60_000);

    /// <summary>A static virtualized table stays within its allocation limit.</summary>
    [Fact]
    public void AStaticTableStaysWithinItsAllocationLimit() =>
        AssertWithin(TableAllocationTests.SteadyBytesPerFrame(scroll: false), 55_000);

    /// <summary>A continuously scrolling table stays within its allocation limit.</summary>
    [Fact]
    public void AScrollingTableStaysWithinItsAllocationLimit() =>
        AssertWithin(TableAllocationTests.SteadyBytesPerFrame(scroll: true), 110_000);

    /// <summary>A tree of thousands of items under collapsed roots costs per visible row, not per item.</summary>
    [Fact]
    public void ALargeCollapsedTreeStaysWithinItsAllocationLimit()
    {
        var items = new List<TreeItem>(5_000);
        for (var root = 0; root < 50; root++)
        {
            items.Add(new TreeItem($"root{root}", $"Root {root}", 0, true));
            for (var child = 0; child < 99; child++)
            {
                items.Add(new TreeItem($"root{root}/child{child}", $"Child {child}", 1));
            }
        }

        var state = new TreeViewState();

        AssertWithin(Measure(gui => gui.TreeView(state, items)), 120_000);
    }

    static void AssertWithin(long bytes, long limit) =>
        Assert.True(bytes <= limit, $"{bytes} B/frame, limit {limit}");

    static long Measure(Action<Gui> draw, string? sheet = null)
    {
        using var harness = new FrameHarness(560, 400);
        if (sheet is not null)
        {
            harness.Gui.StyleSheets.Add(StyleSheet.Parse(sheet));
        }

        return AllocationProbe.BytesPerFrame(() => harness.Frame(draw));
    }

    static void PlainGrid(Gui gui)
    {
        using (gui.Node(-1, -1).Enter())
        {
            for (var row = 0; row < Rows; row++)
            {
                using (gui.Node(-1, 20).Direction(Axis.Horizontal).Enter())
                {
                    for (var column = 0; column < Columns; column++)
                    {
                        using (gui.Node(30, 20).Enter())
                        {
                            gui.DrawText(Labels[(row * Columns) + column]);
                        }
                    }
                }
            }
        }
    }

    static void StyledGrid(Gui gui)
    {
        using (gui.Node(-1, -1).Enter())
        {
            for (var row = 0; row < Rows; row++)
            {
                using (gui.StyledNode("Row").Enter())
                {
                    for (var column = 0; column < Columns; column++)
                    {
                        using (gui.StyledNode("Cell").Enter())
                        {
                            gui.DrawText(Labels[(row * Columns) + column]);
                        }
                    }
                }
            }
        }
    }
}
