namespace Guinevere.Tests.Interaction;

/// <summary>
/// Covers <see cref="LayoutNode.BlockInput"/>. Hit-testing is a bare point-in-rect check, so before
/// arbitration existed every overlapping element reported a hover at once and an overlay could not
/// stop the control beneath it from reacting.
/// </summary>
public class InputArbitrationTests
{
    private static (Gui Gui, IInputHandler Input) NewFrame(Vector2 mouse, int width = 200, int height = 200)
    {
        var surface = SKSurface.Create(new SKImageInfo(width, height));
        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(mouse);
        input.PrevMousePosition.Returns(mouse);

        var gui = new Gui { Input = input };
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        return (gui, input);
    }

    private static (bool Under, bool Overlay, bool OverlayChild) RunOverlayFrame(Vector2 mouse, bool block)
    {
        var (gui, _) = NewFrame(mouse);
        bool under = false, overlay = false, overlayChild = false;

        void Draw()
        {
            using (gui.Node(200, 200, "under").Enter())
            {
                if (gui.Pass == Pass.Pass2Render) under = gui.GetInteractable().OnHover();
            }

            using (gui.Node(100, 100, "overlay").AbsoluteScreen(0, 0).BlockInput(block).Enter())
            {
                gui.SetZIndex(100);
                if (gui.Pass == Pass.Pass2Render) overlay = gui.GetInteractable().OnHover();

                using (gui.Node(50, 50, "overlayChild").Enter())
                {
                    if (gui.Pass == Pass.Pass2Render) overlayChild = gui.GetInteractable().OnHover();
                }
            }
        }

        Draw();
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        Draw();
        gui.EndFrame();

        return (under, overlay, overlayChild);
    }

    [Fact]
    public void OverlappingElementsBothHoverWhenNobodyBlocks()
    {
        var (under, overlay, _) = RunOverlayFrame(new Vector2(20, 20), block: false);

        Assert.True(under);
        Assert.True(overlay);
    }

    [Fact]
    public void BlockingOverlaySuppressesTheNodeBeneathIt()
    {
        var (under, overlay, overlayChild) = RunOverlayFrame(new Vector2(20, 20), block: true);

        Assert.False(under);
        Assert.True(overlay);
        Assert.True(overlayChild);
    }

    [Fact]
    public void BlockingOverlayOnlyBlocksWhileTheCursorIsInsideIt()
    {
        var (under, overlay, _) = RunOverlayFrame(new Vector2(150, 150), block: true);

        Assert.True(under);
        Assert.False(overlay);
    }

    [Fact]
    public void BlockerSubtreeStaysInteractiveEvenWithExplicitNodeIds()
    {
        // Descendants are found by walking the parent chain, so a child given an id of its own -
        // which docking does, to keep panel state stable across re-layouts - is still exempt.
        var (gui, _) = NewFrame(new Vector2(20, 20));
        bool under = false, named = false;

        void Draw()
        {
            using (gui.Node(200, 200, "under").Enter())
            {
                if (gui.Pass == Pass.Pass2Render) under = gui.GetInteractable().OnHover();
            }

            using (gui.Node(100, 100, "overlay").AbsoluteScreen(0, 0).BlockInput().Enter())
            {
                gui.SetZIndex(100);
                using (gui.Node(50, 50, "dock:panel/inspector").Enter())
                {
                    if (gui.Pass == Pass.Pass2Render) named = gui.GetInteractable().OnHover();
                }
            }
        }

        Draw();
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        Draw();
        gui.EndFrame();

        Assert.False(under);
        Assert.True(named);
    }

    [Fact]
    public void DragReportsThePressOriginRatherThanTheLastFramePosition()
    {
        var press = new Vector2(30, 30);
        var moved = new Vector2(70, 45);

        var surface = SKSurface.Create(new SKImageInfo(200, 200));
        var input = Substitute.For<IInputHandler>();
        var gui = new Gui { Input = input };

        DragArgs args = default;

        void Frame(Vector2 mouse, Vector2 prev, bool down, bool pressed = false)
        {
            input.MousePosition.Returns(mouse);
            input.PrevMousePosition.Returns(prev);
            input.IsMouseButtonDown(MouseButton.Left).Returns(down);
            input.IsMouseButtonPressed(MouseButton.Left).Returns(pressed);

            void Draw()
            {
                using (gui.Node(200, 200, "surface").Enter())
                {
                    if (gui.Pass == Pass.Pass2Render) gui.GetInteractable().OnDrag(out args);
                }
            }

            gui.SetStage(Pass.Pass1Build);
            gui.BeginFrame(surface.Canvas);
            Draw();
            gui.CalculateLayout();
            gui.SetStage(Pass.Pass2Render);
            Draw();
            gui.EndFrame();
        }

        Frame(press, press, down: true, pressed: true);
        Assert.Equal(press, args.Origin);

        Frame(moved, press, down: true);
        Assert.Equal(press, args.Origin);
        Assert.Equal(moved - press, args.TotalDelta);
        Assert.Equal(moved - press, args.FrameDelta);
    }

    /// <summary>
    /// Runs a frame with a small child inside a big parent, both asking whether they are hovered.
    /// This is the tree-row-and-expander shape: the child must be able to take the pointer from the
    /// row it sits in.
    /// </summary>
    private static (bool Parent, bool Child) RunNestedFrame(Vector2 mouse, bool childBlocks)
    {
        var (gui, _) = NewFrame(mouse);
        bool parent = false, child = false;

        void Draw()
        {
            using (gui.Node(200, 200, "row").Enter())
            {
                if (gui.Pass == Pass.Pass2Render) parent = gui.GetInteractable().OnHover();

                using (gui.Node(20, 20, "row/expander").BlockInput(childBlocks).Enter())
                {
                    if (gui.Pass == Pass.Pass2Render) child = gui.GetInteractable().OnHover();
                }
            }
        }

        Draw();
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        Draw();
        gui.EndFrame();

        return (parent, child);
    }

    [Fact]
    public void ABlockingChildTakesThePointerFromItsParent()
    {
        var (parent, child) = RunNestedFrame(new Vector2(10, 10), childBlocks: true);

        Assert.False(parent);
        Assert.True(child);
    }

    [Fact]
    public void WithoutBlockingBothTheChildAndItsParentReportTheHover()
    {
        // The default, and why a tree row and its expander both used to act on one click.
        var (parent, child) = RunNestedFrame(new Vector2(10, 10), childBlocks: false);

        Assert.True(parent);
        Assert.True(child);
    }

    [Fact]
    public void AParentStillReactsEverywhereOutsideItsBlockingChild()
    {
        var (parent, child) = RunNestedFrame(new Vector2(120, 120), childBlocks: true);

        Assert.True(parent);
        Assert.False(child);
    }
}
