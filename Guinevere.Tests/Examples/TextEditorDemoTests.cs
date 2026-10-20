using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Examples;

/// <summary>Checks the dockable Excalibur shader editor demo.</summary>
public class TextEditorDemoTests
{
    /// <summary>The demo renders the shader and publishes diagnostics and folding ranges.</summary>
    [Fact]
    public async Task PageEditsTheShaderAndLintsIt()
    {
        using var harness = new FrameHarness(900, 700);
        static void Draw(Gui gui) => Controls01.Program.TextEditorContent(gui);

        for (var i = 0; i < 40; i++)
        {
            harness.Frame(Draw, 0.1);
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        var editor = Controls01.Program.TextEditorDemoState!;
        Assert.Contains("sin(time * 2)", editor.Text);
        Assert.Equal("2", editor.Document.Buffer.GetText(editor.Diagnostics.Single().Start, 1));
        Assert.Contains(editor.FoldingRanges, range => range.StartLine == 7);
        using var image = harness.Snapshot();
        using var pixmap = image.PeekPixels();
        Assert.Contains(pixmap.GetPixelSpan().ToArray().Chunk(4), pixel => pixel[3] > 0);
        Assert.NotEmpty(harness.Gui.RootNode!.Children);
    }
}
