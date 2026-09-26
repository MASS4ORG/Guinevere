using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

public class FontFallbackTests
{
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
