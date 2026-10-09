using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Tests the standalone base contract and its ordering beneath color and application sheets.</summary>
public class ExcaliburStylesBaseSheetTests
{
    /// <summary>The shipped base meets the same minimum contract required of replacements.</summary>
    [Fact]
    public void DefaultBaseIsComplete() =>
        Assert.Empty(ExcaliburStyles.ValidateBaseSheet(ExcaliburStyles.DefaultSheet));

    /// <summary>A replacement survives control rendering, collection removal, and moving the base.</summary>
    [Fact]
    public void EnsureKeepsTheSelectedBase()
    {
        using var harness = new FrameHarness(300, 100);
        var sheet = Replacement();
        var app = StyleSheet.Parse("button { color = #112233; }");
        harness.Gui.StyleSheets.Add(app);
        ExcaliburStyles.SetBaseSheet(harness.Gui, sheet);

        harness.Frame(g => g.Button("Save"));
        Assert.Equal([sheet, app], harness.Gui.StyleSheets);
        Assert.Equal(Color.ParseHex("#112233"),
            harness.Gui.StyleSheets.Resolve(new("button", null, [])).GetColor("color"));

        harness.Gui.StyleSheets.Remove(sheet);
        harness.Gui.StyleSheets.Add(sheet);
        harness.Frame(g => g.Button("Save"));
        Assert.Equal([sheet, app], harness.Gui.StyleSheets);

        harness.Gui.StyleSheets.Clear();
        harness.Frame(g => g.Button("Save"));
        Assert.Equal([sheet], harness.Gui.StyleSheets);
        Assert.DoesNotContain(ExcaliburStyles.DefaultSheet, harness.Gui.StyleSheets);
    }

    /// <summary>Base swaps keep the color overlay, host tokens, and the order of application overrides.</summary>
    [Fact]
    public void BaseChangesPreserveTheCascade()
    {
        var gui = new Gui();
        var first = StyleSheet.Parse("$text = #123456;");
        var second = StyleSheet.Parse("$surface = #654321;");
        gui.StyleSheets.Add(first);
        gui.StyleSheets.Add(second);
        gui.StyleSheets.SetToken("accent", "#fedcba");
        ExcaliburStyles.SetTheme(gui, ExcaliburStyles.Light);
        var sheet = Replacement();

        ExcaliburStyles.SetBaseSheet(gui, sheet);
        Assert.Equal([sheet, ExcaliburStyles.Light, first, second], gui.StyleSheets);
        Assert.Equal(Color.ParseHex("#123456"), ExcaliburStyles.TokenColor(gui, "text"));
        Assert.Equal(Color.ParseHex("#fedcba"), ExcaliburStyles.TokenColor(gui, "accent"));

        var other = Replacement("#aabbcc");
        ExcaliburStyles.SetBaseSheet(gui, other);
        Assert.Equal([other, ExcaliburStyles.Light, first, second], gui.StyleSheets);

        ExcaliburStyles.SetBaseSheet(gui, null);
        Assert.Equal([ExcaliburStyles.DefaultSheet, ExcaliburStyles.Light, first, second], gui.StyleSheets);
        Assert.Same(ExcaliburStyles.DefaultSheet, ExcaliburStyles.GetBaseSheet(gui));
    }

    /// <summary>Dark and null remove color overlays while preserving the selected replacement.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovingColorThemeKeepsTheBase(bool dark)
    {
        var gui = new Gui();
        var sheet = Replacement();
        ExcaliburStyles.SetBaseSheet(gui, sheet);
        ExcaliburStyles.SetTheme(gui, ExcaliburStyles.MonoLight);
        ExcaliburStyles.SetTheme(gui, dark ? ExcaliburStyles.Dark : null);

        Assert.Equal([sheet], gui.StyleSheets);
        Assert.Equal(Color.ParseHex("#123456"), ExcaliburStyles.TokenColor(gui, "text"));
    }

    /// <summary>Selections are local to each GUI and repeating a selection leaves resolved caches intact.</summary>
    [Fact]
    public void BaseSelectionIsLocalAndIdempotent()
    {
        var gui = new Gui();
        var other = new Gui();
        var sheet = Replacement();
        ExcaliburStyles.SetBaseSheet(gui, sheet);
        var version = gui.StyleSheets.Version;
        ExcaliburStyles.SetBaseSheet(gui, sheet);

        Assert.Equal(version, gui.StyleSheets.Version);
        Assert.Same(sheet, ExcaliburStyles.GetBaseSheet(gui));
        Assert.Same(ExcaliburStyles.DefaultSheet, ExcaliburStyles.GetBaseSheet(other));
        ExcaliburStyles.SetBaseSheet(other, null);
        Assert.Equal([ExcaliburStyles.DefaultSheet], other.StyleSheets);
    }

    /// <summary>Incomplete replacements report gaps and leave the existing cascade untouched.</summary>
    [Fact]
    public void InvalidBaseIsRejectedBeforeMutation()
    {
        var gui = new Gui();
        var sheet = Replacement();
        ExcaliburStyles.SetBaseSheet(gui, sheet);
        ExcaliburStyles.SetTheme(gui, ExcaliburStyles.Light);
        var version = gui.StyleSheets.Version;
        var invalid = StyleSheet.Parse("$text = #112233; button { color = $text; }");

        var error = Assert.Throws<ArgumentException>(() => ExcaliburStyles.SetBaseSheet(gui, invalid));

        Assert.Contains("--surface", error.Message);
        Assert.Contains("form", error.Message);
        Assert.Contains("color-picker", error.Message);
        Assert.Equal("sheet", error.ParamName);
        Assert.Equal(version, gui.StyleSheets.Version);
        Assert.Equal([sheet, ExcaliburStyles.Light], gui.StyleSheets);
        Assert.Same(sheet, ExcaliburStyles.GetBaseSheet(gui));
    }

    /// <summary>Missing, cyclic, and incorrectly typed fallback tokens are diagnosed without overlays.</summary>
    [Theory]
    [InlineData("$text = $missing;", "--text")]
    [InlineData("$text = $text;", "--text")]
    [InlineData("$radius = -1;", "--radius")]
    [InlineData("$radius = 20%;", "--radius")]
    [InlineData("$radius = blue;", "--radius")]
    public void InvalidTokensAreDiagnosed(string declarations, string token)
    {
        var sheet = StyleSheet.Parse(ExcaliburStyles.DefaultSheetText + declarations);

        Assert.Contains(ExcaliburStyles.ValidateBaseSheet(sheet), error => error.StartsWith(token + ":"));
    }

    /// <summary>Unresolved control declarations fail even when fallback tokens themselves are complete.</summary>
    [Theory]
    [InlineData("$missing")]
    [InlineData("var(--missing)")]
    public void InvalidControlRuleIsDiagnosed(string value)
    {
        var sheet = StyleSheet.Parse(ExcaliburStyles.DefaultSheetText + $"form {{ dim-color = {value}; }}",
            new StyleSheetOptions { AllowCssSyntax = true });

        Assert.Contains("form: missing or unresolved dim-color", ExcaliburStyles.ValidateBaseSheet(sheet));
    }

    /// <summary>Gradient backgrounds can provide a required surface without a background-color declaration.</summary>
    [Fact]
    public void BackgroundShorthandMeetsTheContract()
    {
        var text = ExcaliburStyles.DefaultSheetText.Replace("progress fill { background-color = $accent;",
            "progress fill { background = linear-gradient(to right, $surface, $popup);", StringComparison.Ordinal);

        Assert.Empty(ExcaliburStyles.ValidateBaseSheet(StyleSheet.Parse(text)));
    }

    /// <summary>Checkerboard tones require the color property their painter reads.</summary>
    [Fact]
    public void CheckerRequiresItsBackgroundColor()
    {
        var text = ExcaliburStyles.DefaultSheetText.Replace("checker { background-color = #f2f2f2;",
            "checker { background = linear-gradient(to right, #fff, #aaa);", StringComparison.Ordinal);

        Assert.Contains("checker: missing or unresolved background-color",
            ExcaliburStyles.ValidateBaseSheet(StyleSheet.Parse(text)));
    }

    /// <summary>Selecting a complete sheet already used as a color overlay promotes it without duplication.</summary>
    [Fact]
    public void BaseAndThemeCannotDuplicateTheSameSheet()
    {
        var gui = new Gui();
        var sheet = Replacement();
        ExcaliburStyles.SetTheme(gui, sheet);
        ExcaliburStyles.SetBaseSheet(gui, sheet);
        ExcaliburStyles.SetTheme(gui, sheet);
        Assert.Equal([sheet], gui.StyleSheets);

        ExcaliburStyles.SetTheme(gui, ExcaliburStyles.Light);
        Assert.Equal([sheet, ExcaliburStyles.Light], gui.StyleSheets);
    }

    /// <summary>System appearance changes affect the color overlay and keep the replacement selected.</summary>
    [Fact]
    public void FollowingAppearanceKeepsTheBase()
    {
        var gui = new Gui();
        var sheet = Replacement();
        ExcaliburStyles.SetBaseSheet(gui, sheet);
        ExcaliburStyles.FollowSystemAppearance(gui, fallback: ExcaliburStyles.Light, accent: false);

        Assert.Equal([sheet, ExcaliburStyles.Light], gui.StyleSheets);
        Assert.Same(sheet, ExcaliburStyles.GetBaseSheet(gui));
    }

    /// <summary>Public base APIs reject a missing GUI or sheet explicitly.</summary>
    [Fact]
    public void BaseApisCheckArguments()
    {
        Assert.Throws<ArgumentNullException>(() => ExcaliburStyles.GetBaseSheet(null!));
        Assert.Throws<ArgumentNullException>(() => ExcaliburStyles.SetBaseSheet(null!, null));
        Assert.Throws<ArgumentNullException>(() => ExcaliburStyles.Ensure(null!));
        Assert.Throws<ArgumentNullException>(() => ExcaliburStyles.ValidateBaseSheet(null!));
    }

    static StyleSheet Replacement(string text = "#123456") =>
        StyleSheet.Parse(ExcaliburStyles.DefaultSheetText + $"\n$text = {text};\n");
}
