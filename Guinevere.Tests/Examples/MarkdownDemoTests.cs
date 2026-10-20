using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Examples;

public class MarkdownDemoTests
{
    [Fact]
    public void PageStreamsTheSampleToItsFullDocument()
    {
        using var harness = new FrameHarness(800, 1200);
        static void Draw(Gui gui) => Example01.Shared.MarkdownDemo(gui, Example01.ColorPalette.Light);

        harness.Frame(Draw, 0.5);
        var partial = harness.Gui.TextLayoutBuilds;
        harness.Frame(Draw, 40.0);
        harness.Frame(Draw, 0.0);

        Assert.True(harness.Gui.TextLayoutBuilds > partial);
        Assert.NotEmpty(harness.Gui.RootNode!.Children);
        using var image = harness.Snapshot();
        using var pixmap = image.PeekPixels();
        Assert.Contains(pixmap.GetPixelSpan().ToArray().Chunk(4), pixel => pixel[3] > 0);
    }

    [Fact]
    public void BadgeImageResolvesAndOthersShowAltText()
    {
        Assert.NotNull(Example01.Shared.MarkdownImage("badge"));
        Assert.Null(Example01.Shared.MarkdownImage("other"));
    }
}
