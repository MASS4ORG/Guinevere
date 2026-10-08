using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Tests for <see cref="ExcaliburStyles"/>: the default sheet and the transitional palette bridge.</summary>
public class ExcaliburStylesTests
{
    /// <summary>The embedded sheet loads, parses and carries its theme metadata.</summary>
    [Fact]
    public void DefaultSheet_Loads()
    {
        Assert.Contains("button", ExcaliburStyles.DefaultSheetText);
        Assert.Equal("\"Guinevere Light\"", ExcaliburStyles.DefaultSheet.Constants["theme-name"]);
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

    /// <summary>A custom palette reaches the styled buttons through tokens; the light default adds none.</summary>
    [Fact]
    public void ControlPalette_IsBridgedToTokens()
    {
        using var harness = new FrameHarness(200, 80);
        harness.Frame(g => g.Button("Save"));
        Assert.Empty(harness.Gui.StyleSheets.Tokens);

        harness.Gui.ControlPalette = ControlPalette.Dark;
        harness.Frame(g => g.Button("Save"));
        var version = harness.Gui.StyleSheets.Version;
        harness.Frame(g => g.Button("Save"));

        Assert.Equal(StyleValue.Format(ControlPalette.Dark.Surface), harness.Gui.StyleSheets.Tokens["--surface"]);
        Assert.Equal(version, harness.Gui.StyleSheets.Version);
    }
}
