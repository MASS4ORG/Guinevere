namespace Guinevere;

public static partial class EditorCommands
{
    static void RegisterMovement()
    {
        Movement("cursorLeft", (state, selection, extend) => !extend && !selection.IsEmpty
            ? selection.Start
            : TextNavigation.PreviousBoundary(state.Document.Buffer, selection.Caret));
        Movement("cursorRight", (state, selection, extend) => !extend && !selection.IsEmpty
            ? selection.End
            : TextNavigation.NextBoundary(state.Document.Buffer, selection.Caret));
        Movement("cursorWordLeft", (state, selection, _) =>
            TextNavigation.WordLeft(state.Document.Buffer, selection.Caret));
        Movement("cursorWordRight", (state, selection, _) =>
            TextNavigation.WordRight(state.Document.Buffer, selection.Caret));
        Movement("cursorHome", (state, selection, _) => SmartHome(state.Document.Buffer, selection.Caret));
        Movement("cursorEnd", (state, selection, _) => LineEnd(state.Document.Buffer, selection.Caret));
        Movement("cursorTop", (_, _, _) => 0);
        Movement("cursorBottom", (state, _, _) => state.Document.Buffer.Length);
        Vertical("cursorUp", _ => -1);
        Vertical("cursorDown", _ => 1);
        Vertical("cursorPageUp", state => -Math.Max(1, state.PageRows - 1));
        Vertical("cursorPageDown", state => Math.Max(1, state.PageRows - 1));
    }

    /// <summary>Registers a horizontal movement and its selecting variant, which keeps the anchor.</summary>
    static void Movement(string name, Func<TextEditorState, TextSelection, bool, int> target)
    {
        Register(name, state => Move(state, target, extend: false));
        Register(name + "Select", state => Move(state, target, extend: true));
    }

    static void Move(TextEditorState state, Func<TextEditorState, TextSelection, bool, int> target, bool extend)
    {
        var document = state.Document;
        document.SetSelections([.. document.Selections.Select(selection =>
            selection.MoveTo(target(state, selection, extend), extend))], document.PrimaryIndex);
        document.BreakUndoGroup();
        state.RevealCaret = true;
    }

    /// <summary>Registers a movement by visible rows that keeps the column it started from.</summary>
    static void Vertical(string name, Func<TextEditorState, int> rows)
    {
        Register(name, state => MoveRows(state, rows(state), extend: false));
        Register(name + "Select", state => MoveRows(state, rows(state), extend: true));
    }

    static void MoveRows(TextEditorState state, int rows, bool extend)
    {
        var document = state.Document;
        document.SetSelections([.. document.Selections.Select(selection =>
            RowTarget(state, selection, rows, extend))], document.PrimaryIndex);
        document.BreakUndoGroup();
        state.RevealCaret = true;
    }

    /// <summary>
    /// The selection after moving its caret by rows, skipping folded lines and aiming for the remembered visual
    /// column. Moving past the first or last row goes to the start or end of the text.
    /// </summary>
    internal static TextSelection RowTarget(TextEditorState state, TextSelection selection, int rows, bool extend)
    {
        var buffer = state.Document.Buffer;
        var tabSize = state.Settings.TabSize;
        var line = buffer.LineOf(selection.Caret);
        var column = selection.Column >= 0
            ? selection.Column
            : TextNavigation.VisualColumn(buffer.Line(line), selection.Caret - buffer.LineStart(line), tabSize);
        if (!extend && !selection.IsEmpty)
        {
            selection = TextSelection.At(rows < 0 ? selection.Start : selection.End);
            line = buffer.LineOf(selection.Caret);
        }

        var row = state.Folds.RowOfLine(line) + rows;
        var lastRow = state.Folds.RowCount(buffer.LineCount) - 1;
        if (row < 0)
        {
            return selection.MoveTo(0, extend, column);
        }

        if (row > lastRow)
        {
            return selection.MoveTo(buffer.Length, extend, column);
        }

        var target = state.Folds.LineOfRow(row);
        var offset = buffer.LineStart(target) + TextNavigation.ColumnAt(buffer.Line(target), column, tabSize);
        return selection.MoveTo(offset, extend, column);
    }

    /// <summary>The first non-blank character of the line, or the line start when the caret is already there.</summary>
    static int SmartHome(ITextBuffer buffer, int caret)
    {
        var line = buffer.LineOf(caret);
        var start = buffer.LineStart(line);
        var indent = start + TextNavigation.Indentation(buffer.Line(line)).Length;
        return caret == indent ? start : indent;
    }

    static int LineEnd(ITextBuffer buffer, int caret)
    {
        var line = buffer.LineOf(caret);
        return buffer.LineStart(line) + buffer.LineLength(line);
    }
}
