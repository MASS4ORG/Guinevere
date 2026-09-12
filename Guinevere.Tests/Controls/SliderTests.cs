using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the slider: clicking the track jumps the thumb, dragging keeps scrubbing beyond the widget,
/// steps snap to multiples, and the keyboard adjusts by one step once the slider has focus.
/// </summary>
public class SliderTests
{
    private static readonly Font TestFont = Font.FromFamilyName("serif");

    private sealed class Harness
    {
        private readonly SKSurface _surface = SKSurface.Create(new SKImageInfo(400, 60));
        private readonly IInputHandler _input = Substitute.For<IInputHandler>();
        private readonly TestableGui _gui;

        public Harness(float initial = 5f)
        {
            _gui = new TestableGui { Input = _input };
            _gui.SetScreenRect(400, 60);
            Value = initial;

            _input.MousePosition.Returns(new Vector2(-100, -100));
            _input.PrevMousePosition.Returns(new Vector2(-100, -100));
            _input.IsMouseButtonDown(Arg.Any<MouseButton>()).Returns(false);
            _input.IsKeyPressed(Arg.Any<KeyboardKey>()).Returns(false);
        }

        public float Value { get; private set; }

        public void Frame(Vector2? mouse = null, bool pressed = false, bool down = false,
            KeyboardKey? key = null, float step = 0f, bool enabled = true)
        {
            _input.MousePosition.Returns(mouse ?? new Vector2(-100, -100));
            _input.PrevMousePosition.Returns(mouse ?? new Vector2(-100, -100));
            _input.IsMouseButtonPressed(MouseButton.Left).Returns(pressed);
            _input.IsMouseButtonDown(MouseButton.Left).Returns(down);
            _input.IsKeyPressed(Arg.Any<KeyboardKey>())
                .Returns(call => key is not null && call.Arg<KeyboardKey>() == key);

            var value = Value;

            void Draw() => _gui.Slider(ref value, 0f, 10f, step: step, width: 300, height: 30, enabled: enabled);

            _gui.Time.Update(0.016);
            _gui.SetStage(Pass.Pass1Build);
            _gui.BeginFrame(_surface.Canvas, TestFont);
            Draw();
            _gui.CalculateLayout();
            _gui.SetStage(Pass.Pass2Render);
            Draw();
            _gui.Render();
            _gui.EndFrame();

            Value = value;
        }
    }

    [Fact]
    public void ClickingOnTheTrackJumpsTheThumb()
    {
        var h = new Harness();

        h.Frame(mouse: new Vector2(225, 15), pressed: true, down: true);
        h.Frame(mouse: new Vector2(225, 15)); // release

        Assert.Equal(7.5f, h.Value, precision: 3);
    }

    [Fact]
    public void DraggingBeyondTheTrackKeepsScrubbing()
    {
        var h = new Harness();

        h.Frame(mouse: new Vector2(150, 15), pressed: true, down: true);
        h.Frame(mouse: new Vector2(1000, 15), down: true);
        h.Frame(mouse: new Vector2(1000, 15)); // release

        // The pointer is far off the right edge of the widget; the thumb still follows.
        Assert.Equal(10f, h.Value, precision: 3);
    }

    [Fact]
    public void StepSnapsToMultiples()
    {
        var h = new Harness(0f);

        h.Frame(mouse: new Vector2(219, 15), pressed: true, down: true, step: 0.5f);
        h.Frame(mouse: new Vector2(219, 15), step: 0.5f);

        // 219/300 of the 0..10 range is 7.3 which rounds to the nearest 0.5 multiple.
        Assert.Equal(7.5f, h.Value, precision: 3);
    }

    [Fact]
    public void ArrowsAdjustByOneStepWhenFocused()
    {
        var h = new Harness();

        h.Frame(mouse: new Vector2(150, 15), pressed: true, down: true);
        h.Frame(mouse: new Vector2(150, 15)); // release; focus lands this frame

        Assert.Equal(5f, h.Value, precision: 3);

        h.Frame(key: KeyboardKey.Right);
        Assert.Equal(6f, h.Value, precision: 3);

        h.Frame(key: KeyboardKey.Left);
        Assert.Equal(5f, h.Value, precision: 3);
    }

    [Fact]
    public void DisabledSliderIgnoresInput()
    {
        var h = new Harness();

        h.Frame(mouse: new Vector2(225, 15), pressed: true, down: true, enabled: false);
        h.Frame(mouse: new Vector2(225, 15), enabled: false);

        Assert.Equal(5f, h.Value, precision: 3);
    }
}
