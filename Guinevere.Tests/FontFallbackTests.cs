using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

public class FontFallbackTests
{
    [Fact]
    public void ConfiguredFontsPersistAcrossFramesAndAllowPerFrameOverride()
    {
        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        var text = Font.FromFamilyName("sans-serif");
        var emoji = Font.FromFamilyName("serif");
        var widget = Font.FromFamilyName("monospace");
        var gui = new Gui { Input = new ScriptedInputHandler() };
        gui.ConfigureFonts(text, emoji, widget);

        gui.BeginFrame(surface.Canvas);
        Assert.Same(text, gui.CurrentNodeScope.Get<LayoutNodeScopeTextFont>().Value);
        Assert.Same(emoji, gui.CurrentNodeScope.Get<LayoutNodeScopeIconFont>().Value);
        Assert.Same(widget, gui.CurrentNodeScope.Get<LayoutNodeScopeWidgetIconFont>().Value);
        gui.EndFrame();

        gui.BeginFrame(surface.Canvas, fontIcon: widget);
        Assert.Same(widget, gui.CurrentNodeScope.Get<LayoutNodeScopeIconFont>().Value);
        gui.EndFrame();

        gui.BeginFrame(surface.Canvas);
        Assert.Same(emoji, gui.CurrentNodeScope.Get<LayoutNodeScopeIconFont>().Value);
        gui.EndFrame();
        text.Dispose();
        emoji.Dispose();
        widget.Dispose();
    }

    [Fact]
    public void ConfiguredTextFontIsTheIconFallbackWhenIconsAreOmitted()
    {
        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        var text = Font.FromFamilyName("sans-serif");
        var gui = new Gui { Input = new ScriptedInputHandler() };
        gui.ConfigureFonts(text);

        gui.BeginFrame(surface.Canvas);
        Assert.Same(text, gui.CurrentNodeScope.Get<LayoutNodeScopeIconFont>().Value);
        Assert.Same(text, gui.CurrentNodeScope.Get<LayoutNodeScopeWidgetIconFont>().Value);
        gui.EndFrame();
        text.Dispose();
    }

    [Fact]
    public void Text_UsesWidgetFontForPrivateUseGlyphs()
    {
        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        var fonts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../Integrations/Resources/Fonts"));
        var text = Font.FromFile(Path.Combine(fonts, "font.ttf"));
        var emoji = Font.FromFile(Path.Combine(fonts, "icons.ttf"));
        var widget = Font.FromFile(Path.Combine(fonts, "widget-icons.ttf"));
        var gui = new Gui { Input = new ScriptedInputHandler() };
        gui.BeginFrame(surface.Canvas, text, emoji, widget);

        var runs = gui.CreateTextRuns("A\uf007", text, emoji);

        Assert.Equal(2, runs.Count);
        Assert.Equal("Roboto", runs[0].Font.SkFont.Typeface.FamilyName);
        Assert.Contains("Font Awesome", runs[1].Font.SkFont.Typeface.FamilyName, StringComparison.Ordinal);
        gui.EndFrame();
        text.Dispose();
        emoji.Dispose();
        widget.Dispose();
    }
}
