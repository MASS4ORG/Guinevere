using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the popup's open and close rules, in particular that the press which opens one does not
/// immediately count as the click-outside that closes it again.
/// </summary>
public class PopupTests
{
    private const int Width = 400;
    private const int Height = 300;

    private static readonly Font TestFont = Font.FromFamilyName("serif");

    private sealed class Harness
    {
        private readonly SKSurface _surface = SKSurface.Create(new SKImageInfo(Width, Height));
        private readonly IInputHandler _input = Substitute.For<IInputHandler>();
        private readonly TestableGui _gui;

        public Harness()
        {
            _gui = new TestableGui { Input = _input };
            _gui.SetScreenRect(Width, Height);
            _input.GetTypedCharacters().Returns(string.Empty);
            _input.IsKeyPressed(Arg.Any<KeyboardKey>()).Returns(false);
        }

        /// <summary>Runs one frame with the popup asked to be open, and reports whether it still is.</summary>
        public bool Frame(bool open, Vector2 mouse, bool pressed = false)
        {
            _input.MousePosition.Returns(mouse);
            _input.PrevMousePosition.Returns(mouse);
            _input.IsMouseButtonPressed(Arg.Any<MouseButton>())
                .Returns(call => pressed && call.Arg<MouseButton>() == MouseButton.Left);
            _input.IsMouseButtonDown(Arg.Any<MouseButton>()).Returns(false);

            var state = open;

            // ReSharper disable once ExplicitCallerInfoArgument - one stable id across frames.
            void Draw() => _gui.Popup(ref state, () => { }, 150, 100,
                position: new Vector2(10, 10), filePath: "popup-test", lineNumber: 1);

            _gui.Time.Update(0.016);
            _gui.SetStage(Pass.Pass1Build);
            _gui.BeginFrame(_surface.Canvas, TestFont);
            Draw();
            _gui.CalculateLayout();
            _gui.SetStage(Pass.Pass2Render);
            Draw();
            _gui.Render();
            _gui.EndFrame();

            return state;
        }
    }

    /// <summary>
    /// The button that opens a popup is outside it, so the opening press must not close it — otherwise
    /// the popup opens and shuts within one frame and never appears.
    /// </summary>
    [Fact]
    public void TheClickThatOpensAPopupDoesNotAlsoCloseIt()
    {
        var h = new Harness();
        var onTheButton = new Vector2(300, 250);

        Assert.True(h.Frame(open: true, onTheButton, pressed: true));
    }

    /// <summary>A later press outside still closes it.</summary>
    [Fact]
    public void APressOutsideClosesAnOpenPopup()
    {
        var h = new Harness();
        var outside = new Vector2(300, 250);

        h.Frame(open: true, outside, pressed: true);

        Assert.False(h.Frame(open: true, outside, pressed: true));
    }

    /// <summary>A press inside leaves it open, which is how its own content is clicked.</summary>
    [Fact]
    public void APressInsideLeavesAPopupOpen()
    {
        var h = new Harness();

        h.Frame(open: true, new Vector2(300, 250), pressed: true);

        Assert.True(h.Frame(open: true, new Vector2(50, 50), pressed: true));
    }
}
