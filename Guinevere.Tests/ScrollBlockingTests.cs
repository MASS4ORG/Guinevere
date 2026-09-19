using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>
/// A scroll container behind a blocking overlay -- a modal dialog's dimmed background, say -- must
/// not respond to the wheel. <see cref="LayoutNode.BlockInput"/> only ever covered hover-based
/// interactions; the wheel handler read <see cref="IInputHandler.MouseWheelDelta"/> off a raw
/// rect-contains check with no awareness of what was drawn on top, so scrolling a dialog also
/// scrolled whatever docked panel happened to sit under the cursor behind it.
/// </summary>
public class ScrollBlockingTests
{
    const int Width = 200;
    const int Height = 200;

    static Gui RunFrame(bool withBlockingOverlay, float wheelDelta, out string scrollerId)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));

        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(100, 100));
        input.PrevMousePosition.Returns(new Vector2(100, 100));
        input.MouseWheelDelta.Returns(wheelDelta);

        var gui = new TestableGui { Input = input };
        gui.SetScreenRect(Width, Height);
        const string id = "background/scroller";
        scrollerId = id;

        void Draw()
        {
            using (gui.Node(Width, Height, id).Enter())
            {
                gui.ScrollY();
                using (gui.Node(Width, Height * 3).Enter()) { }
            }

            if (!withBlockingOverlay) return;

            var overlay = gui.Node(Width, Height, "overlay").AbsoluteScreen(0, 0).BlockInput();
            using (overlay.Enter()) { }
        }

        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        Draw();
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        Draw();
        gui.Render();
        gui.EndFrame();

        return gui;
    }

    // Negative: scrolling "down" from the top, so a clamp-to-zero can't be mistaken for a block.
    const float ScrollDown = -5f;

    [Fact]
    public void AWheelOverABlockingOverlayDoesNotScrollWhatIsBehindIt()
    {
        var gui = RunFrame(withBlockingOverlay: true, wheelDelta: ScrollDown, out var scrollerId);

        Assert.Equal(0f, gui.GetScrollState(scrollerId)!.ScrollOffset.Y);
    }

    /// <summary>Control: with nothing blocking it, the same wheel input does move the scroller.</summary>
    [Fact]
    public void AWheelWithNoBlockerScrollsNormally()
    {
        var gui = RunFrame(withBlockingOverlay: false, wheelDelta: ScrollDown, out var scrollerId);

        Assert.NotEqual(0f, gui.GetScrollState(scrollerId)!.ScrollOffset.Y);
    }
}
