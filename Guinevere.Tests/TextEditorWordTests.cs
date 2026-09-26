namespace Guinevere.Tests;

public class TextEditorWordTests
{
    [Theory]
    [InlineData("alpha beta", 2, 0, 5)]
    [InlineData("alpha beta", 8, 6, 10)]
    [InlineData("one\ntwo", 5, 4, 7)]
    [InlineData("alpha beta", 100, 6, 10)]
    [InlineData("", 0, 0, 0)]
    public void DoubleClickSelectsOnlyTheWordAtTheCaret(string text, int index, int start, int end) =>
        Assert.Equal((start, end), TextEditor.WordAt(text, index));
}
