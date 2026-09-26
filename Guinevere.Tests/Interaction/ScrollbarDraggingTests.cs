using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Interaction;

public class ScrollbarDraggingTests
{
    [Theory]
    [InlineData(Axis.Vertical)]
    [InlineData(Axis.Horizontal)]
    public void DraggingThumbMovesTheScrollOffsetAndStopsOnRelease(Axis axis)
    {
        using var harness = new FrameHarness(200, 200);
        void Draw(Gui gui)
        {
            using (gui.Node(100, 100, "scroll").Enter())
            {
                gui.ScrollContainer(scrollX: true, scrollY: true);
                using (gui.Node(300, 300).Enter()) { }
            }
        }

        harness.Frame(Draw);
        var state = Assert.IsType<ScrollState>(harness.Gui.GetScrollState("scroll"));
        var node = harness.Gui.RootNode!.FindChildById("scroll")!;
        Assert.True(state.ShowScrollbarX);
        Assert.True(state.ShowScrollbarY);
        var thumb = axis == Axis.Vertical
            ? state.CalculateVerticalScrollbar(node.Rect).thumb
            : state.CalculateHorizontalScrollbar(node.Rect).thumb;
        var start = new Vector2(thumb.X + thumb.W * 0.5f, thumb.Y + thumb.H * 0.5f);

        harness.Input.MoveTo(start);
        harness.Input.PressButton();
        harness.Frame(Draw);
        harness.Input.MoveTo(start + (axis == Axis.Vertical ? new Vector2(0, 25) : new Vector2(25, 0)));
        harness.Frame(Draw);

        Assert.True(axis == Axis.Vertical ? state.ScrollOffset.Y > 0 : state.ScrollOffset.X > 0);

        harness.Input.ReleaseButton();
        harness.Frame(Draw);
        Assert.False(state.IsDraggingScrollbarX);
        Assert.False(state.IsDraggingScrollbarY);
    }
}
