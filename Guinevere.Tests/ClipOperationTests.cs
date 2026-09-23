using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>Clip operations: rectangles, empty rectangles, shapes, and scrolling containers' viewports.</summary>
public class ClipOperationTests
{
    static (FrameHarness Harness, LayoutNode Node) Scene(bool scrolling)
    {
        var harness = new FrameHarness();
        void Draw(Gui gui)
        {
            using (gui.Node(100, 80, "box").Enter())
            {
                if (scrolling) gui.ScrollY();
                using (gui.Node(100, 400, "content").Enter()) { }
            }
        }

        harness.Frame(Draw);
        harness.Frame(Draw);
        return (harness, harness.Gui.RootNode!.Children[0]);
    }

    static SKRectI ClipAfter(ClipOperation operation, Gui gui, LayoutNode node)
    {
        using var surface = SKSurface.Create(new SKImageInfo(400, 300));
        operation.Execute(gui, node, surface.Canvas);
        return surface.Canvas.DeviceClipBounds;
    }

    [Fact]
    public void RectangleClipsToItself()
    {
        var (harness, node) = Scene(scrolling: false);
        using var _ = harness;

        var bounds = ClipAfter(new ClipOperation(new Rect(10, 20, 30, 40)), harness.Gui, node);

        Assert.Equal(new SKRectI(10, 20, 40, 60), bounds);
    }

    [Fact]
    public void EmptyRectangleDoesNotClip()
    {
        var (harness, node) = Scene(scrolling: false);
        using var _ = harness;

        var bounds = ClipAfter(new ClipOperation(new Rect(10, 20, 0, 40)), harness.Gui, node);

        Assert.Equal(400, bounds.Width);
    }

    [Fact]
    public void ScrollingContainerClipsToItsViewport()
    {
        var (harness, node) = Scene(scrolling: true);
        using var _ = harness;

        var bounds = ClipAfter(new ClipOperation(new Rect(0, 0, 100, 400)), harness.Gui, node);

        Assert.Equal((int)node.Rect.H, bounds.Height);
    }

    [Fact]
    public void ShapeClipsToItsPathAtThePosition()
    {
        var (harness, node) = Scene(scrolling: false);
        using var _ = harness;

        var bounds = ClipAfter(new ClipOperation(Shape.Circle(10), new Vector2(50, 50)), harness.Gui, node);

        Assert.InRange(bounds.Width, 20, 21);
        Assert.InRange(bounds.MidX, 49, 51);
    }
}
