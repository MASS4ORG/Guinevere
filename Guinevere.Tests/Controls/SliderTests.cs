using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the slider: clicking the track jumps the thumb, dragging keeps scrubbing beyond the widget,
/// steps snap to multiples, and the keyboard adjusts by one step once the slider has focus.
/// </summary>
public class SliderTests
{
    private static readonly Font TestFont = Font.FromFamilyName("serif", 12);

    private sealed class Harness
    {
        private readonly SKSurface surface = SKSurface.Create(new SKImageInfo(400, 60));
        private readonly IInputHandler input = Substitute.For<IInputHandler>();
        private readonly TestableGui gui;

        public Harness(float initial = 5f)
        {
            gui = new TestableGui { Input = input };
            gui.SetScreenRect(400, 60);
            Value = initial;

            input.MousePosition.Returns(new Vector2(-100, -100));
            input.PrevMousePosition.Returns(new Vector2(-100, -100));
            input.IsMouseButtonDown(Arg.Any<MouseButton>()).Returns(false);
            input.IsKeyPressed(Arg.Any<KeyboardKey>()).Returns(false);
        }

        public float Value { get; private set; }

        public void Frame(Vector2? mouse = null, bool pressed = false, bool down = false,
            KeyboardKey? key = null, float step = 0f, bool enabled = true)
        {
            input.MousePosition.Returns(mouse ?? new Vector2(-100, -100));
            input.PrevMousePosition.Returns(mouse ?? new Vector2(-100, -100));
            input.IsMouseButtonPressed(MouseButton.Left).Returns(pressed);
            input.IsMouseButtonDown(MouseButton.Left).Returns(down);
            input.IsKeyPressed(Arg.Any<KeyboardKey>())
                .Returns(call => key is not null && call.Arg<KeyboardKey>() == key);

            var value = Value;

            void Draw() => gui.Slider(ref value, 0f, 10f, step: step, width: 300, height: 30, enabled: enabled);

            gui.Time.Update(0.016);
            gui.SetStage(Pass.Pass1Build);
            gui.BeginFrame(surface.Canvas, TestFont);
            Draw();
            gui.CalculateLayout();
            gui.SetStage(Pass.Pass2Render);
            Draw();
            gui.Render();
            gui.EndFrame();

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
        var h = new Harness(5f);

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
        var h = new Harness(5f);

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
        var h = new Harness(5f);

        h.Frame(mouse: new Vector2(225, 15), pressed: true, down: true, enabled: false);
        h.Frame(mouse: new Vector2(225, 15), enabled: false);

        Assert.Equal(5f, h.Value, precision: 3);
    }
}