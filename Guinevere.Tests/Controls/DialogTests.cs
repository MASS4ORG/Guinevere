using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the dialog's closing rules: Escape always closes it, a click outside only closes it when
/// asked to, and — mirroring the popup's rule — the press that opens a dialog never counts as the
/// click-outside that closes it again.
/// </summary>
public class DialogTests
{
    const int Width = 400;
    const int Height = 300;

    const float TitleBarHeight = 36;
    const float BodyPadding = 16;

    sealed class Harness
    {
        readonly SKSurface _surface = SKSurface.Create(new SKImageInfo(Width, Height));
        readonly IInputHandler _input = Substitute.For<IInputHandler>();
        readonly TestableGui _gui;

        public Harness()
        {
            _gui = new TestableGui { Input = _input };
            _gui.SetScreenRect(Width, Height);
            _input.GetTypedCharacters().Returns(string.Empty);
        }

        /// <summary>The dialog's top-left this frame, back-computed from its padded content area.</summary>
        public Vector2? Origin { get; private set; }

        /// <summary>Runs one frame with the dialog asked to be open, and reports whether it still is.</summary>
        public bool Frame(bool open, Vector2 mouse, bool pressed = false, bool down = false,
            bool escape = false, bool closeOnClickOutside = false, bool draggable = false)
        {
            _input.MousePosition.Returns(mouse);
            _input.PrevMousePosition.Returns(mouse);
            _input.IsMouseButtonPressed(Arg.Any<MouseButton>())
                .Returns(call => pressed && call.Arg<MouseButton>() == MouseButton.Left);
            _input.IsMouseButtonDown(Arg.Any<MouseButton>())
                .Returns(call => down && call.Arg<MouseButton>() == MouseButton.Left);
            _input.IsKeyPressed(Arg.Any<KeyboardKey>())
                .Returns(call => escape && call.Arg<KeyboardKey>() == KeyboardKey.Escape);

            var state = open;

            void Content()
            {
                if (_gui.Pass == Pass.Pass2Render)
                    Origin = _gui.CurrentNode.Rect.Position - new Vector2(BodyPadding, TitleBarHeight + BodyPadding);
            }

            // ReSharper disable once ExplicitCallerInfoArgument - one stable id across frames.
            void Draw() => _gui.Dialog(ref state, "Title", Content, 150, 100,
                closeOnClickOutside: closeOnClickOutside, draggable: draggable, titleBarHeight: TitleBarHeight,
                filePath: "dialog-test", lineNumber: 1);

            _gui.Time.Update(0.016);
            _gui.SetStage(Pass.Pass1Build);
            _gui.BeginFrame(_surface.Canvas, Font.FromFamilyName("serif"));
            Draw();
            _gui.CalculateLayout();
            _gui.SetStage(Pass.Pass2Render);
            Draw();
            _gui.Render();
            _gui.EndFrame();

            return state;
        }
    }

    static readonly Vector2 OutsideTheDialog = new(10, 10);

    /// <summary>Escape closes the dialog regardless of the click-outside setting.</summary>
    [Fact]
    public void EscapeClosesAnOpenDialog()
    {
        var h = new Harness();

        Assert.False(h.Frame(open: true, OutsideTheDialog, escape: true));
    }

    /// <summary>A dialog is a deliberate ask by default: clicking the dimmed overlay leaves it open.</summary>
    [Fact]
    public void AClickOutsideLeavesADialogOpenByDefault()
    {
        var h = new Harness();

        Assert.True(h.Frame(open: true, OutsideTheDialog, pressed: true));
    }

    /// <summary>Opting in, a click outside closes it — except on the frame it just opened.</summary>
    [Fact]
    public void AClickOutsideClosesADialogThatOptedIn()
    {
        var h = new Harness();

        h.Frame(open: true, OutsideTheDialog, pressed: true, closeOnClickOutside: true);

        Assert.False(h.Frame(open: true, OutsideTheDialog, pressed: true, closeOnClickOutside: true));
    }

    /// <summary>The click that opens a dialog does not also count as the click that closes it.</summary>
    [Fact]
    public void TheClickThatOpensADialogDoesNotAlsoCloseIt()
    {
        var h = new Harness();

        Assert.True(h.Frame(open: true, OutsideTheDialog, pressed: true, closeOnClickOutside: true));
    }

    /// <summary>Pressing the title bar and moving the pointer while held repositions the dialog.</summary>
    [Fact]
    public void DraggingTheTitleBarMovesTheDialog()
    {
        var h = new Harness();

        h.Frame(open: true, new Vector2(200, 150), draggable: true); // opens, centered
        var centered = h.Origin!.Value;

        h.Frame(open: true, new Vector2(150, 95), pressed: true, draggable: true); // press the title bar
        h.Frame(open: true, new Vector2(200, 150), down: true, draggable: true); // drag by (50, 55)
        h.Frame(open: true, new Vector2(200, 150), draggable: true); // release

        var dragged = h.Origin!.Value;
        Assert.Equal(centered.X + 50, dragged.X, 1);
        Assert.Equal(centered.Y + 55, dragged.Y, 1);
    }

    /// <summary>The close button's own corner of the title bar never starts a drag.</summary>
    [Fact]
    public void PressingTheCloseButtonCornerDoesNotStartADrag()
    {
        var h = new Harness();

        h.Frame(open: true, new Vector2(200, 150), draggable: true);
        var centered = h.Origin!.Value;

        // Inside the reserved close-button corner: the title bar's top-right titleBarHeight square.
        var closeCorner = new Vector2(centered.X + 150 - 10, centered.Y + 10);
        h.Frame(open: true, closeCorner, pressed: true, draggable: true);
        h.Frame(open: true, new Vector2(300, 250), down: true, draggable: true);
        h.Frame(open: true, new Vector2(300, 250), draggable: true);

        Assert.Equal(centered, h.Origin!.Value);
    }

    /// <summary>With dragging off, a press-and-move on the title bar leaves the dialog centered.</summary>
    [Fact]
    public void ADialogThatOptedOutOfDraggingStaysPut()
    {
        var h = new Harness();

        h.Frame(open: true, new Vector2(200, 150));
        var centered = h.Origin!.Value;

        h.Frame(open: true, new Vector2(150, 95), pressed: true);
        h.Frame(open: true, new Vector2(200, 150), down: true);
        h.Frame(open: true, new Vector2(200, 150));

        Assert.Equal(centered, h.Origin!.Value);
    }
}
