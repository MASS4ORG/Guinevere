using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Tests for <see cref="ExcaliburStyles"/>: the default sheet and the built-in color themes.</summary>
public class ExcaliburStylesTests
{
    /// <summary>The embedded sheet loads, parses and carries its theme metadata.</summary>
    [Fact]
    public void DefaultSheetLoads()
    {
        Assert.Contains("button", ExcaliburStyles.DefaultSheetText);
        Assert.Equal("\"Material You Dark\"", ExcaliburStyles.DefaultSheet.Constants["theme-name"]);
        Assert.Same(ExcaliburStyles.DefaultSheet, ExcaliburStyles.Dark);
        Assert.Equal("dark", ExcaliburStyles.DefaultSheet.Constants["theme-kind"]);
    }

    /// <summary>Drawing a control puts the default sheet first, below application sheets, even after it is moved.</summary>
    [Fact]
    public void ControlsKeepTheDefaultSheetFirst()
    {
        using var harness = new FrameHarness(200, 80);
        var theme = StyleSheet.Parse("button { border-radius = 0; }");
        harness.Gui.StyleSheets.Add(theme);

        harness.Frame(g => g.Button("Save"));
        Assert.Same(ExcaliburStyles.DefaultSheet, harness.Gui.StyleSheets[0]);
        Assert.Same(theme, harness.Gui.StyleSheets[1]);

        harness.Gui.StyleSheets.RemoveAt(0);
        harness.Gui.StyleSheets.Add(ExcaliburStyles.DefaultSheet);
        harness.Frame(g => g.Button("Save"));
        Assert.Equal([ExcaliburStyles.DefaultSheet, theme], harness.Gui.StyleSheets);
    }

    /// <summary>Each built-in theme is a token sheet with its kind and the colors it was designed with.</summary>
    [Theory]
    [InlineData("light", "light", "#edeef4", "#20242a")]
    [InlineData("mono-light", "light", "#ebedf1", "#1b1f27")]
    [InlineData("mono-dark", "dark", "#232731", "#d7dae0")]
    public void ThemesDefineTheirTokens(string name, string kind, string surface, string text)
    {
        var sheet = name switch
        {
            "light" => ExcaliburStyles.Light,
            "mono-light" => ExcaliburStyles.MonoLight,
            _ => ExcaliburStyles.MonoDark,
        };
        var gui = new Gui();
        ExcaliburStyles.SetTheme(gui, sheet);

        Assert.Equal(kind, sheet.Constants["theme-kind"]);
        Assert.Empty(sheet.Rules);
        Assert.Equal(Parse(surface), ExcaliburStyles.TokenColor(gui, "surface"));
        Assert.Equal(Parse(text), ExcaliburStyles.TokenColor(gui, "$text"));
    }

    /// <summary>
    /// A theme sits right above the default sheet and below application sheets; setting another replaces it, and the
    /// dark (default) theme or <c>null</c> removes it.
    /// </summary>
    [Fact]
    public void SetThemeLayersAndReplaces()
    {
        var gui = new Gui();
        var app = StyleSheet.Parse("$text = #123456;");
        gui.StyleSheets.Add(app);

        ExcaliburStyles.SetTheme(gui, ExcaliburStyles.Light);
        Assert.Equal([ExcaliburStyles.DefaultSheet, ExcaliburStyles.Light, app], gui.StyleSheets);
        Assert.Equal(Parse("#123456"), ExcaliburStyles.TokenColor(gui, "text"));

        var version = gui.StyleSheets.Version;
        ExcaliburStyles.SetTheme(gui, ExcaliburStyles.Light);
        Assert.Equal(version, gui.StyleSheets.Version);

        ExcaliburStyles.SetTheme(gui, ExcaliburStyles.MonoDark);
        Assert.Equal([ExcaliburStyles.DefaultSheet, ExcaliburStyles.MonoDark, app], gui.StyleSheets);

        ExcaliburStyles.SetTheme(gui, ExcaliburStyles.Dark);
        Assert.Equal([ExcaliburStyles.DefaultSheet, app], gui.StyleSheets);

        ExcaliburStyles.SetTheme(gui, ExcaliburStyles.MonoLight);
        ExcaliburStyles.SetTheme(gui, null);
        Assert.Equal([ExcaliburStyles.DefaultSheet, app], gui.StyleSheets);
    }

    /// <summary>An unknown token reads as transparent; drop feedback follows the theme's semantic colors.</summary>
    [Fact]
    public void TokenColorAndDroppableAreaFollowTheTheme()
    {
        var gui = new Gui();

        Assert.Equal(Color.Transparent, ExcaliburStyles.TokenColor(gui, "no-such-token"));
        var style = ExcaliburStyles.DroppableArea(gui);
        Assert.Equal(Parse("#6dd58c"), style.Accepted);
        Assert.Equal(Parse("#f2b8b5"), style.Rejected);
    }

    /// <summary>The Material default is a complete base sheet.</summary>
    [Fact]
    public void DefaultSheetIsAValidBase() => Assert.Empty(ExcaliburStyles.ValidateBaseSheet(ExcaliburStyles.DefaultSheet));

    /// <summary>Setting a theme hands the sheets' control sizes to the controls; sheets without them keep the defaults.</summary>
    [Fact]
    public void ApplyMetricsReadsTheSheetTokens()
    {
        using var harness = new FrameHarness(200, 80);
        float field = 0, compact = 0, indicator = 0, classicField = 0;
        harness.Frame(g =>
        {
            ExcaliburStyles.SetTheme(g, ExcaliburStyles.Light);
            (field, compact, indicator) = (g.ControlStyle.FieldHeight, g.ControlStyle.CompactHeight,
                g.ControlStyle.IndicatorSize);
        });
        Assert.Equal((28f, 22f, 18f), (field, compact, indicator));

        using var classic = new FrameHarness(200, 80);
        ExcaliburStyles.SetBaseSheet(classic.Gui, ExcaliburStyles.Classic);
        classic.Frame(g =>
        {
            ExcaliburStyles.SetTheme(g, null);
            classicField = g.ControlStyle.FieldHeight;
        });
        Assert.Equal(ControlMetrics.FieldHeight, classicField);

        harness.Gui.StyleSheets.Add(StyleSheet.Parse("$control-field-height = 40;"));
        harness.Frame(g =>
        {
            ExcaliburStyles.SetTheme(g, ExcaliburStyles.Light);
            field = g.ControlStyle.FieldHeight;
        });
        Assert.Equal(40f, field);
    }

    /// <summary>The classic look is selectable as a complete base and keeps the pre-Material palette.</summary>
    [Fact]
    public void ClassicIsASelectableBase()
    {
        var gui = new Gui();
        ExcaliburStyles.SetBaseSheet(gui, ExcaliburStyles.Classic);

        Assert.Equal([ExcaliburStyles.Classic], gui.StyleSheets);
        Assert.Equal(Parse("#548fe0"), ExcaliburStyles.TokenColor(gui, "accent"));
    }

    /// <summary>A theme that overrides only a Material role recolors the slots and the controls aliased to it.</summary>
    [Fact]
    public void RoleOverridesReachSlotsAndControls()
    {
        using var harness = new FrameHarness(200, 80);
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("$md-primary = #123456; $md-on-surface = #fedcba;"));

        var primary = PrimaryButtonBackground(harness);

        Assert.Equal(Parse("#123456"), primary);
        Assert.Equal(Parse("#123456"), ExcaliburStyles.TokenColor(harness.Gui, "accent"));
        Assert.Equal(Parse("#fedcba"), ExcaliburStyles.TokenColor(harness.Gui, "text"));
    }

    /// <summary>A theme written with the slot names alone still recolors the controls.</summary>
    [Fact]
    public void SlotOverridesStillRecolorControls()
    {
        using var harness = new FrameHarness(200, 80);
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("$accent = #abcdef;"));

        Assert.Equal(Parse("#abcdef"), PrimaryButtonBackground(harness));
    }

    static Color PrimaryButtonBackground(FrameHarness harness)
    {
        Color color = default;
        harness.Frame(g =>
        {
            ExcaliburStyles.Ensure(g);
            color = g.ResolveStyle("button", ["primary"]).GetColor("background-color") ?? default;
        });
        return color;
    }

    static Color Parse(string hex) => StyleValue.TryColor(hex, out var color) ? color : throw new ArgumentException(hex);
}
