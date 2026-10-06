using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers numeric text editing, including the fact that dragging the text field does not change the
/// value, Enter commits, invalid text reverts, and committed values stay clamped to the range.
/// </summary>
public class NumberFieldTests
{
    static readonly Font TestFont = Font.FromFamilyName("serif");

    sealed class Harness
    {
        readonly SKSurface _surface = SKSurface.Create(new SKImageInfo(400, 60));
        readonly IInputHandler _input = Substitute.For<IInputHandler>();
        readonly TestableGui _gui;
        readonly string _id = $"num/{Guid.NewGuid():N}";

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
        public bool Mixed { get; set; }
        public bool Committed { get; private set; }

        /// <summary>Updates the bound value as a label scrub or another editor would.</summary>
        public void SetExternal(float value) => Value = value;

        public void Frame(Vector2? mouse = null, bool pressed = false, bool down = false,
            KeyboardKey? key = null, string typed = "", bool control = false, bool shift = false)
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
                KeyboardKey.LeftShift => shift,
                _ => false
            });
            _input.GetTypedCharacters().Returns(typed);

            var value = Value;

            void Draw()
            {
                Committed = _gui.NumberField(ref value, step: 0.25f, min: 0f, max: 100f,
                    width: 280, height: 24, fontSize: 12, id: _id, mixed: Mixed);
                _gui.Button("Next", 60, 24);
            }

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

    /// <summary>Scrubbing the bound value while focused cannot commit the stale edit buffer on focus loss.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ExternalChangeSurvivesTab(bool shift, bool selected)
    {
        var h = new Harness();
        h.Frame();
        h.Frame(mouse: new Vector2(140, 12), pressed: true, down: true);
        h.Frame(mouse: new Vector2(140, 12));
        h.Frame();
        if (selected) h.Frame(key: KeyboardKey.A, control: true);
        h.SetExternal(65f);
        h.Frame();
        h.Frame(key: KeyboardKey.Tab, shift: shift);
        h.Frame();
        Assert.Equal(65f, h.Value);
    }

    /// <summary>Uncommitted typing is retained when the caller's numeric value has not changed.</summary>
    [Fact]
    public void TypingSurvivesFramesAndRepeatedClicks()
    {
        var h = new Harness();
        h.Frame();
        h.Frame(mouse: new Vector2(140, 12), pressed: true, down: true);
        h.Frame(mouse: new Vector2(140, 12));
        h.Frame(key: KeyboardKey.A, control: true);
        h.Frame(typed: "53");
        h.Frame();
        h.Frame(mouse: new Vector2(140, 12), pressed: true, down: true);
        h.Frame(mouse: new Vector2(140, 12));
        h.Frame(key: KeyboardKey.Enter);
        Assert.Equal(53f, h.Value);
    }

    /// <summary>Entering the first owner's value in a mixed field still reports an explicit edit.</summary>
    [Fact]
    public void MixedNumberCommitsEvenWhenValueIsUnchanged()
    {
        var h = new Harness(10f) { Mixed = true };
        h.Frame();
        Assert.False(h.Committed);
        h.Frame(mouse: new Vector2(140, 12), pressed: true, down: true);
        h.Frame(mouse: new Vector2(140, 12));
        h.Frame(typed: "10");
        h.Frame(key: KeyboardKey.Enter);
        Assert.Equal(10f, h.Value);
        Assert.True(h.Committed);
        h.Mixed = false;
        h.Frame();
        Assert.False(h.Committed);
    }

    /// <summary>Invalid or untouched mixed inputs retain their value without reporting an edit.</summary>
    [Fact]
    public void MixedNumberDoesNotCommitPlaceholderOrInvalidInput()
    {
        var h = new Harness(10f) { Mixed = true };
        h.Frame();
        h.Frame(mouse: new Vector2(140, 12), pressed: true, down: true);
        h.Frame(mouse: new Vector2(140, 12));
        h.Frame(key: KeyboardKey.Enter);
        Assert.False(h.Committed);
        h.Frame();
        h.Frame(mouse: new Vector2(140, 12), pressed: true, down: true);
        h.Frame(mouse: new Vector2(140, 12));
        h.Frame(key: KeyboardKey.A, control: true);
        h.Frame(typed: "NaN");
        h.Frame(key: KeyboardKey.Enter);
        Assert.Equal(10f, h.Value);
        Assert.False(h.Committed);
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
