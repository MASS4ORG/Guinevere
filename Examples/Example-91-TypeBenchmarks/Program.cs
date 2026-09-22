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
var referenceColors = new ReferenceColor[count];
var skColors = new SKColor[count];
var checksum = 0f;
var effect = new TextEffects
{
    Outline = new TextEffects.TextOutline(Color.Black, 2f),
    DropShadow = new TextEffects.TextShadow(Color.Black, new Vector2(2f), 4f),
    InnerShadow = new TextEffects.TextShadow(Color.White, new Vector2(-1f), 2f),
    Gradient = new TextEffects.TextGradient(Color.Red, Color.Blue)
};
var referenceEffect = new ReferenceTextEffects(effect);
var effectReferences = new ReferenceTextEffects[count];
var valueEffects = new TextEffects[count];

Console.WriteLine($"Runtime: {Environment.Version}; Vector width: {Vector<byte>.Count * 8} bits");
Console.WriteLine($"Sizes: Color={Unsafe.SizeOf<Color>()} B, TextEffects={Unsafe.SizeOf<TextEffects>()} B, " +
                  $"reference Color={Unsafe.SizeOf<ReferenceColor>()} B, " +
                  $"LayoutStyle={Unsafe.SizeOf<LayoutStyle>()} B");
Console.WriteLine("| Scenario | Items/op | Mean us | ns/item | Alloc B/op |");
Console.WriteLine("|---|---:|---:|---:|---:|");

Measure("polymorphic-draw-build", count, iterations, () =>
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
    for (var i = 0; i < count; i++) effectReferences[i] = referenceEffect;
    GC.KeepAlive(effectReferences[count - 1]);
});

Measure("text-effects-value-copy", count, iterations, () =>
{
    for (var i = 0; i < count; i++) valueEffects[i] = effect;
    checksum += valueEffects[count - 1].Outline?.Width ?? 0f;
});

Measure("text-effects-reference-create", count, iterations, () =>
{
    for (var i = 0; i < count; i++) effectReferences[i] = new ReferenceTextEffects(effect);
    GC.KeepAlive(effectReferences[count - 1]);
});

Measure("packed-color-create", count, iterations, () =>
{
    for (var i = 0; i < count; i++) colors[i] = Color.FromArgb(255, i & 255, i >> 2 & 255, 255 - i & 255);
    checksum += colors[count - 1].R;
});

Measure("reference-color-create", count, iterations, () =>
{
    for (var i = 0; i < count; i++)
        referenceColors[i] = new ReferenceColor(System.Drawing.Color.FromArgb(255, i & 255, i >> 2 & 255, 255 - i & 255));
    checksum += referenceColors[count - 1].Value.R;
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

// Style token lookup: compare direct palette reads, production flattened slots, and ancestor-walking slots.
var scopeGui = new Gui();
var scopeNodes = new LayoutNode[9];
scopeNodes[0] = new LayoutNode("scope-0", scopeGui, null);
for (var i = 1; i < scopeNodes.Length; i++)
    scopeNodes[i] = new LayoutNode($"scope-{i}", scopeGui, scopeNodes[i - 1]);

var rootScope = scopeNodes[0].Scope;
var leafScope = scopeNodes[^1].Scope;
rootScope.Set(new LayoutNodeScopeTextColor { Value = Color.Red });
rootScope.Set(new LayoutNodeScopeTextSize { Value = 14f });
rootScope.Set(new LayoutNodeScopeZIndex { Value = 2 });
rootScope.Set(new LayoutNodeScopeIsClipped { Value = true });
rootScope.Set(new LayoutNodeScopeIsScrollContainer { Value = true });
rootScope.Set(new LayoutNodeScopeLocalScrollOffset { Value = Vector2.One });
rootScope.Set(new LayoutNodeScopeCumulativeScrollOffset { Value = Vector2.One });
rootScope.Set(new LayoutNodeScopeEscapesAncestorClips { Value = false });
var currentTokenSet = new object[]
{
    new LayoutNodeScopeTextColor { Value = Color.Red },
    new LayoutNodeScopeTextSize { Value = 14f },
    new LayoutNodeScopeZIndex { Value = 2 },
    new LayoutNodeScopeIsClipped { Value = true },
    new LayoutNodeScopeIsScrollContainer { Value = true },
    new LayoutNodeScopeLocalScrollOffset { Value = Vector2.One },
    new LayoutNodeScopeCumulativeScrollOffset { Value = Vector2.One },
    new LayoutNodeScopeEscapesAncestorClips { Value = false }
};

var palette = ControlPalette.Dark;
Measure("style-aggregate-8-reads", count, iterations * 4, () =>
{
    var sum = 0;
    for (var i = 0; i < count; i++)
    {
        sum += palette.Surface.R + palette.Border.R + palette.Accent.R + palette.Text.R;
        sum += palette.TextDim.R + palette.TextDisabled.R + palette.Selected.R + palette.Divider.R;
    }
    GC.KeepAlive(sum);
});

Measure("style-slotted-local-8-reads", count, iterations, () =>
{
    var sum = 0f;
    for (var i = 0; i < count; i++) sum += ReadCurrentTokens(rootScope);
    GC.KeepAlive(sum);
});

Measure("style-slotted-depth8-8-reads", count, iterations, () =>
{
    var sum = 0f;
    for (var i = 0; i < count; i++) sum += ReadCurrentTokens(leafScope);
    GC.KeepAlive(sum);
});

Measure("style-slotted-missing-read", count, iterations, () =>
{
    var sum = 0;
    for (var i = 0; i < count; i++) sum += leafScope.Get<LayoutNodeScopeScrollContainerId>().Value?.Length ?? 0;
    GC.KeepAlive(sum);
});

var indexedRoot = new IndexedScope(null, 20);
var indexedLeaf = indexedRoot;
for (var i = 0; i < 8; i++) indexedLeaf = new IndexedScope(indexedLeaf, 20);
var tokenSet = Enumerable.Range(0, 20).Select(i => (object)new BoxedToken(i)).ToArray();
indexedRoot.Apply(tokenSet);

Measure("style-indexed-depth8-8-reads", count, iterations, () =>
{
    var sum = 0;
    for (var i = 0; i < count; i++)
        for (var slot = 0; slot < 8; slot++) sum += indexedLeaf.Get<BoxedToken>(slot, BoxedToken.Default).Value;
    GC.KeepAlive(sum);
});

var flatTokens = new object?[20];
Array.Copy(tokenSet, flatTokens, tokenSet.Length);
Measure("style-indexed-flat-8-reads", count, iterations, () =>
{
    var sum = 0;
    for (var i = 0; i < count; i++)
        for (var slot = 0; slot < 8; slot++) sum += ((BoxedToken)flatTokens[slot]!).Value;
    GC.KeepAlive(sum);
});

Measure("style-indexed-apply-20", 20, iterations * 20, () => indexedRoot.Apply(tokenSet));

Measure("style-slotted-apply-8", 8, iterations * 20, () =>
{
    rootScope.Set((LayoutNodeScopeTextColor)currentTokenSet[0]);
    rootScope.Set((LayoutNodeScopeTextSize)currentTokenSet[1]);
    rootScope.Set((LayoutNodeScopeZIndex)currentTokenSet[2]);
    rootScope.Set((LayoutNodeScopeIsClipped)currentTokenSet[3]);
    rootScope.Set((LayoutNodeScopeIsScrollContainer)currentTokenSet[4]);
    rootScope.Set((LayoutNodeScopeLocalScrollOffset)currentTokenSet[5]);
    rootScope.Set((LayoutNodeScopeCumulativeScrollOffset)currentTokenSet[6]);
    rootScope.Set((LayoutNodeScopeEscapesAncestorClips)currentTokenSet[7]);
});
GC.KeepAlive(checksum);

static float ReadCurrentTokens(LayoutNodeScope scope) =>
    scope.Get<LayoutNodeScopeTextColor>().Value.R +
    scope.Get<LayoutNodeScopeTextSize>().Value +
    scope.Get<LayoutNodeScopeZIndex>().Value +
    (scope.Get<LayoutNodeScopeIsClipped>().Value ? 1 : 0) +
    (scope.Get<LayoutNodeScopeIsScrollContainer>().Value ? 1 : 0) +
    scope.Get<LayoutNodeScopeLocalScrollOffset>().Value.X +
    scope.Get<LayoutNodeScopeCumulativeScrollOffset>().Value.X +
    (scope.Get<LayoutNodeScopeEscapesAncestorClips>().Value ? 1 : 0);

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

sealed class ReferenceTextEffects
{
    public TextEffects.TextOutline? Outline { get; }
    public TextEffects.TextShadow? DropShadow { get; }
    public TextEffects.TextShadow? InnerShadow { get; }
    public TextEffects.TextGradient? Gradient { get; }

    public ReferenceTextEffects(TextEffects source)
    {
        Outline = source.Outline;
        DropShadow = source.DropShadow;
        InnerShadow = source.InnerShadow;
        Gradient = source.Gradient;
    }
}

readonly record struct ReferenceColor(System.Drawing.Color Value);

sealed record BoxedToken(int Value)
{
    public static BoxedToken Default { get; } = new(0);
}

sealed class IndexedScope(IndexedScope? parent, int capacity)
{
    readonly object?[] _values = new object?[capacity];

    public void Apply(object[] values) => Array.Copy(values, _values, Math.Min(values.Length, _values.Length));

    public T Get<T>(int slot, T fallback) where T : class
    {
        for (var current = this; current is not null; current = current.Parent)
            if (current._values[slot] is T value)
                return value;
        return fallback;
    }

    IndexedScope? Parent { get; } = parent;
}
