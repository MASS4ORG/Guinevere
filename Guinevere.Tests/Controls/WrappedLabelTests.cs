using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>The read-only wrapped label: one reflowing text node, drag-to-select, Ctrl+A and Ctrl+C.</summary>
public class WrappedLabelTests
{
    const string Text = "The quick brown fox jumps over the lazy dog, again and again, until the pane wraps it.";

    TextEditState? _state;
    float _paneWidth = 240f;

    void Draw(Gui gui, bool copyable = true)
    {
        using (gui.Node(_paneWidth, 200, "pane").Enter())
        {
            _state = gui.WrappedLabel(Text, 14, copyable: copyable);
        }
    }

    static LayoutNode TextNode(Gui gui) => gui.RootNode!.Children[0].Children[0].Children[0];

    static int LineCount(Gui gui) =>
        (int)MathF.Round(TextNode(gui).Rect.H / WrappedTextLayout.LineHeight(14));

    [Fact]
    public void WrapsToThePaneWidthInTheFirstFrame()
    {
        using var harness = new FrameHarness();

        harness.Frame(g => Draw(g));

        Assert.True(LineCount(harness.Gui) > 1);
        Assert.Equal(240f, TextNode(harness.Gui).Rect.W, 1);
    }

    [Fact]
    public void ReflowsInTheSameFrameTheWidthChanges()
    {
        using var harness = new FrameHarness();
        harness.Frame(g => Draw(g));
        var wide = LineCount(harness.Gui);

        _paneWidth = 120f;
        TextLayout? expected = null;
        harness.Frame(g =>
        {
            Draw(g);
            expected ??= g.LayoutText(Text, 120f, 14);
        });

        Assert.True(LineCount(harness.Gui) > wide);
        Assert.Equal(expected!.Height, TextNode(harness.Gui).Rect.H, 1);
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
