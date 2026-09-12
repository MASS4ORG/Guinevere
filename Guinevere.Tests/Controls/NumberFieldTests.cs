using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the numeric field's Unity-style scrub editing: dragging right/up increases, left/down
/// decreases from the value at the press, a plain click focuses for typing, Enter commits, invalid
/// text reverts, and everything stays clamped to the field's range.
/// </summary>
public class NumberFieldTests
{
    private static readonly Font TestFont = Font.FromFamilyName("serif", 12);

    private sealed class Harness
    {
        private readonly SKSurface surface = SKSurface.Create(new SKImageInfo(400, 60));
        private readonly IInputHandler input = Substitute.For<IInputHandler>();
        private readonly TestableGui gui;
        private readonly string id = $"num/{Guid.NewGuid():N}";

        public Harness(float initial = 42.5f)
        {
            gui = new TestableGui { Input = input };
            gui.SetScreenRect(400, 60);
            Value = initial;

            input.MousePosition.Returns(new Vector2(-100, -100));
            input.PrevMousePosition.Returns(new Vector2(-100, -100));
            input.GetTypedCharacters().Returns(string.Empty);
            input.IsMouseButtonDown(Arg.Any<MouseButton>()).Returns(false);
            input.IsKeyPressed(Arg.Any<KeyboardKey>()).Returns(false);
        }

        public float Value { get; private set; }

        public void Frame(Vector2? mouse = null, bool pressed = false, bool down = false,
            KeyboardKey? key = null, string typed = "", bool control = false)
        {
            input.MousePosition.Returns(mouse ?? new Vector2(-100, -100));
            input.PrevMousePosition.Returns(mouse ?? new Vector2(-100, -100));
            input.IsMouseButtonPressed(MouseButton.Left).Returns(pressed);
            input.IsMouseButtonDown(MouseButton.Left).Returns(down);
            input.IsKeyPressed(Arg.Any<KeyboardKey>())
                .Returns(call => key is not null && call.Arg<KeyboardKey>() == key);
            input.IsKeyDown(Arg.Any<KeyboardKey>()).Returns(call => call.Arg<KeyboardKey>() switch
            {
                KeyboardKey.LeftControl => control,
                _ => false
            });
            input.GetTypedCharacters().Returns(typed);

            var value = Value;

            void Draw() => gui.NumberField(ref value, step: 0.25f, min: 0f, max: 100f,
                width: 280, height: 24, fontSize: 12, id: id);

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
    public void DraggingRightIncreasesTheValue()
    {
        var h = new Harness(42.5f);

        h.Frame(mouse: new Vector2(140, 12), pressed: true, down: true);
        h.Frame(mouse: new Vector2(180, 12), down: true);
        h.Frame(mouse: new Vector2(180, 12)); // release

        // 40px right at 0.25 per pixel.
        Assert.Equal(52.5f, h.Value, precision: 3);
    }

    [Fact]
    public void DraggingUpIncreasesAndDownDecreases()
    {
        var h = new Harness(42.5f);

        h.Frame(mouse: new Vector2(140, 12), pressed: true, down: true);
        h.Frame(mouse: new Vector2(140, 2), down: true); // up 10px
        Assert.Equal(45f, h.Value, precision: 3);

        h.Frame(mouse: new Vector2(140, 22), down: true); // back down 10px from the press
        Assert.Equal(40f, h.Value, precision: 3);

        h.Frame(mouse: new Vector2(140, 22)); // release
        Assert.Equal(40f, h.Value, precision: 3);
    }

    [Fact]
    public void ScrubbingClampsToTheRange()
    {
        var h = new Harness(90f);

        h.Frame(mouse: new Vector2(140, 12), pressed: true, down: true);
        h.Frame(mouse: new Vector2(1000, 12), down: true);
        h.Frame(mouse: new Vector2(1000, 12));

        Assert.Equal(100f, h.Value, precision: 3);
    }

    [Fact]
    public void ClickFocusesForTypingAndEnterCommits()
    {
        var h = new Harness(42.5f);

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