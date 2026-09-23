using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Where a text area's caret lands on multi-line text, and that a focused area draws it.</summary>
public class TextAreaCaretTests
{
    [Theory]
    [InlineData("", 0, 0, 0)]
    [InlineData("ab\ncd", 0, 0, 0)]
    [InlineData("ab\ncd", 2, 0, 2)]
    [InlineData("ab\ncd", 3, 1, 0)]
    [InlineData("ab\ncd", 5, 1, 2)]
    [InlineData("ab\ncd", 99, 1, 2)]
    [InlineData("ab\n\ncd", 3, 1, 0)]
    [InlineData("ab", -4, 0, 0)]
    public void CaretOffsetMapsToLineAndColumn(string text, int position, int line, int column)
    {
        var caret = ControlsExtensions.CaretLineAndColumn(text, position);

        Assert.Equal(line, caret.Line);
        Assert.Equal(column, caret.Column);
    }

    [Fact]
    public void FocusedAreaDrawsTheCaretInsideItself()
    {
        using var harness = new FrameHarness();
        var text = "first\nsecond";
        void Draw(Gui gui) => gui.TextArea(ref text, 300, 120, id: "notes");

        harness.Frame(Draw);
        var area = harness.Gui.RootNode!.Children[0];
        harness.Click(Draw, new Vector2(area.Rect.X + 20, area.Rect.Y + 30));
        harness.Frame(Draw);

        area = harness.Gui.RootNode!.Children[0];
        Assert.Equal(area.Id, harness.Gui.Focus.CurrentFocusedId);
        // The caret used to be a render-pass-only node that never got a layout rect.
        Assert.DoesNotContain(area.Children, child => child.Rect is { W: 0, H: 0 });
    }
}
