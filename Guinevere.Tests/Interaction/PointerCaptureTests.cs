using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Interaction;

/// <summary>
/// Covers the rule that one element owns the pointer for the length of a gesture. Without it, any
/// element the cursor crossed while a button was held started a drag of its own — dragging a dock tab
/// past a splitter moved the splitter.
/// </summary>
public class PointerCaptureTests
{
    const int Size = 200;

    /// <summary>Two side-by-side targets, "left" and "right", each 100 wide.</summary>
    sealed class TwoTargets
    {
        readonly SKSurface _surface = SKSurface.Create(new SKImageInfo(Size, Size));
        readonly IInputHandler _input = Substitute.For<IInputHandler>();
        readonly TestableGui _gui;

        public TwoTargets()
        {
            _gui = new TestableGui { Input = _input };
            _gui.SetScreenRect(Size, Size);
        }

        public bool LeftDragged { get; private set; }
        public bool RightDragged { get; private set; }
        public Gui Gui => _gui;

        /// <summary>Renames the element that took the capture, standing in for a dock tab that lands in
        /// another group and comes back under a different node id.</summary>
        public bool RenameLeft { get; set; }

        public void Frame(Vector2 mouse, bool down, bool pressed = false, MouseButton button = MouseButton.Left)
        {
            _input.MousePosition.Returns(mouse);
            _input.PrevMousePosition.Returns(mouse);
            _input.IsMouseButtonDown(button).Returns(down);
            _input.IsMouseButtonPressed(button).Returns(pressed);

            LeftDragged = false;
            RightDragged = false;

            void Draw()
            {
                using (_gui.Node(Size, Size, "row").Direction(Axis.Horizontal).Enter())
                {
                    using (_gui.Node(100, Size, RenameLeft ? "left-moved" : "left").Enter())
                        if (_gui.Pass == Pass.Pass2Render)
                            LeftDragged |= _gui.GetInteractable().OnDrag(out _, button);

                    using (_gui.Node(100, Size, "right").Enter())
                        if (_gui.Pass == Pass.Pass2Render)
                            RightDragged |= _gui.GetInteractable().OnDrag(out _, button);
                }
            }

            _gui.SetStage(Pass.Pass1Build);
            _gui.BeginFrame(_surface.Canvas);
            Draw();
            _gui.CalculateLayout();
            _gui.SetStage(Pass.Pass2Render);
            Draw();
            _gui.EndFrame();
        }
    }

    static readonly Vector2 OnLeft = new(50, 100);
    static readonly Vector2 OnRight = new(150, 100);

    [Fact]
    public void DraggingAcrossAnotherElementDoesNotHandOverThePointer()
    {
        var scene = new TwoTargets();

        scene.Frame(OnLeft, down: true, pressed: true);
        Assert.True(scene.LeftDragged);

        // Still holding, now over the other element — it must not start dragging too.
        scene.Frame(OnRight, down: true);

        Assert.True(scene.LeftDragged);
        Assert.False(scene.RightDragged);
    }

    [Fact]
    public void AHoldDoesNotBeginUnderAButtonThatWentDownElsewhere()
    {
        var scene = new TwoTargets();

        // Press outside both targets, then move onto one with the button still held.
        scene.Frame(new Vector2(100, 190), down: true, pressed: true);
        scene.Frame(OnRight, down: true);

        Assert.False(scene.RightDragged);
    }

    [Fact]
    public void ReleasingHandsThePointerBackSoTheNextPressIsFree()
    {
        var scene = new TwoTargets();

        scene.Frame(OnLeft, down: true, pressed: true);
        scene.Frame(OnRight, down: false);
        Assert.False(scene.Gui.IsPointerCaptured);

        scene.Frame(OnRight, down: true, pressed: true);

        Assert.True(scene.RightDragged);
        Assert.False(scene.LeftDragged);
    }

    [Fact]
    public void TheCapturingElementIsReportedWhileTheGestureLasts()
    {
        var scene = new TwoTargets();
        Assert.False(scene.Gui.IsPointerCaptured);

        scene.Frame(OnLeft, down: true, pressed: true);

        Assert.True(scene.Gui.IsPointerCaptured);
        Assert.EndsWith("left", scene.Gui.PointerCapture, StringComparison.Ordinal);
    }

    [Fact]
    public void AnotherButtonCannotTakeOverAGestureInProgress()
    {
        var scene = new TwoTargets();

        scene.Frame(OnLeft, down: true, pressed: true);
        Assert.True(scene.LeftDragged);

        // A right-press elsewhere while the left drag is live.
        scene.Frame(OnRight, down: true, pressed: true, button: MouseButton.Right);

        Assert.False(scene.RightDragged);
    }

    [Fact]
    public void ACaptureEndsOnTheButtonEvenIfItsElementIsGone()
    {
        var scene = new TwoTargets();

        scene.Frame(OnLeft, down: true, pressed: true);
        Assert.True(scene.Gui.IsPointerCaptured);

        // The element that held the pointer is re-created under a different id, so it never runs its
        // own release - which is what used to wedge the whole input system after one dock move.
        scene.RenameLeft = true;
        scene.Frame(OnLeft, down: false);

        Assert.False(scene.Gui.IsPointerCaptured);
    }

    [Fact]
    public void AnotherElementCanBeDraggedAfterTheCapturingOneDisappears()
    {
        var scene = new TwoTargets();

        scene.Frame(OnLeft, down: true, pressed: true);
        scene.RenameLeft = true;
        scene.Frame(OnLeft, down: false);

        scene.Frame(OnRight, down: true, pressed: true);

        Assert.True(scene.RightDragged);
    }
}
