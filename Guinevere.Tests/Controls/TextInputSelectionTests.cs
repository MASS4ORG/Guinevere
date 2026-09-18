using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the text field's selection: extending it with the keyboard, replacing it by typing, and the
/// clipboard acting on it rather than on the whole value.
/// </summary>
public class TextInputSelectionTests
{
    /// <summary>A non-default typeface on purpose: caret maths measured with the wrong one drifts.</summary>
    static readonly Font TestFont = Font.FromFamilyName("serif");

    sealed class Field
    {
        readonly SKSurface _surface = SKSurface.Create(new SKImageInfo(300, 60));
        readonly IInputHandler _input = Substitute.For<IInputHandler>();
        readonly TestableGui _gui;
        readonly string _id = $"field/{Guid.NewGuid():N}";

        public Field(string initial)
        {
            _gui = new TestableGui { Input = _input };
            _gui.SetScreenRect(300, 60);
            Text = initial;

            _input.MousePosition.Returns(new Vector2(-100, -100));
            _input.PrevMousePosition.Returns(new Vector2(-100, -100));
            _input.GetTypedCharacters().Returns(string.Empty);

            // Focus comes from a click on the field.
            Frame(mouse: new Vector2(150, 15), pressed: true);
        }

        public string Text { get; private set; }

        /// <summary>Replaces the value the caller passes in, as a host rebinding the field would.</summary>
        public void SetExternal(string value) => Text = value;

        public string Clipboard { get; private set; } = string.Empty;

        public void Frame(Vector2? mouse = null, bool pressed = false, KeyboardKey? press = null,
            bool shift = false, bool control = false, string typed = "")
        {
            _input.MousePosition.Returns(mouse ?? new Vector2(-100, -100));
            _input.PrevMousePosition.Returns(mouse ?? new Vector2(-100, -100));
            _input.IsMouseButtonPressed(MouseButton.Left).Returns(pressed);
            _input.IsMouseButtonDown(MouseButton.Left).Returns(pressed);
            _input.GetTypedCharacters().Returns(typed);
            _input.GetClipboardText().Returns(_ => Clipboard);
            _input.When(i => i.SetClipboardText(Arg.Any<string>()))
                .Do(call => Clipboard = call.Arg<string>());

            _input.IsKeyPressed(Arg.Any<KeyboardKey>())
                .Returns(call => press is not null && call.Arg<KeyboardKey>() == press);
            _input.IsKeyDown(Arg.Any<KeyboardKey>()).Returns(call => call.Arg<KeyboardKey>() switch
            {
                KeyboardKey.LeftShift => shift,
                KeyboardKey.LeftControl => control,
                _ => false
            });

            var text = Text;

            void Draw() => _gui.TextInput(ref text, width: 280, height: 24, fontSize: 12, id: _id);

            _gui.Time.Update(0.016);
            _gui.SetStage(Pass.Pass1Build);
            _gui.BeginFrame(_surface.Canvas, TestFont);
            Draw();
            _gui.CalculateLayout();
            _gui.SetStage(Pass.Pass2Render);
            Draw();
            _gui.Render();
            _gui.EndFrame();

            Text = text;
        }

        public void Press(KeyboardKey key, bool shift = false, bool control = false) =>
            Frame(press: key, shift: shift, control: control);

        public void Type(string text) => Frame(typed: text);
    }

    [Fact]
    public void ShiftArrowsExtendTheSelectionAndTypingReplacesIt()
    {
        var field = new Field("hello world");
        field.Press(KeyboardKey.Home);

        for (var i = 0; i < 5; i++) field.Press(KeyboardKey.Right, shift: true);

        field.Type("HELLO");

        Assert.Equal("HELLO world", field.Text);
    }

    [Fact]
    public void SelectAllThenTypingReplacesEverything()
    {
        var field = new Field("hello world");

        field.Press(KeyboardKey.A, control: true);
        field.Type("x");

        Assert.Equal("x", field.Text);
    }

    [Fact]
    public void BackspaceRemovesTheSelectionRatherThanOneCharacter()
    {
        var field = new Field("hello world");
        field.Press(KeyboardKey.Home);
        for (var i = 0; i < 6; i++) field.Press(KeyboardKey.Right, shift: true);

        field.Press(KeyboardKey.Backspace);

        Assert.Equal("world", field.Text);
    }

    [Fact]
    public void CutTakesTheSelectionAndPasteBringsItBack()
    {
        var field = new Field("hello world");
        field.Press(KeyboardKey.Home);
        for (var i = 0; i < 5; i++) field.Press(KeyboardKey.Right, shift: true);

        field.Press(KeyboardKey.X, control: true);
        Assert.Equal("hello", field.Clipboard);
        Assert.Equal(" world", field.Text);

        field.Press(KeyboardKey.End);
        field.Press(KeyboardKey.V, control: true);

        Assert.Equal(" worldhello", field.Text);
    }

    [Fact]
    public void CopyTakesOnlyTheSelection()
    {
        var field = new Field("hello world");
        field.Press(KeyboardKey.End);
        for (var i = 0; i < 5; i++) field.Press(KeyboardKey.Left, shift: true);

        field.Press(KeyboardKey.C, control: true);

        Assert.Equal("world", field.Clipboard);
        Assert.Equal("hello world", field.Text);
    }

    [Fact]
    public void APlainArrowCollapsesTheSelectionInsteadOfMoving()
    {
        var field = new Field("hello world");
        field.Press(KeyboardKey.Home);
        for (var i = 0; i < 5; i++) field.Press(KeyboardKey.Right, shift: true);

        // Collapse to the left edge, then delete forward: the 'h' goes, nothing else.
        field.Press(KeyboardKey.Left);
        field.Press(KeyboardKey.Delete);

        Assert.Equal("ello world", field.Text);
    }

    [Fact]
    public void ControlArrowsMoveByWord()
    {
        var field = new Field("hello brave world");
        field.Press(KeyboardKey.Home);

        field.Press(KeyboardKey.Right, control: true);
        field.Press(KeyboardKey.Backspace, control: true);

        Assert.Equal("brave world", field.Text);
    }

    [Fact]
    public void ControlShiftArrowSelectsAWord()
    {
        var field = new Field("hello world");
        field.Press(KeyboardKey.Home);

        field.Press(KeyboardKey.Right, shift: true, control: true);
        field.Type("bye ");

        Assert.Equal("bye world", field.Text);
    }

    [Fact]
    public void ThePassedValueWinsWhenItChangesUnderneathTheField()
    {
        // The inspector moving to another node must not leave the previous node's name on screen.
        var field = new Field("first");
        Assert.Equal("first", field.Text);

        field.SetExternal("second");
        field.Frame();

        Assert.Equal("second", field.Text);
    }

    [Fact]
    public void DisabledFieldNeverGainsFocusAndIgnoresTyping()
    {
        using var surface = SKSurface.Create(new SKImageInfo(300, 60));
        var input = Substitute.For<IInputHandler>();
        var gui = new TestableGui { Input = input };
        gui.SetScreenRect(300, 60);
        var text = "locked";
        var id = $"field/{Guid.NewGuid():N}";

        void Frame(Vector2 mouse, bool pressed = false, string typed = "")
        {
            input.MousePosition.Returns(mouse);
            input.PrevMousePosition.Returns(mouse);
            input.IsMouseButtonPressed(MouseButton.Left).Returns(pressed);
            input.IsMouseButtonDown(MouseButton.Left).Returns(pressed);
            input.GetTypedCharacters().Returns(typed);
            input.GetClipboardText().Returns(string.Empty);
            input.IsKeyPressed(Arg.Any<KeyboardKey>()).Returns(false);
            input.IsKeyDown(Arg.Any<KeyboardKey>()).Returns(false);

            void Draw() => gui.TextInput(ref text, width: 280, height: 24, fontSize: 12,
                enabled: false, id: id);

            gui.Time.Update(0.016);
            gui.SetStage(Pass.Pass1Build);
            gui.BeginFrame(surface.Canvas, TestFont);
            Draw();
            gui.CalculateLayout();
            gui.SetStage(Pass.Pass2Render);
            Draw();
            gui.Render();
            gui.EndFrame();
        }

        Frame(new Vector2(150, 15), pressed: true);
        Frame(new Vector2(150, 15), typed: "XYZ");

        Assert.Equal("locked", text);
    }

}
