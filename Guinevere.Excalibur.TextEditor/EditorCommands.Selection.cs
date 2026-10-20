namespace Guinevere;

public static partial class EditorCommands
{
    /// <summary>How far bracket matching looks from the caret, so a huge unbalanced file stays fast.</summary>
    const int BracketSearchLimit = 100_000;

    static void RegisterSelection()
    {
        Register("selectAll", state =>
            state.Document.SetSelections([new TextSelection(0, state.Document.Buffer.Length)]));
        Register("selectLine", SelectLine);
        Register("addCursorAbove", state => AddCursor(state, -1));
        Register("addCursorBelow", state => AddCursor(state, 1));
        Register("selectNextOccurrence", SelectNextOccurrence);
        Register("selectAllOccurrences", SelectAllOccurrences);
        Register("cancel", Cancel);
        Register("jumpToBracket", JumpToBracket);
        Register("fold", state => FoldAtCaret(state, fold: true));
        Register("unfold", state => FoldAtCaret(state, fold: false));
        Register("triggerSuggest", state => state.TriggerCompletion());
        Register("acceptSuggestion", Editing(AcceptCompletion));
        Register("nextSuggestion", state => state.Completion?.MoveSelection(1));
        Register("previousSuggestion", state => state.Completion?.MoveSelection(-1));
    }

    /// <summary>Selects each caret's whole line with its break; a selection already covering lines grows by one.</summary>
    static void SelectLine(TextEditorState state)
    {
        var buffer = state.Document.Buffer;
        state.Document.SetSelections([.. state.Document.Selections.Select(selection =>
        {
            var start = buffer.LineStart(buffer.LineOf(selection.Start));
            return new TextSelection(start, LineEndWithBreak(buffer, buffer.LineOf(selection.End)));
        })], state.Document.PrimaryIndex);
        state.RevealCaret = true;
    }

    /// <summary>Adds a caret one row above the topmost caret or below the bottommost, at the same visual column.</summary>
    static void AddCursor(TextEditorState state, int direction)
    {
        var document = state.Document;
        var edge = direction < 0 ? document.Selections[0] : document.Selections[^1];
        var line = document.Buffer.LineOf(edge.Caret);
        if ((direction < 0 && line == 0) || (direction > 0 && line == document.Buffer.LineCount - 1))
        {
            return;
        }

        var added = RowTarget(state, TextSelection.At(edge.Caret) with { Column = edge.Column }, direction, false);
        document.SetSelections([.. document.Selections, added]);
        state.RevealCaret = true;
    }

    /// <summary>
    /// Selects the word under an empty primary caret, or else adds the next occurrence of the primary selection's
    /// text after the last selection, wrapping to the start.
    /// </summary>
    static void SelectNextOccurrence(TextEditorState state)
    {
        var document = state.Document;
        var buffer = document.Buffer;
        var primary = document.Primary;
        if (primary.IsEmpty)
        {
            var (start, end) = TextNavigation.WordAt(buffer, primary.Caret);
            if (end > start)
            {
                var selections = document.Selections.ToList();
                selections[document.PrimaryIndex] = new TextSelection(start, end);
                document.SetSelections(selections, document.PrimaryIndex);
            }

            return;
        }

        var needle = buffer.GetText(primary.Start, primary.Length);
        var text = buffer.Text;
        var from = document.Selections[^1].End;
        var found = text.IndexOf(needle, from, StringComparison.Ordinal);
        found = found < 0 ? text.IndexOf(needle, 0, StringComparison.Ordinal) : found;
        if (found < 0 || document.Selections.Any(selection => selection.Start == found))
        {
            return;
        }

        document.SetSelections([.. document.Selections, new TextSelection(found, found + needle.Length)]);
        state.RevealCaret = true;
    }

    static void SelectAllOccurrences(TextEditorState state)
    {
        var document = state.Document;
        var primary = document.Primary;
        var (start, end) = primary.IsEmpty ? TextNavigation.WordAt(document.Buffer, primary.Caret) : (primary.Start,
            primary.End);
        if (end <= start)
        {
            return;
        }

        var needle = document.Buffer.GetText(start, end - start);
        var text = document.Buffer.Text;
        var selections = new List<TextSelection>();
        var primaryIndex = 0;
        for (var at = text.IndexOf(needle, StringComparison.Ordinal); at >= 0;
             at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
        {
            primaryIndex = at == start ? selections.Count : primaryIndex;
            selections.Add(new TextSelection(at, at + needle.Length));
        }

        document.SetSelections(selections, primaryIndex);
    }

    /// <summary>Closes the completion list or find bar first; then drops extra carets, then the selection.</summary>
    static void Cancel(TextEditorState state)
    {
        var document = state.Document;
        if (state.Completion is not null)
        {
            state.CloseCompletion();
        }
        else if (state.Find.IsOpen)
        {
            state.Find.IsOpen = false;
        }
        else if (document.Selections.Count > 1)
        {
            document.SetSelections([document.Primary]);
        }
        else if (!document.Primary.IsEmpty)
        {
            document.SetSelections([TextSelection.At(document.Primary.Caret)]);
        }
    }

    static void JumpToBracket(TextEditorState state)
    {
        var document = state.Document;
        document.SetSelections([.. document.Selections.Select(selection =>
            MatchBracket(document.Buffer, selection.Caret, state.Language) is { } match
                ? TextSelection.At(match.Open == selection.Caret || match.Open == selection.Caret - 1
                    ? match.Close
                    : match.Open)
                : selection)], document.PrimaryIndex);
        state.RevealCaret = true;
    }

    /// <summary>
    /// The bracket pair at a caret: the bracket right after it, or else the one right before it, with its partner.
    /// </summary>
    public static (int Open, int Close)? MatchBracket(ITextBuffer buffer, int caret, LanguageServices language)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(language);
        foreach (var at in (ReadOnlySpan<int>)[caret, caret - 1])
        {
            if (at < 0 || at >= buffer.Length)
            {
                continue;
            }

            foreach (var (open, close) in language.Brackets)
            {
                if (buffer[at] == open && Scan(buffer, at, open, close, 1) is { } closing)
                {
                    return (at, closing);
                }

                if (buffer[at] == close && Scan(buffer, at, close, open, -1) is { } opening)
                {
                    return (opening, at);
                }
            }
        }

        return null;
    }

    /// <summary>The offset of the partner of the bracket at <paramref name="from"/>, read as one window of text.</summary>
    static int? Scan(ITextBuffer buffer, int from, char self, char partner, int step)
    {
        var start = step > 0 ? from : Math.Max(0, from - BracketSearchLimit);
        var end = step > 0 ? Math.Min(buffer.Length, from + BracketSearchLimit) : from + 1;
        var window = buffer.GetText(start, end - start);
        var depth = 0;
        for (var i = from - start; i >= 0 && i < window.Length; i += step)
        {
            depth += window[i] == self ? 1 : window[i] == partner ? -1 : 0;
            if (depth == 0)
            {
                return start + i;
            }
        }

        return null;
    }

    /// <summary>Folds the innermost region holding the primary caret, or unfolds the folded region on its line.</summary>
    static void FoldAtCaret(TextEditorState state, bool fold)
    {
        var line = state.Document.Buffer.LineOf(state.Document.Primary.Caret);
        if (!fold)
        {
            if (state.IsFolded(line))
            {
                state.ToggleFold(line);
            }

            return;
        }

        var region = state.FoldingRanges.Where(range => range.StartLine <= line && range.EndLine >= line
                && !state.IsFolded(range.StartLine))
            .OrderByDescending(range => range.StartLine).FirstOrDefault();
        if (region.EndLine > region.StartLine)
        {
            state.ToggleFold(region.StartLine);
            var start = state.Document.Buffer.LineStart(region.StartLine);
            state.Document.SetSelections([TextSelection.At(start + state.Document.Buffer.LineLength(region.StartLine))]);
        }
    }

    /// <summary>Replaces the typed prefix with the highlighted completion and closes the list.</summary>
    static void AcceptCompletion(TextEditorState state)
    {
        if (state.Completion is not { SelectedItem: { } item } session)
        {
            return;
        }

        var caret = state.Document.Primary.Caret;
        var text = item.InsertText ?? item.Label;
        state.CloseCompletion();
        state.Document.Apply([new TextEdit(session.ReplaceStart, caret - session.ReplaceStart, text)],
            [TextSelection.At(session.ReplaceStart + text.Length)]);
    }
}
