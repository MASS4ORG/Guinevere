using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Tests for <see cref="ExcaliburStyles"/>: the default sheet and the built-in color themes.</summary>
public class ExcaliburStylesTests
{
    /// <summary>The embedded sheet loads, parses and carries its theme metadata.</summary>
    [Fact]
    public void DefaultSheet_Loads()
    {
        Assert.Contains("button", ExcaliburStyles.DefaultSheetText);
        Assert.Equal("\"Guinevere Dark\"", ExcaliburStyles.DefaultSheet.Constants["theme-name"]);
        Assert.Same(ExcaliburStyles.DefaultSheet, ExcaliburStyles.Dark);
        Assert.Equal("dark", ExcaliburStyles.DefaultSheet.Constants["theme-kind"]);
    }

    /// <summary>Drawing a control puts the default sheet first, below application sheets, even after it is moved.</summary>
    [Fact]
    public void Controls_KeepTheDefaultSheetFirst()
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
    [InlineData("light", "light", "#ffffff", "#000000")]
    [InlineData("mono-light", "light", "#ffffff", "#1f2328")]
    [InlineData("mono-dark", "dark", "#1e222a", "#d7dae0")]
    public void Themes_DefineTheirTokens(string name, string kind, string surface, string text)
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
    public void SetTheme_LayersAndReplaces()
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
    public void TokenColorAndDroppableArea_FollowTheTheme()
    {
        var gui = new Gui();

        Assert.Equal(Color.Transparent, ExcaliburStyles.TokenColor(gui, "no-such-token"));
        var style = ExcaliburStyles.DroppableArea(gui);
        Assert.Equal(Parse("#58b074"), style.Accepted);
        Assert.Equal(Parse("#d06666"), style.Rejected);
    }

    static Color Parse(string hex) => StyleValue.TryColor(hex, out var color) ? color : throw new ArgumentException(hex);
}
