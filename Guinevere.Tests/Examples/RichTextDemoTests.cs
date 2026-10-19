using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Examples;

public class RichTextDemoTests
{
    [Fact]
    public void PageDrawsAndReusesItsLayoutsAcrossFrames()
    {
        using var harness = new FrameHarness(800, 900);
        static void Draw(Gui gui) => Example01.Shared.RichTextDemo(gui, Example01.ColorPalette.Dark);

        harness.Frame(Draw);
        var builds = harness.Gui.TextLayoutBuilds;
        harness.Frame(Draw);

        Assert.Equal(builds, harness.Gui.TextLayoutBuilds);
        Assert.Equal("none", Example01.Shared.RichTextLinks.Hovered);
    }

    [Fact]
    public void HighlighterStylesKeywordsStringsNumbersCommentsAndTypes()
    {
        var p = Example01.ColorPalette.Dark;
        const string Source = "// note\npublic Greeter x = \"hi\" + 42;";

        var ranges = Example01.Shared.Highlight(Source, p).Content.Ranges;
        Color? ColorAt(int offset) => ranges.Single(range => range.Start <= offset && offset < range.End).Style.Color;

        Assert.Equal(p.TextDim, ColorAt(0));
        Assert.Equal(p.Accent, ColorAt(Source.IndexOf("public", StringComparison.Ordinal)));
        Assert.Equal(p.Pick(4), ColorAt(Source.IndexOf("Greeter", StringComparison.Ordinal)));
        Assert.Null(ColorAt(Source.IndexOf(" x", StringComparison.Ordinal) + 1));
        Assert.Equal(p.Positive, ColorAt(Source.IndexOf('"')));
        Assert.Equal(p.Warning, ColorAt(Source.IndexOf("42", StringComparison.Ordinal)));
    }
}
