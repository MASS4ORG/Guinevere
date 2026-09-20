using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Guinevere;
using SkiaSharp;

var quick = args.Contains("--quick");
var count = quick ? 1_000 : 10_000;
var iterations = quick ? 500 : 2_000;
var drawable = new NoopDrawable();
var packed = Enumerable.Range(0, count)
    .Select(i => Color.FromArgb(128 + i % 128, (byte)i, (byte)(i >> 2), (byte)(255 - i)))
    .ToArray();
var colors = new Color[count];
var legacyColors = new LegacyColor[count];
var skColors = new SKColor[count];
var checksum = 0f;
var effect = new TextEffects
{
    Outline = new TextEffects.TextOutline(Color.Black, 2f),
    DropShadow = new TextEffects.TextShadow(Color.Black, new Vector2(2f), 4f),
    InnerShadow = new TextEffects.TextShadow(Color.White, new Vector2(-1f), 2f),
    Gradient = new TextEffects.TextGradient(Color.Red, Color.Blue)
};
var legacyEffect = new LegacyTextEffects(effect);
var effectReferences = new LegacyTextEffects[count];
var valueEffects = new TextEffects[count];

Console.WriteLine($"Runtime: {Environment.Version}; Vector width: {Vector<byte>.Count * 8} bits");
Console.WriteLine($"Sizes: Color={Unsafe.SizeOf<Color>()} B, TextEffects={Unsafe.SizeOf<TextEffects>()} B, " +
                  $"legacy Color={Unsafe.SizeOf<LegacyColor>()} B, " +
                  $"LayoutStyle={Unsafe.SizeOf<LayoutStyle>()} B");
Console.WriteLine("| Scenario | Items/op | Mean us | ns/item | Alloc B/op |");
Console.WriteLine("|---|---:|---:|---:|---:|");

Measure("legacy-polymorphic-draw-build", count, iterations, () =>
{
    var commands = new List<IDrawListEntry>(count);
    for (var i = 0; i < count; i++) commands.Add(new DrawableEntry(drawable));
    GC.KeepAlive(commands);
});

var drawList = new DrawList();
drawList.EnsureCapacity(count);
Measure("typed-reused-draw-build", count, iterations, () =>
{
    drawList.Clear();
    for (var i = 0; i < count; i++) drawList.Add(drawable);
    GC.KeepAlive(drawList.Count);
});

Measure("text-effects-reference-copy", count, iterations, () =>
{
    for (var i = 0; i < count; i++) effectReferences[i] = legacyEffect;
    GC.KeepAlive(effectReferences[count - 1]);
});

Measure("text-effects-value-copy", count, iterations, () =>
{
    for (var i = 0; i < count; i++) valueEffects[i] = effect;
    checksum += valueEffects[count - 1].Outline?.Width ?? 0f;
});

Measure("text-effects-reference-create", count, iterations, () =>
{
    for (var i = 0; i < count; i++) effectReferences[i] = new LegacyTextEffects(effect);
    GC.KeepAlive(effectReferences[count - 1]);
});

Measure("packed-color-create", count, iterations, () =>
{
    for (var i = 0; i < count; i++) colors[i] = Color.FromArgb(255, i & 255, i >> 2 & 255, 255 - i & 255);
    checksum += colors[count - 1].R;
});

Measure("legacy-color-create", count, iterations, () =>
{
    for (var i = 0; i < count; i++)
        legacyColors[i] = new LegacyColor(System.Drawing.Color.FromArgb(255, i & 255, i >> 2 & 255, 255 - i & 255));
    checksum += legacyColors[count - 1].Value.R;
});

Measure("color-to-skcolor", count, iterations, () =>
{
    for (var i = 0; i < count; i++) skColors[i] = colors[i];
    checksum += skColors[count - 1].Red;
});


var style = LayoutStyle.Default;
Measure("layout-style-read", count, iterations * 4, () =>
{
    var sum = 0f;
    for (var i = 0; i < count; i++) sum += style.Width + style.PaddingLeft + style.MaxWidth;
    GC.KeepAlive(sum);
});

var styles = new LayoutStyle[count];
Measure("layout-style-copy", count, iterations, () =>
{
    for (var i = 0; i < count; i++) styles[i] = style;
    GC.KeepAlive(styles[count - 1]);
});
GC.KeepAlive(checksum);

static void Measure(string name, int count, int iterations, Action action)
{
    for (var i = 0; i < 20; i++) action();
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var before = GC.GetAllocatedBytesForCurrentThread();
    var stopwatch = Stopwatch.StartNew();
    for (var i = 0; i < iterations; i++) action();
    stopwatch.Stop();
    var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    var microseconds = stopwatch.Elapsed.TotalMicroseconds / iterations;
    Console.WriteLine($"| {name} | {count} | {microseconds:F2} | {microseconds * 1_000 / count:F2} | " +
                      $"{allocated / iterations} |");
}

sealed class NoopDrawable : IDrawable
{
    public SKPaint? Paint => null;

    public void Render(Gui gui, LayoutNode node, SKCanvas canvas)
    {
    }
}

sealed class LegacyTextEffects
{
    public TextEffects.TextOutline? Outline { get; }
    public TextEffects.TextShadow? DropShadow { get; }
    public TextEffects.TextShadow? InnerShadow { get; }
    public TextEffects.TextGradient? Gradient { get; }

    public LegacyTextEffects(TextEffects source)
    {
        Outline = source.Outline;
        DropShadow = source.DropShadow;
        InnerShadow = source.InnerShadow;
        Gradient = source.Gradient;
    }
}

readonly record struct LegacyColor(System.Drawing.Color Value);
