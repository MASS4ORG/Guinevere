using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>Ordinary text nodes support optional selection, copying and immutable text.</summary>
public sealed class TextSelectionTests
{
    const string Text = "hello world";

    static void Draw(Gui gui) => gui.DrawText(Text, selectable: true);

    static TextEditState State(FrameHarness h) =>
        h.Gui.ControlState($"{h.Gui.RootNode!.Children[0].Id}/selection", () => new TextEditState());

    /// <summary>Dragging selects text and continues outside the label until release.</summary>
    [Fact]
    public void PointerSelectionCopiesWithoutEditing()
    {
        using var h = new FrameHarness();
        h.Frame(Draw);
        h.Input.MoveTo(1, 5);
        h.Input.PressButton();
        h.Frame(Draw);
        h.Input.MoveTo(300, 5);
        h.Frame(Draw);
        Assert.Equal(Text, State(h).SelectedText);
        h.Input.ReleaseButton();
        h.Frame(Draw);
        Assert.False(State(h).IsSelecting);
        h.Input.PressKey(KeyboardKey.LeftControl);
        h.Input.PressKey(KeyboardKey.C);
        h.Frame(Draw);
        Assert.Equal(Text, h.Input.GetClipboardText());
        h.Input.TypeText("changed");
        h.Input.PressKey(KeyboardKey.Backspace);
        h.Input.PressKey(KeyboardKey.X);
        h.Frame(Draw);
        Assert.Equal(Text, State(h).Text);
    }

    /// <summary>Only the focused selectable label responds to copy and select-all.</summary>
    [Fact]
    public void ShortcutsBelongToTheFocusedLabel()
    {
        using var h = new FrameHarness();
        void Two(Gui gui)
        {
            gui.DrawText(Text, selectable: true);
            gui.DrawText("second label", selectable: true);
        }
        h.Frame(Two);
        h.Input.PressKey(KeyboardKey.LeftControl);
        h.Input.PressKey(KeyboardKey.C);
        h.Frame(Two);
        Assert.Equal("", h.Input.GetClipboardText());
        h.Click(Two, new Vector2(1, 5));
        h.Input.PressKey(KeyboardKey.A);
        h.Frame(Two);
        Assert.Equal(Text, State(h).SelectedText);
        h.Input.ReleaseKey(KeyboardKey.C);
        h.Input.PressKey(KeyboardKey.C);
        h.Frame(Two);
        Assert.Equal(Text, h.Input.GetClipboardText());
    }

    /// <summary>Keyboard selection uses shift arrows and Home/End without allowing text edits.</summary>
    [Theory]
    [InlineData(KeyboardKey.Right, KeyboardKey.Home, "h")]
    [InlineData(KeyboardKey.Left, KeyboardKey.End, "d")]
    [InlineData(KeyboardKey.End, KeyboardKey.Home, Text)]
    [InlineData(KeyboardKey.Home, KeyboardKey.End, Text)]
    public void ShiftKeysExtendSelection(KeyboardKey key, KeyboardKey start, string expected)
    {
        using var h = new FrameHarness();
        h.Frame(Draw);
        h.Click(Draw, new Vector2(1, 5));
        h.Input.PressKey(start);
        h.Frame(Draw);
        h.Input.ReleaseKey(start);
        h.Input.PressKey(KeyboardKey.RightShift);
        h.Input.PressKey(key);
        h.Frame(Draw);
        Assert.Equal(expected, State(h).SelectedText);
    }

    /// <summary>Unshifted arrows collapse the selection to its corresponding edge.</summary>
    [Theory]
    [InlineData(KeyboardKey.Left, 0)]
    [InlineData(KeyboardKey.Right, 11)]
    public void ArrowsCollapseSelection(KeyboardKey key, int expected)
    {
        using var h = new FrameHarness();
        h.Frame(Draw);
        h.Click(Draw, new Vector2(1, 5));
        h.Input.PressKey(KeyboardKey.RightControl);
        h.Input.PressKey(KeyboardKey.A);
        h.Frame(Draw);
        h.Input.ReleaseKey(KeyboardKey.RightControl);
        h.Input.PressKey(key);
        h.Frame(Draw);
        Assert.False(State(h).HasSelection);
        Assert.Equal(expected, State(h).CursorPosition);
    }

    /// <summary>A click followed by copy without a selection copies the whole label.</summary>
    [Fact]
    public void CopyWithoutSelectionUsesTheWholeLabel()
    {
        using var h = new FrameHarness();
        h.Frame(Draw);
        h.Click(Draw, new Vector2(1, 5));
        h.Input.PressKey(KeyboardKey.LeftControl);
        h.Input.PressKey(KeyboardKey.C);
        h.Frame(Draw);
        Assert.Equal(Text, h.Input.GetClipboardText());
    }

    /// <summary>Empty and wrapped labels use their visual lines when selecting.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("hello\nworld")]
    public void EmptyAndMultilineLabelsRemainSelectable(string text)
    {
        using var h = new FrameHarness();
        void Lines(Gui gui) => gui.DrawText(text, wrapWidth: 70, selectable: true);
        h.Frame(Lines);
        h.Input.MoveTo(1, 5);
        h.Input.PressButton();
        h.Frame(Lines);
        h.Input.MoveTo(300, 40);
        h.Frame(Lines);
        Assert.Equal(text, State(h).Text);
        if (text.Length > 0) Assert.Equal(text, State(h).SelectedText);
    }

    /// <summary>Repeated clicks select a word and then the whole label.</summary>
    [Fact]
    public void DoubleAndTripleClickSelectWordAndText()
    {
        using var h = new FrameHarness();
        h.Frame(Draw);
        h.Click(Draw, new Vector2(2, 5));
        h.Click(Draw, new Vector2(2, 5));
        Assert.Equal("hello", State(h).SelectedText);
        h.Click(Draw, new Vector2(2, 5));
        Assert.Equal(Text, State(h).SelectedText);
    }
}
