namespace Guinevere.Tests.CodeEditing;

/// <summary>
/// Text with selections written inline: <c>|</c> is a caret and <c>«…»</c> a selection with its caret at the end.
/// </summary>
static class EditorTestText
{
    internal static TextEditorState Editor(string marked, LanguageServices? language = null,
        TextEditorSettings? settings = null)
    {
        var (text, selections) = Parse(marked);
        var state = new TextEditorState(text, language ?? LanguageServices.ForLanguage("csharp"))
        {
            Settings = settings ?? new TextEditorSettings { SuggestWhileTyping = false },
            Clipboard = new TestClipboard(),
        };
        state.Document.SetSelections(selections.Count == 0 ? [TextSelection.At(0)] : selections);
        return state;
    }

    static (string Text, List<TextSelection> Selections) Parse(string marked)
    {
        var text = new System.Text.StringBuilder();
        var selections = new List<TextSelection>();
        var anchor = -1;
        foreach (var c in marked)
        {
            switch (c)
            {
                case '|':
                    selections.Add(TextSelection.At(text.Length));
                    break;
                case '«':
                    anchor = text.Length;
                    break;
                case '»':
                    selections.Add(new TextSelection(anchor, text.Length));
                    break;
                default:
                    text.Append(c);
                    break;
            }
        }

        return (text.ToString(), selections);
    }

    /// <summary>The text with its selections marked the way <see cref="Editor"/> reads them.</summary>
    internal static string Show(TextEditorState state)
    {
        var text = state.Document.Buffer.Text;
        var marks = new SortedDictionary<int, string>();
        foreach (var selection in state.Document.Selections)
        {
            if (selection.IsEmpty)
            {
                marks[selection.Caret] = marks.GetValueOrDefault(selection.Caret) + "|";
            }
            else
            {
                marks[selection.Start] = marks.GetValueOrDefault(selection.Start) + "«";
                marks[selection.End] = "»" + marks.GetValueOrDefault(selection.End);
            }
        }

        foreach (var (offset, mark) in marks.Reverse())
        {
            text = text.Insert(offset, mark);
        }

        return text;
    }

    internal static string Run(string marked, params string[] commands)
    {
        var state = Editor(marked);
        foreach (var command in commands)
        {
            Assert.True(state.Execute(command), command);
        }

        return Show(state);
    }

    internal sealed class TestClipboard : IClipboard
    {
        internal string Text { get; set; } = "";

        public string GetClipboardText() => Text;

        public void SetClipboardText(string text) => Text = text;
    }
}
