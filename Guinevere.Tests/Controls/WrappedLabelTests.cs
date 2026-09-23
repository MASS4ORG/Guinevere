using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>The read-only wrapped label: one node per visual line, drag-to-select, Ctrl+A and Ctrl+C.</summary>
public class WrappedLabelTests
{
    const string Text = "The quick brown fox jumps over the lazy dog, again and again, until the pane wraps it.";

    readonly SKFont _font = new() { Size = 14 };
    TextEditState? _state;

    void Draw(Gui gui, bool copyable = true)
    {
        using (gui.Node(240, 200, "pane").Enter())
            _state = gui.WrappedLabel(Text, 14, _font, Color.White, copyable: copyable);
    }

    static int LineCount(Gui gui) => gui.RootNode!.Children[0].Children.Count;

    [Fact]
    public void WrapsIntoOneNodePerLine()
    {
        using var harness = new FrameHarness();

        harness.Frame(g => Draw(g));
        harness.Frame(g => Draw(g));

        Assert.True(LineCount(harness.Gui) > 1);
        Assert.Equal(WrappedTextLayout.Wrap(Text, _font, 240).Count, LineCount(harness.Gui));
    }

    [Fact]
    public void DraggingSelectsARangeAndReleasingEndsIt()
    {
        using var harness = new FrameHarness();
        harness.Frame(g => Draw(g));

        harness.Input.MoveTo(1, 5);
        harness.Input.PressButton();
        harness.Frame(g => Draw(g));
        harness.Input.MoveTo(120, 5);
        harness.Frame(g => Draw(g));

        Assert.True(_state!.HasSelection);
        Assert.StartsWith("The", _state.SelectedText, StringComparison.Ordinal);

        harness.Input.ReleaseButton();
        harness.Frame(g => Draw(g));
        Assert.False(_state.IsSelecting);
    }

    [Fact]
    public void CtrlASelectsEverythingAndCtrlCCopiesIt()
    {
        using var harness = new FrameHarness();
        harness.Frame(g => Draw(g));

        harness.Input.PressKey(KeyboardKey.LeftControl);
        harness.Input.PressKey(KeyboardKey.A);
        harness.Frame(g => Draw(g));
        Assert.Equal(Text, _state!.SelectedText);

        harness.Input.PressKey(KeyboardKey.C);
        harness.Frame(g => Draw(g));
        Assert.Equal(Text, harness.Input.GetClipboardText());
    }

    [Fact]
    public void CtrlCWithoutSelectionCopiesTheWholeText()
    {
        using var harness = new FrameHarness();
        harness.Frame(g => Draw(g));

        harness.Input.PressKey(KeyboardKey.RightControl);
        harness.Input.PressKey(KeyboardKey.C);
        harness.Frame(g => Draw(g));

        Assert.Equal(Text, harness.Input.GetClipboardText());
    }

    [Fact]
    public void ShortcutsAreOffWhenNotCopyable()
    {
        using var harness = new FrameHarness();
        harness.Frame(g => Draw(g, copyable: false));

        harness.Input.PressKey(KeyboardKey.LeftControl);
        harness.Input.PressKey(KeyboardKey.A);
        harness.Frame(g => Draw(g, copyable: false));

        Assert.False(_state!.HasSelection);
    }
}
