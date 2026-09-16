using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers numeric text editing, including the fact that dragging the text field does not change the
/// value, Enter commits, invalid text reverts, and committed values stay clamped to the range.
/// </summary>
public class NumberFieldTests
{
    private static readonly Font TestFont = Font.FromFamilyName("serif");

    private sealed class Harness
    {
        private readonly SKSurface _surface = SKSurface.Create(new SKImageInfo(400, 60));
        private readonly IInputHandler _input = Substitute.For<IInputHandler>();
        private readonly TestableGui _gui;
        private readonly string _id = $"num/{Guid.NewGuid():N}";

        public Harness(float initial = 42.5f)
        {
            _gui = new TestableGui { Input = _input };
            _gui.SetScreenRect(400, 60);
            Value = initial;

            _input.MousePosition.Returns(new Vector2(-100, -100));
            _input.PrevMousePosition.Returns(new Vector2(-100, -100));
            _input.GetTypedCharacters().Returns(string.Empty);
            _input.IsMouseButtonDown(Arg.Any<MouseButton>()).Returns(false);
            _input.IsKeyPressed(Arg.Any<KeyboardKey>()).Returns(false);
        }

        public float Value { get; private set; }

        public void Frame(Vector2? mouse = null, bool pressed = false, bool down = false,
            KeyboardKey? key = null, string typed = "", bool control = false)
        {
            _input.MousePosition.Returns(mouse ?? new Vector2(-100, -100));
            _input.PrevMousePosition.Returns(mouse ?? new Vector2(-100, -100));
            _input.IsMouseButtonPressed(MouseButton.Left).Returns(pressed);
            _input.IsMouseButtonDown(MouseButton.Left).Returns(down);
            _input.IsKeyPressed(Arg.Any<KeyboardKey>())
                .Returns(call => key is not null && call.Arg<KeyboardKey>() == key);
            _input.IsKeyDown(Arg.Any<KeyboardKey>()).Returns(call => call.Arg<KeyboardKey>() switch
            {
                KeyboardKey.LeftControl => control,
                _ => false
            });
            _input.GetTypedCharacters().Returns(typed);

            var value = Value;

            void Draw() => _gui.NumberField(ref value, step: 0.25f, min: 0f, max: 100f,
                width: 280, height: 24, fontSize: 12, id: _id);

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
    public void DraggingDoesNotChangeTheValue()
    {
        var h = new Harness();

        h.Frame(mouse: new Vector2(140, 12), pressed: true, down: true);
        h.Frame(mouse: new Vector2(180, 12), down: true);
        h.Frame(mouse: new Vector2(180, 12)); // release

        Assert.Equal(42.5f, h.Value, precision: 3);
    }

    [Fact]
    public void ClickFocusesForTypingAndEnterCommits()
    {
        var h = new Harness();

        h.Frame(mouse: new Vector2(140, 12), pressed: true, down: true);
        h.Frame(mouse: new Vector2(140, 12)); // release without moving: a click
        h.Frame(key: KeyboardKey.A, control: true); // select all so typing replaces the old value
        h.Frame(typed: "50");
        h.Frame(key: KeyboardKey.Enter);

        Assert.Equal(50f, h.Value, precision: 3);
    }

    [Fact]
    public void InvalidTextRevertsToThePreviousValue()
    {
        var h = new Harness(50f);

        h.Frame(mouse: new Vector2(140, 12), pressed: true, down: true);
        h.Frame(mouse: new Vector2(140, 12));
        h.Frame(key: KeyboardKey.A, control: true);
        h.Frame(typed: "abc");
        h.Frame(key: KeyboardKey.Enter);

        Assert.Equal(50f, h.Value, precision: 3);
    }

    [Fact]
    public void CommittedTextIsClampedToTheRange()
    {
        var h = new Harness(10f);

        h.Frame(mouse: new Vector2(140, 12), pressed: true, down: true);
        h.Frame(mouse: new Vector2(140, 12));
        h.Frame(key: KeyboardKey.A, control: true);
        h.Frame(typed: "2500");
        h.Frame(key: KeyboardKey.Enter);

        Assert.Equal(100f, h.Value, precision: 3);
    }
}