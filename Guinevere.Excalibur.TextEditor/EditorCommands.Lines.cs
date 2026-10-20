namespace Guinevere;

public static partial class EditorCommands
{
    static void RegisterLines()
    {
        Register("deleteLines", Editing(DeleteLines));
        Register("copyLinesDown", Editing(state => CopyLines(state, down: true)));
        Register("copyLinesUp", Editing(state => CopyLines(state, down: false)));
        Register("moveLinesUp", Editing(state => MoveLines(state, up: true)));
        Register("moveLinesDown", Editing(state => MoveLines(state, up: false)));
        Register("toggleLineComment", Editing(ToggleLineComment));
    }

    static void DeleteLines(TextEditorState state)
    {
        var buffer = state.Document.Buffer;
        var edits = new List<TextEdit>();
        foreach (var (first, last) in SelectedLineBlocks(state))
        {
            var start = buffer.LineStart(first);
            var end = LineEndWithBreak(buffer, last);
            if (end == buffer.Length && first > 0)
            {
                start--;
            }

            edits.Add(new TextEdit(start, end - start, ""));
        }

        ApplyMapped(state, edits);
    }

    /// <summary>
    /// Duplicates each block of selected lines below itself. Copying down moves the selections onto the copy;
    /// copying up leaves them on the original, which is now the upper of the two.
    /// </summary>
    static void CopyLines(TextEditorState state, bool down)
    {
        var document = state.Document;
        var buffer = document.Buffer;
        var edits = new List<TextEdit>();
        var after = new List<TextSelection>();
        var shift = 0;
        var selection = 0;
        foreach (var (first, last) in SelectedLineBlocks(state))
        {
            var start = buffer.LineStart(first);
            var end = buffer.LineStart(last) + buffer.LineLength(last);
            var copy = "\n" + buffer.GetText(start, end - start);
            edits.Add(new TextEdit(end, 0, copy));
            var move = shift + (down ? copy.Length : 0);
            for (; selection < document.Selections.Count && buffer.LineOf(document.Selections[selection].Start) <= last;
                 selection++)
            {
                var current = document.Selections[selection];
                after.Add(new TextSelection(current.Anchor + move, current.Caret + move));
            }

            shift += copy.Length;
        }

        document.Apply(edits, after, EditKind.Other, document.PrimaryIndex);
    }

    static int BlockLength(ITextBuffer buffer, (int First, int Last) block) =>
        buffer.LineStart(block.Last) + buffer.LineLength(block.Last) - buffer.LineStart(block.First);

    /// <summary>Swaps each block of selected lines with the line above or below it, carrying the selections.</summary>
    static void MoveLines(TextEditorState state, bool up)
    {
        var document = state.Document;
        var buffer = document.Buffer;
        var blocks = SelectedLineBlocks(state);
        if ((up && blocks[0].First == 0) || (!up && blocks[^1].Last == buffer.LineCount - 1))
        {
            return;
        }

        var edits = new List<TextEdit>();
        var moves = new List<(int Start, int End, int Shift)>();
        foreach (var (first, last) in blocks)
        {
            var other = up ? first - 1 : last + 1;
            var blockStart = buffer.LineStart(first);
            var blockText = buffer.GetText(blockStart, BlockLength(buffer, (first, last)));
            var otherText = buffer.Line(other);
            var start = buffer.LineStart(Math.Min(first, other));
            var end = buffer.LineStart(Math.Max(last, other)) + buffer.LineLength(Math.Max(last, other));
            edits.Add(new TextEdit(start, end - start, up ? blockText + "\n" + otherText : otherText + "\n" + blockText));
            moves.Add((blockStart, blockStart + blockText.Length, up ? -(otherText.Length + 1) : otherText.Length + 1));
        }

        int Move(int offset)
        {
            foreach (var (start, end, shift) in moves)
            {
                if (offset >= start && offset <= end)
                {
                    return offset + shift;
                }
            }

            return offset;
        }

        var after = document.Selections.Select(selection =>
            new TextSelection(Move(selection.Anchor), Move(selection.Caret))).ToList();
        document.Apply(edits, after, EditKind.Other, document.PrimaryIndex);
    }

    /// <summary>
    /// Comments out the selected lines with the language's line comment, or removes it when every non-blank line
    /// already has it. Markers are aligned at the smallest indentation of the block.
    /// </summary>
    static void ToggleLineComment(TextEditorState state)
    {
        if (state.Language.LineComment is not { Length: > 0 } marker)
        {
            return;
        }

        var buffer = state.Document.Buffer;
        var edits = new List<TextEdit>();
        foreach (var (first, last) in SelectedLineBlocks(state))
        {
            var lines = Enumerable.Range(first, last - first + 1)
                .Select(line => (Line: line, Text: buffer.Line(line)))
                .Where(line => !string.IsNullOrWhiteSpace(line.Text) || first == last).ToList();
            var commented = lines.All(line => line.Text.TrimStart().StartsWith(marker, StringComparison.Ordinal));
            var column = lines.Count == 0 ? 0 : lines.Min(line => TextNavigation.Indentation(line.Text).Length);
            foreach (var (line, text) in lines)
            {
                var start = buffer.LineStart(line);
                if (commented)
                {
                    var at = text.IndexOf(marker, StringComparison.Ordinal);
                    var length = marker.Length + (at + marker.Length < text.Length && text[at + marker.Length] == ' '
                        ? 1
                        : 0);
                    edits.Add(new TextEdit(start + at, length, ""));
                }
                else
                {
                    edits.Add(new TextEdit(start + Math.Min(column, text.Length), 0, marker + " "));
                }
            }
        }

        ApplyMapped(state, edits);
    }
}
