using static Guinevere.Tests.CodeEditing.EditorTestText;

namespace Guinevere.Tests.CodeEditing;

/// <summary>Command boundaries for line selections, history and caret movement.</summary>
public class EditorCommandBoundaryTests
{
    /// <summary>Line operations exclude a trailing line start and merge adjacent selected line blocks.</summary>
    [Fact]
    public void IndentationMergesLineBlocksAndExcludesTheTrailingLine()
    {
        Assert.Equal("    «a\n»b", Run("«a\n»b", "indentLines"));
        Assert.Equal("    a|\n    b|\nc", Run("a|\nb|\nc", "indentLines"));
    }

    /// <summary>Vertical movement collapses selections toward the direction of travel.</summary>
    [Theory]
    [InlineData("cursorUp", "a|\nb\nc")]
    [InlineData("cursorDown", "a\nb\nc|")]
    public void VerticalMovementCollapsesASelection(string command, string expected)
    {
        Assert.Equal(expected, Run("a\n«b»\nc", command));
    }

    /// <summary>Undo and redo restore a grouped typing operation through the public command API.</summary>
    [Fact]
    public void RedoRestoresTypingAfterUndo()
    {
        var state = Editor("a|");
        state.Type("bc");
        state.Execute("undo");
        Assert.Equal("a|", Show(state));
        state.Execute("redo");
        Assert.Equal("abc|", Show(state));
    }

    /// <summary>Cut preserves empty carets while deleting the other selections.</summary>
    [Fact]
    public void CutHandlesMixedCaretsAndSelections()
    {
        var state = Editor("«a» b|");
        state.Execute("cut");
        Assert.Equal("| b|", Show(state));
        Assert.Equal("a\n", ((TestClipboard)state.Clipboard!).Text);
    }

    /// <summary>Bracket matching returns no partner at empty, unmatched and out-of-range carets.</summary>
    [Theory]
    [InlineData("", 0)]
    [InlineData("(", 0)]
    [InlineData("abc", 3)]
    [InlineData("abc", -1)]
    public void UnmatchedBracketsDoNotMoveTheCaret(string text, int caret)
    {
        Assert.Null(EditorCommands.MatchBracket(new StringTextBuffer(text), caret, LanguageServices.PlainText));
    }

    /// <summary>The suggest command starts a provider request and publishes its completion list.</summary>
    [Fact]
    public async Task SuggestCommandPublishesCompletions()
    {
        var state = Editor("alpha al|");
        Assert.True(state.Execute("triggerSuggest"));
        for (var attempt = 0; attempt < 100 && state.Completion is null; attempt++)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
            state.Update(0);
        }
        Assert.Equal("alpha", Assert.Single(state.Completion!.Items).Label);
    }
}
