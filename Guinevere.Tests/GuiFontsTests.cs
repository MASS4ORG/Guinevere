using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>Font fallback, scoped measurement, compatibility shortcuts and display scaling.</summary>
public class GuiFontsTests
{
    /// <summary>Supplementary code points use a registered fallback without splitting surrogate pairs.</summary>
    [Fact]
    public void SupplementaryCodePointsSelectOneFallbackRun()
    {
        using var frame = new FrameHarness();
        frame.Gui.Fonts.RegisterFile("Text", FontRegistryTests.FontPath());
        frame.Gui.Fonts.RegisterFile("Emoji", FontRegistryTests.FontPath("icons.ttf"));
        var font = frame.Gui.Fonts.Resolve("Text, Emoji", 18)!;
        Assert.Equal((ushort)0, font.SkFont.GetGlyph(0x1f600));
        Assert.NotEqual((ushort)0, font.Fallbacks[0].SkFont.GetGlyph(0x1f600));
        frame.Frame(gui =>
        {
            gui.SetTextFont(font);
            var primary = gui.GetTextFont(18);
            var runs = gui.CreateTextRuns("A😀B", primary, primary);
            Assert.Equal(new[] { "A", "😀", "B" }, runs.Select(run => run.Text));
            Assert.Equal(font.Fallbacks[0].FamilyName, runs[1].Font.FamilyName);
            var measured = gui.MeasureTextWidth("A😀B", 18);
            Assert.Equal(measured, primary.MeasureText("A😀B").W);
            var node = gui.DrawText("A😀B", 18);
            Assert.Equal(measured, node.Style.Width);
        });
    }

    /// <summary>Drawing and measurement apply the same validated display scale.</summary>
    [Theory]
    [InlineData(1f, 1f)]
    [InlineData(2f, 2f)]
    [InlineData(0f, 1f)]
    [InlineData(float.NaN, 1f)]
    public void DpiIsAppliedEquallyToDrawingAndMeasurement(float scale, float expected)
    {
        using var frame = new FrameHarness();
        var display = Substitute.For<IDisplayCapability>();
        display.ScaleFactor.Returns(scale);
        frame.Gui.Platform.Register(display);
        frame.Frame(gui =>
        {
            Assert.Equal(expected, gui.FontScale);
            Assert.Equal(20 * expected, gui.GetTextFont(20).Size);
            var node = gui.DrawText("Text", 20);
            Assert.Equal(gui.MeasureTextWidth("Text", 20), node.Style.Width);
            Assert.Equal(24 * expected, node.Style.Height, 4);
        });
    }

    /// <summary>Frame overrides register their faces while preserving configured role defaults.</summary>
    [Fact]
    public void DefaultsAndPerFrameOverridesAreRegisteredWithoutChangingConfiguredRoles()
    {
        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        var gui = new Gui { Input = new ScriptedInputHandler() };
        var text = Font.FromFile(FontRegistryTests.FontPath());
        var overrideFont = Font.FromFile(FontRegistryTests.FontPath("widget-icons.ttf"));
        gui.ConfigureFonts(text);
        Assert.Same(text, gui.Fonts.ResolveRole(FontRole.Ui));
        Assert.Equal("Roboto", gui.Fonts.Resolve("Roboto")!.FamilyName);
        gui.BeginFrame(surface.Canvas, overrideFont, overrideFont, overrideFont);
        Assert.Same(overrideFont, gui.CurrentNodeScope.Get<LayoutNodeScopeTextFont>().Value);
        Assert.Same(text, gui.Fonts.ResolveRole(FontRole.Ui));
        gui.EndFrame();
        gui.BeginFrame(surface.Canvas);
        Assert.Same(text, gui.CurrentNodeScope.Get<LayoutNodeScopeTextFont>().Value);
        gui.EndFrame();
    }

    /// <summary>Registry role family lists supply frame defaults.</summary>
    [Fact]
    public void RegistryRoleListsSupplyFrameDefaults()
    {
        using var frame = new FrameHarness();
        frame.Gui.Fonts.RegisterFile("Brand", FontRegistryTests.FontPath());
        frame.Gui.Fonts.SetRole(FontRole.Ui, "Brand, monospace");
        frame.Frame(gui => Assert.Equal("Roboto", gui.CurrentNodeScope.Get<LayoutNodeScopeTextFont>().Value.FamilyName));
    }

    /// <summary>An existing canvas scale is accounted for when applying display scaling.</summary>
    [Fact]
    public void ScaledCanvasesDoNotApplyDpiTwice()
    {
        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        var gui = new Gui { Input = new ScriptedInputHandler() };
        var display = Substitute.For<IDisplayCapability>();
        display.ScaleFactor.Returns(2);
        gui.Platform.Register(display);
        surface.Canvas.Scale(2);
        gui.BeginFrame(surface.Canvas);
        Assert.Equal(1, gui.FontScale);
        Assert.Equal(20, gui.GetTextFont(20).Size);
        gui.EndFrame();
    }

    /// <summary>Variation selectors remain invisible when drawing without shaping.</summary>
    [Fact]
    public void VariationSelectorsDoNotAddMissingGlyphs()
    {
        using var frame = new FrameHarness();
        frame.Frame(gui =>
        {
            var font = gui.GetTextFont();
            var runs = gui.CreateTextRuns("A\ufe0fB\U000e0100C", font, font);
            Assert.Equal("ABC", string.Concat(runs.Select(run => run.Text)));
            Assert.Empty(gui.CreateTextRuns("", font, font));
        });
    }

    /// <summary>Icon and emoji role fallback lists participate in text drawing.</summary>
    [Fact]
    public void IconAndEmojiRolesRetainTheirOwnFallbackLists()
    {
        using var frame = new FrameHarness();
        var fonts = frame.Gui.Fonts;
        fonts.RegisterFile("Text", FontRegistryTests.FontPath());
        fonts.RegisterFile("Widgets", FontRegistryTests.FontPath("widget-icons.ttf"));
        fonts.RegisterFile("Emoji", FontRegistryTests.FontPath("icons.ttf"));
        fonts.SetRole(FontRole.Icon, "Text, Widgets");
        fonts.SetRole(FontRole.Emoji, "Text, Emoji");
        var widgets = fonts.Resolve("Widgets")!;
        var emojiFace = fonts.Resolve("Emoji")!;
        var codePoint = Enumerable.Range(0x1f600, 80).First(code =>
            widgets.SkFont.GetGlyph(code) == 0 && emojiFace.SkFont.GetGlyph(code) != 0);
        var glyph = char.ConvertFromUtf32(codePoint);
        frame.Frame(gui =>
        {
            var font = fonts.Resolve("Text", 18)!;
            var emoji = gui.CurrentNodeScope.Get<LayoutNodeScopeIconFont>().Value.Resized(18);
            var runs = gui.CreateTextRuns("\uf007" + glyph, font, emoji);
            Assert.Equal(2, runs.Count);
            Assert.Contains("Font Awesome", runs[0].Font.FamilyName);
            Assert.NotEqual((ushort)0, runs[1].Font.SkFont.GetGlyph(codePoint));
        });
    }
}
