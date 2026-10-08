using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Styling;

/// <summary>Checks stylesheet rendering for drawn parts without additional layout nodes.</summary>
public class StyleDrawingTests
{
    /// <summary>A part draws the same box properties as a styled node, with opacity applied to the whole box.</summary>
    [Theory]
    [InlineData("1")]
    [InlineData("0.5")]
    [InlineData("0")]
    public void DrawnBoxMatchesStyledNode(string opacity)
    {
        using var harness = new FrameHarness(100, 60);
        harness.Gui.StyleSheets.Add(StyleSheet.Parse($$"""
            box { width = 30; height = 30; border-radius = 5 0 5 0; border-width = 2; border-color = #0000ff;
                background = linear-gradient(to right, #ff0000, #00ff00); opacity = {{opacity}};
                outline = 2px solid #000000; outline-offset = 2; box-shadow = 0 2px 3px #00000080; }
            """));
        harness.Frame(g =>
        {
            g.DrawBackgroundRect(Color.White);
            using (g.Node(100, 60).Padding(10).Direction(Axis.Horizontal).Gap(20).Enter())
            {
                using (g.StyledNode("box").Enter()) { }
                using (g.Node(30, 30).Enter()) g.DrawStyledBox(g.ResolveStyle("box"), g.CurrentNode.Rect);
            }
        });
        using var image = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(image);
        for (var y = 5; y < 45; y++)
            for (var x = 5; x < 45; x++)
                Assert.Equal(pixels.GetPixel(x, y), pixels.GetPixel(x + 50, y));
        var container = Assert.Single(harness.Gui.RootNode!.Children);
        Assert.Equal(2, container.Children.Count);
    }

    /// <summary>Empty geometry and styles leave the node's draw list empty in both passes.</summary>
    [Theory]
    [InlineData(0f, 10f)]
    [InlineData(10f, 0f)]
    [InlineData(10f, 10f)]
    public void EmptyPartsDoNotQueueDrawing(float width, float height)
    {
        using var harness = new FrameHarness(100, 60);
        harness.Frame(g =>
        {
            using (g.Node(30, 30).Enter())
            {
                g.DrawStyledBox(ResolvedStyle.Empty, new Rect(0, 0, width, height));
                Assert.Equal(0, g.CurrentNode.DrawList.Count);
            }
        });
    }
}
