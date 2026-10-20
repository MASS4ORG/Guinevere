namespace Guinevere;

public static partial class EditorCommands
{
    static void RegisterClipboard()
    {
        Register("copy", state => Copy(state, cut: false));
        Register("cut", Editing(state => Copy(state, cut: true)));
        Register("paste", Editing(Paste));
    }

    /// <summary>
    /// Copies the selections joined by line breaks; with only bare carets it copies their whole lines, which paste
    /// then inserts as lines. Cutting deletes what was copied.
    /// </summary>
    static void Copy(TextEditorState state, bool cut)
    {
        var document = state.Document;
        var buffer = document.Buffer;
        if (document.Selections.All(selection => selection.IsEmpty))
        {
            var blocks = SelectedLineBlocks(state);
            var lines = string.Concat(blocks.Select(block =>
            {
                var start = buffer.LineStart(block.First);
                var text = buffer.GetText(start, LineEndWithBreak(buffer, block.Last) - start);
                return text.EndsWith('\n') ? text : text + "\n";
            }));
            state.Clipboard?.SetClipboardText(lines);
            state.LineClipboard = lines;
            if (cut)
            {
                DeleteLines(state);
            }

            return;
        }

        var copied = string.Join("\n", document.Selections.Select(selection =>
            buffer.GetText(selection.Start, selection.Length)));
        state.Clipboard?.SetClipboardText(copied);
        state.LineClipboard = null;
        if (cut)
        {
            Delete(state, (_, caret) => (caret, caret));
        }
    }

    /// <summary>
    /// Pastes at every caret. One line per caret is spread across them; whole lines copied without a selection go
    /// above each caret's line.
    /// </summary>
    static void Paste(TextEditorState state)
    {
        if (state.Clipboard?.GetClipboardText() is not { Length: > 0 } raw)
        {
            return;
        }

        var text = TextBufferText.NormalizeLineBreaks(raw);
        var document = state.Document;
        var buffer = document.Buffer;
        var parts = text.TrimEnd('\n').Split('\n');
        var spread = document.Selections.Count > 1 && parts.Length == document.Selections.Count;
        var asLines = !spread && text == state.LineClipboard;
        var index = 0;
        EditEach(state, selection =>
        {
            var part = spread ? parts[index++] : text;
            if (asLines && selection.IsEmpty)
            {
                var lineStart = buffer.LineStart(buffer.LineOf(selection.Caret));
                return (new TextEdit(lineStart, 0, part), TextSelection.At(selection.Caret + part.Length));
            }

            return (new TextEdit(selection.Start, selection.Length, part), TextSelection.At(selection.Start + part.Length));
        });
    }

    static void RegisterFind()
    {
        Register("find", state => OpenFind(state, replace: false));
        Register("replace", state => OpenFind(state, replace: true));
        Register("findNext", state => FindNext(state, forward: true));
        Register("findPrevious", state => FindNext(state, forward: false));
        Register("replaceOne", Editing(ReplaceOne));
        Register("replaceAll", Editing(ReplaceAll));
    }

    /// <summary>Opens the find bar, seeding the query with a single-line primary selection.</summary>
    static void OpenFind(TextEditorState state, bool replace)
    {
        var primary = state.Document.Primary;
        var buffer = state.Document.Buffer;
        if (!primary.IsEmpty && buffer.LineOf(primary.Start) == buffer.LineOf(primary.End))
        {
            state.Find.Query = buffer.GetText(primary.Start, primary.Length);
        }

        state.Find.IsOpen = true;
        state.Find.ShowReplace = replace;
    }

    /// <summary>Selects the next match after the primary selection, or the previous one before it, wrapping.</summary>
    static void FindNext(TextEditorState state, bool forward)
    {
        var matches = state.Find.Matches(state.Document);
        if (matches.Count == 0)
        {
            return;
        }

        var primary = state.Document.Primary;
        var match = forward
            ? matches.FirstOrDefault(m => m.Start >= primary.End && !(m.Start == primary.Start && m.End == primary.End),
                matches[0])
            : matches.LastOrDefault(m => m.End <= primary.Start, matches[^1]);
        state.Document.SetSelections([new TextSelection(match.Start, match.End)]);
        state.RevealCaret = true;
    }

    /// <summary>Replaces the selected match, then selects the next one.</summary>
    static void ReplaceOne(TextEditorState state)
    {
        var document = state.Document;
        var primary = document.Primary;
        var current = state.Find.Matches(document).FirstOrDefault(m => m.Start == primary.Start && m.End == primary.End);
        if (current.Length > 0)
        {
            var replacement = state.Find.ReplacementFor(document, current);
            document.Apply([new TextEdit(current.Start, current.Length, replacement)],
                [TextSelection.At(current.Start + replacement.Length)]);
        }

        FindNext(state, forward: true);
    }

    static void ReplaceAll(TextEditorState state)
    {
        var edits = state.Find.ReplaceAllEdits(state.Document);
        if (edits.Count > 0)
        {
            var end = TextEdit.Map(edits[^1].End, edits);
            state.Document.Apply(edits, [TextSelection.At(end)]);
        }
    }
}
