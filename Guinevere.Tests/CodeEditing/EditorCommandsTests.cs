using static Guinevere.Tests.CodeEditing.EditorTestText;

namespace Guinevere.Tests.CodeEditing;

public class EditorCommandsTests
{
    static string Typed(string marked, string text)
    {
        var state = Editor(marked);
        state.Type(text);
        return Show(state);
    }

    [Theory]
    [InlineData("f|", "(", "f(|)")]
    [InlineData("f|x", "(", "f(|x")]
    [InlineData("f(|)", ")", "f()|")]
    [InlineData("«ab»", "[", "[«ab»]")]
    [InlineData("x = |;", "\"", "x = \"|\";")]
    [InlineData("it|", "'", "it'|")]
    [InlineData("a| b|", "z", "az| bz|")]
    public void TypingClosesPairsWrapsSelectionsAndStepsOverClosers(string before, string typed, string after) =>
        Assert.Equal(after, Typed(before, typed));

    [Theory]
    [InlineData("    if (x) {|}", "    if (x) {\n        |\n    }")]
    [InlineData("  a|", "  a\n  |")]
    [InlineData("a«bc»d", "a\n|d")]
    public void NewLinesKeepAndOpenIndentation(string before, string after) => Assert.Equal(after, Run(before, "newline"));

    [Theory]
    [InlineData("ab|c", "a|c")]
    [InlineData("f(|)", "f|")]
    [InlineData("        |x", "    |x")]
    [InlineData("a\n|b", "a|b")]
    [InlineData("|abc", "|abc")]
    [InlineData("a«bc»d", "a|d")]
    [InlineData("👨‍👩‍👧|x", "|x")]
    [InlineData("é|", "|")]
    public void DeleteLeftRemovesWholeTextElementsPairsAndIndentStops(string before, string after) =>
        Assert.Equal(after, Run(before, "deleteLeft"));

    [Theory]
    [InlineData("deleteRight", "a|👍b", "a|b")]
    [InlineData("deleteWordLeft", "foo bar|", "foo |")]
    [InlineData("deleteWordRight", "|foo bar", "| bar")]
    [InlineData("cursorRight", "|👍x", "👍|x")]
    [InlineData("cursorLeft", "x👍|", "x|👍")]
    [InlineData("cursorLeft", "a«bc»d", "a|bcd")]
    [InlineData("cursorRightSelect", "a|bc", "a«b»c")]
    [InlineData("cursorWordRight", "|foo.bar", "foo|.bar")]
    [InlineData("cursorWordLeft", "foo  bar|", "foo  |bar")]
    [InlineData("cursorHome", "    ab|c", "    |abc")]
    [InlineData("cursorEnd", "a|bc\nd", "abc|\nd")]
    [InlineData("cursorBottom", "|ab\ncd", "ab\ncd|")]
    public void SingleCommandsEditAndMoveByElementsAndWords(string command, string before, string after) =>
        Assert.Equal(after, Run(before, command));

    [Fact]
    public void SmartHomeTogglesBetweenIndentationAndLineStart() =>
        Assert.Equal("|    abc", Run("    ab|c", "cursorHome", "cursorHome"));

    [Fact]
    public void VerticalMovesRememberTheVisualColumnAcrossTabsAndShortLines()
    {
        var state = Editor("abcdefgh|\nab\n\tcdefgh");

        state.Execute("cursorDown");
        Assert.Equal("abcdefgh\nab|\n\tcdefgh", Show(state));
        state.Execute("cursorDown");
        Assert.Equal("abcdefgh\nab\n\tcdef|gh", Show(state));
        state.Execute("cursorDown");
        Assert.Equal("abcdefgh\nab\n\tcdefgh|", Show(state));
        state.Execute("cursorTop");
        state.Execute("cursorUp");
        Assert.Equal("|abcdefgh\nab\n\tcdefgh", Show(state));
    }

    [Fact]
    public void PageMovesUseTheVisibleRows()
    {
        var state = Editor("|" + string.Join("\n", Enumerable.Range(0, 100)));
        state.PageRows = 11;

        state.Execute("cursorPageDown");
        Assert.Equal(10, state.Document.Buffer.LineOf(state.Document.Primary.Caret));
        state.Execute("cursorPageUpSelect");
        Assert.Equal(new TextSelection(state.Document.Buffer.LineStart(10), 0, 0), state.Document.Primary);
    }

    [Theory]
    [InlineData("tab", "a|b", "a   |b")]
    [InlineData("tab", "«a\nb»", "    «a\n    b»")]
    [InlineData("outdent", "      |x", "  |x")]
    [InlineData("indentLines", "a|\n\nb", "    a|\n\nb")]
    [InlineData("outdentLines", "\t|x", "|x")]
    [InlineData("toggleLineComment", "  «a\n    b»\n", "  // «a\n  //   b»\n")]
    [InlineData("toggleLineComment", "  // a|\n", "  a|\n")]
    [InlineData("deleteLines", "a\nb|\nc", "a\n|c")]
    [InlineData("deleteLines", "a\nb|", "a|")]
    [InlineData("copyLinesDown", "a|b\nc", "ab\na|b\nc")]
    [InlineData("copyLinesUp", "a|b\nc", "a|b\nab\nc")]
    [InlineData("moveLinesDown", "a|\nb\nc", "b\na|\nc")]
    [InlineData("moveLinesUp", "a\nb\n«c»", "a\n«c»\nb")]
    [InlineData("moveLinesUp", "|a\nb", "|a\nb")]
    public void LineCommandsWorkOnEverySelectedLine(string command, string before, string after) =>
        Assert.Equal(after, Run(before, command));

    [Fact]
    public void LineCommentNeedsALanguageMarker()
    {
        var state = Editor("a|", LanguageServices.PlainText);

        state.Execute("toggleLineComment");

        Assert.Equal("a|", Show(state));
    }

    [Theory]
    [InlineData("addCursorBelow", "ab|c\nde\nfghi", "ab|c\nde|\nfghi")]
    [InlineData("addCursorAbove", "a\nbc|", "a|\nbc|")]
    [InlineData("selectNextOccurrence", "foo |foo foo", "foo «foo» foo")]
    [InlineData("selectAllOccurrences", "x «ab» ab ab", "x «ab» «ab» «ab»")]
    [InlineData("selectLine", "a\nb|c\nd", "a\n«bc\n»d")]
    [InlineData("selectAll", "a|b", "«ab»")]
    [InlineData("cancel", "a| b|", "a b|")]
    [InlineData("cancel", "«ab»", "ab|")]
    [InlineData("jumpToBracket", "|{ a ( b ) }", "{ a ( b ) |}")]
    [InlineData("jumpToBracket", "{ a ( b )| }", "{ a |( b ) }")]
    public void SelectionCommandsAddCaretsAndMatches(string command, string before, string after) =>
        Assert.Equal(after, Run(before, command));

    [Fact]
    public void SelectNextOccurrenceAddsTheNextMatchAndWraps()
    {
        Assert.Equal("«ab» «ab» x «ab»", Run("ab «ab» x ab", "selectNextOccurrence", "selectNextOccurrence"));
    }

    [Fact]
    public void MultipleCaretsEditTogether()
    {
        var state = Editor("one|\ntwo|\nthree|");

        state.Type(";");
        state.Execute("deleteWordLeft");
        state.Type("x");

        Assert.Equal("onex|\ntwox|\nthreex|", Show(state));
        state.Execute("undo");
        Assert.Equal("one|\ntwo|\nthree|", Show(state).Replace(";", ""));
    }

    [Fact]
    public void CopyingWithoutASelectionCopiesLinesAndPasteInsertsThemAbove()
    {
        var state = Editor("a\nb|c\nd");
        var clipboard = (TestClipboard)state.Clipboard!;

        state.Execute("copy");
        Assert.Equal("bc\n", clipboard.Text);
        state.Execute("cursorDown");
        state.Execute("paste");
        Assert.Equal("a\nbc\nbc\nd|", Show(state));
        state.Execute("cut");
        Assert.Equal("a\nbc\nbc|", Show(state));
        Assert.Equal("d\n", clipboard.Text);
    }

    [Fact]
    public void PasteSpreadsOneLinePerCaretAndCutCopiesSelections()
    {
        var state = Editor("«x» «y»");
        var clipboard = (TestClipboard)state.Clipboard!;

        state.Execute("cut");
        Assert.Equal("x\ny", clipboard.Text);
        Assert.Equal("| |", Show(state));
        clipboard.Text = "1\n2";
        state.Execute("paste");
        Assert.Equal("1| 2|", Show(state));
        clipboard.Text = "z";
        state.Execute("paste");
        Assert.Equal("1z| 2z|", Show(state));
    }

    [Fact]
    public void FindSelectsMatchesAndReplacesWithGroups()
    {
        var state = Editor("|let a = 1; let b = 22;");
        state.Find.Query = @"(\d+)";
        state.Find.UseRegex = true;
        state.Find.Replacement = "<$1>";

        state.Execute("findNext");
        Assert.Equal("let a = «1»; let b = 22;", Show(state));
        state.Execute("findNext");
        Assert.Equal("let a = 1; let b = «22»;", Show(state));
        state.Execute("findPrevious");
        state.Execute("replaceOne");
        Assert.Equal("let a = <1>; let b = «22»;", Show(state));
        state.Execute("replaceAll");
        Assert.Equal("let a = <<1>>; let b = <22>|;", Show(state));
    }

    [Fact]
    public void FindHonoursCaseWholeWordsAndInvalidPatterns()
    {
        var state = Editor("«Foo» food foo");
        state.Execute("find");
        Assert.True(state.Find.IsOpen);
        Assert.Equal("Foo", state.Find.Query);
        Assert.Equal(3, state.Find.Matches(state.Document).Count);

        state.Find.WholeWord = true;
        Assert.Equal(2, state.Find.Matches(state.Document).Count);
        state.Find.MatchCase = true;
        Assert.Single(state.Find.Matches(state.Document));
        state.Find.UseRegex = true;
        state.Find.Query = "(";
        Assert.Empty(state.Find.Matches(state.Document));
        Assert.NotNull(state.Find.Error);
        state.Execute("cancel");
        Assert.False(state.Find.IsOpen);
        state.Execute("replace");
        Assert.True(state.Find.ShowReplace);
    }

    [Fact]
    public void ReadOnlyEditorsMoveAndCopyButNeverEdit()
    {
        var state = Editor("«ab»c", settings: new TextEditorSettings { ReadOnly = true });

        state.Type("x");
        state.Execute("deleteLeft");
        state.Execute("cut");
        state.Execute("copy");
        state.Execute("cursorEnd");

        Assert.Equal("abc|", Show(state));
        Assert.Equal("ab", ((TestClipboard)state.Clipboard!).Text);
    }

    [Fact]
    public void UnknownCommandsAreReportedAndCustomOnesRun()
    {
        var state = Editor("a|");
        EditorCommands.Register("test.shout", s => s.Type("!"));

        Assert.False(state.Execute("no-such-command"));
        Assert.True(state.Execute("test.shout"));
        Assert.Equal("a!|", Show(state));
        Assert.Contains("deleteLeft", EditorCommands.Names);
    }
}
