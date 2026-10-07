using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Styling;

/// <summary>Tests for <see cref="StyleCache"/> through <see cref="StyleSheetCollection"/>.</summary>
public class StyleCacheTests
{
    const string Widths = """
        button { width = 1; }
        button:hover { width = 2; }
        button:checked { width = 3; }
        Panel > button { width = 4; }
        button.big { width = 5; }
        #ok { width = 6; }
        """;

    static StyleSheetCollection Collection(params string[] sheets)
    {
        var collection = new StyleSheetCollection();
        foreach (var sheet in sheets) collection.Add(StyleSheet.Parse(sheet));
        return collection;
    }

    /// <summary>A repeated target returns the cached instance.</summary>
    [Fact]
    public void Hit_ReturnsSameInstance()
    {
        var sheets = Collection(Widths);
        var target = new StyleTarget("button", null, ["big"]);

        var first = sheets.Resolve(target);

        Assert.Same(first, sheets.Resolve(target with { Classes = ["big"] }));
        Assert.Same(first, StyleResolver.Resolve(sheets, target));
        Assert.Equal(1, sheets.CachedCount);
    }

    /// <summary>Cache hits, including those with ancestors and through <c>gui.ResolveStyle</c>, allocate nothing.</summary>
    [Fact]
    public void Hit_AllocatesNothing()
    {
        var gui = new Gui();
        gui.StyleSheets.Add(StyleSheet.Parse(Widths));
        string[] classes = ["big"];
        var ancestors = new List<StyleTarget> { new("Panel", null, []) };
        var target = new StyleTarget("button", null, classes, StyleState.Hover, ["checked"], ancestors);
        var cached = gui.StyleSheets.Resolve(target);
        gui.ResolveStyle("button", classes, state: StyleState.Hover);
        ResolvedStyle? last = null;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            last = gui.StyleSheets.Resolve(target);
            GC.KeepAlive(gui.ResolveStyle("button", classes, state: StyleState.Hover));
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.Same(cached, last);
    }

    /// <summary>Every element of the key selects its own entry.</summary>
    [Fact]
    public void Key_DistinguishesStateModifiersAncestorsClassesAndId()
    {
        var sheets = Collection(Widths);
        var plain = new StyleTarget("button", null, []);

        Assert.Equal("1", sheets.Resolve(plain).Get("width"));
        Assert.Equal("2", sheets.Resolve(plain with { State = StyleState.Hover }).Get("width"));
        Assert.Equal("3", sheets.Resolve(plain with { Modifiers = ["checked"] }).Get("width"));
        Assert.Equal("4", sheets.Resolve(plain with { Ancestors = [new StyleTarget("Panel", null, [])] }).Get("width"));
        Assert.Equal("5", sheets.Resolve(plain with { Classes = ["big"] }).Get("width"));
        Assert.Equal("6", sheets.Resolve(plain with { Id = "ok" }).Get("width"));
        Assert.Equal("1", sheets.Resolve(plain with { Type = "button", Modifiers = [] }).Get("width"));
        Assert.Same(sheets.Resolve(plain), sheets.Resolve(plain with { Modifiers = [], Ancestors = [] }));
        Assert.Equal(6, sheets.CachedCount);
    }

    /// <summary>Inserted keys are snapshots: reusing the caller's lists later cannot corrupt the cache.</summary>
    [Fact]
    public void Insert_SnapshotsCallerLists()
    {
        var sheets = Collection(Widths);
        var ancestors = new List<StyleTarget> { new("Panel", null, []) };
        var classes = new List<string>();
        var target = new StyleTarget("button", null, classes, Ancestors: ancestors);
        Assert.Equal("4", sheets.Resolve(target).Get("width"));

        ancestors[0] = new StyleTarget("Other", null, []);
        classes.Add("big");

        Assert.Equal("5", sheets.Resolve(target).Get("width"));
        Assert.Equal("4", sheets.Resolve(target with
        {
            Classes = [],
            Ancestors = [new StyleTarget("Panel", null, [])],
        }).Get("width"));
    }

    /// <summary>Adding, inserting, replacing, removing and clearing sheets bump the version and invalidate.</summary>
    [Fact]
    public void SheetChanges_Invalidate()
    {
        var sheets = Collection("box { width = 1; }");
        var target = new StyleTarget("box", null, []);
        Assert.Equal("1", sheets.Resolve(target).Get("width"));

        var version = sheets.Version;
        sheets.Add(StyleSheet.Parse("box { width = 2; }"));
        Assert.Equal("2", sheets.Resolve(target).Get("width"));
        sheets.Insert(2, StyleSheet.Parse("box { width = 3; }"));
        Assert.Equal("3", sheets.Resolve(target).Get("width"));
        sheets[2] = StyleSheet.Parse("box { width = 4; }");
        Assert.Equal("4", sheets.Resolve(target).Get("width"));
        sheets.RemoveAt(2);
        Assert.Equal("2", sheets.Resolve(target).Get("width"));
        sheets.Clear();
        Assert.Same(ResolvedStyle.Empty, sheets.Resolve(target));
        Assert.Equal(version + 5, sheets.Version);
        Assert.Equal(0, sheets.CachedCount);
        Assert.Throws<ArgumentNullException>(() => sheets.Add(null!));
    }

    /// <summary>Host token overrides sit above every sheet and invalidate the cache.</summary>
    [Fact]
    public void TokenOverrides_InvalidateAndLayerAboveSheets()
    {
        var sheets = Collection("$accent = red; box { color = $accent; }", "$accent = blue;");
        var target = new StyleTarget("box", null, []);
        Assert.Equal("blue", sheets.Resolve(target).Get("color"));

        sheets.SetToken("$accent", "green");
        Assert.Equal("green", sheets.Resolve(target).Get("color"));
        Assert.Equal("green", sheets.Tokens["--accent"]);
        sheets.SetToken("accent", "white");
        Assert.Equal("white", sheets.Resolve(target).Get("color"));

        var version = sheets.Version;
        Assert.True(sheets.RemoveToken("--accent"));
        Assert.Equal("blue", sheets.Resolve(target).Get("color"));
        Assert.False(sheets.RemoveToken("accent"));
        Assert.Equal(version + 1, sheets.Version);
        Assert.Throws<ArgumentException>(() => sheets.SetToken(" ", "x"));
    }

    /// <summary>Call-site variables re-expand only referencing declarations and never pollute the cache.</summary>
    [Fact]
    public void ScopedVariables_ReexpandWithoutPollutingCache()
    {
        var sheets = Collection("$progress = 0; progress { width = $progress; height = 5; } bar { height = 5; }");
        var target = new StyleTarget("progress", null, []);
        var cached = sheets.Resolve(target);

        var scoped = sheets.Resolve(target, [new StyleVariable("progress", 0.65f)]);

        Assert.NotSame(cached, scoped);
        Assert.Equal(("0.65", "5"), (scoped.Get("width"), scoped.Get("height")));
        Assert.Same(cached, sheets.Resolve(target, []));
        Assert.Equal("0", sheets.Resolve(target).Get("width"));
        var bar = new StyleTarget("bar", null, []);
        Assert.Same(sheets.Resolve(bar), sheets.Resolve(bar, [new StyleVariable("progress", 1)]));
    }

    /// <summary>Reaching the capacity clears the cache instead of growing without bound.</summary>
    [Fact]
    public void Capacity_BoundsEntries()
    {
        var sheets = Collection(Widths);
        for (var i = 0; i <= StyleCache.Capacity; i++) sheets.Resolve(new StyleTarget("button", $"id{i}", []));

        Assert.Equal(1, sheets.CachedCount);
        Assert.Equal("6", sheets.Resolve(new StyleTarget("button", "ok", [])).Get("width"));
    }

    /// <summary>A source reload invalidates the cache and the next frame draws the new style.</summary>
    [Fact]
    public void Reload_IsVisibleInNextFrame()
    {
        using var harness = new FrameHarness(40, 40);
        var color = "#ff0000";
        var source = StyleSheetSource.FromProvider(() =>
            $"box {{ width = expand; height = expand; bg-color = {color}; }}");
        harness.Gui.AddStyleSheet(source);
        static void Draw(Gui gui)
        {
            using (gui.StyledNode("box").Enter()) { }
        }

        harness.Frame(Draw);
        Assert.Equal(SKColors.Red, Pixel(harness));

        color = "#00ff00";
        Assert.True(source.TryReload());
        harness.Frame(Draw);
        Assert.Equal(SKColors.Lime, Pixel(harness));
    }

    static SKColor Pixel(FrameHarness harness)
    {
        using var image = harness.Snapshot();
        using var pixmap = image.PeekPixels();
        return pixmap.GetPixelColor(20, 20);
    }
}
