namespace Guinevere;

/// <summary>How an edit groups with the previous one in the undo history.</summary>
public enum EditKind
{
    /// <summary>A separate undo step.</summary>
    Other,

    /// <summary>Typed text, which merges with typing just before it.</summary>
    Typing,

    /// <summary>Deletions by Backspace or Delete, which merge with each other.</summary>
    Deleting,
}

/// <summary>What one applied edit changed, for caches that track lines.</summary>
/// <param name="FirstLine">The first line whose text changed, in the previous version.</param>
/// <param name="LineDelta">Lines added (positive) or removed (negative).</param>
/// <param name="Edits">
/// The edits in the previous version's offsets, sorted and not overlapping; empty when the change is not described
/// edit by edit, such as an undo, so offset-based data must wait for its provider instead of being mapped.
/// </param>
public readonly record struct DocumentChange(int FirstLine, int LineDelta, IReadOnlyList<TextEdit> Edits);

/// <summary>
/// An editable document: the current buffer, a version that increases with every change, the selections and the
/// undo history. Buffers are immutable, so an undo step keeps the previous buffer instead of inverse edits.
/// </summary>
public sealed class TextDocument
{
    const double MergeSeconds = 1.5;

    readonly List<HistoryEntry> _undo = [];
    readonly List<HistoryEntry> _redo = [];
    TextSelection[] _selections = [TextSelection.At(0)];

    /// <summary>One undo step: the state before and after it, and the first line it touched.</summary>
    sealed record HistoryEntry(ITextBuffer Before, TextSelection[] SelectionsBefore, int PrimaryBefore, ITextBuffer After,
        TextSelection[] SelectionsAfter, int PrimaryAfter, EditKind Kind, DateTime Time, int FirstLine);

    /// <summary>Creates a document over a buffer.</summary>
    public TextDocument(ITextBuffer buffer) => Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));

    /// <summary>The current text.</summary>
    public ITextBuffer Buffer { get; private set; }

    /// <summary>Increases on every change to the text, including undo and redo.</summary>
    public int Version { get; private set; }

    /// <summary>The line break written when the document is saved, such as <c>\r\n</c>.</summary>
    public string LineBreak { get; set; } = "\n";

    /// <summary>The selections, sorted by start and never overlapping.</summary>
    public IReadOnlyList<TextSelection> Selections => _selections;

    /// <summary>Index of the selection the view follows, normally the last one added.</summary>
    public int PrimaryIndex { get; private set; }

    /// <summary>The selection the view follows.</summary>
    public TextSelection Primary => _selections[PrimaryIndex];

    /// <summary>Whether there is a step to undo.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Whether there is a step to redo.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Raised after the text changes, before the selections are read again.</summary>
    public event Action<DocumentChange>? Changed;

    /// <summary>The text with the saved line break restored.</summary>
    public string ToFileText() => LineBreak == "\n" ? Buffer.Text : Buffer.Text.Replace("\n", LineBreak);

    /// <summary>Replaces the whole text and clears the history, keeping the original line break for saving.</summary>
    public void Load(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        LineBreak = TextBufferText.DetectLineBreak(text);
        var previous = Buffer;
        Buffer = previous.Replace(0, previous.Length, text);
        _undo.Clear();
        _redo.Clear();
        Version++;
        _selections = [TextSelection.At(0)];
        PrimaryIndex = 0;
        Changed?.Invoke(new DocumentChange(0, Buffer.LineCount - previous.LineCount, []));
    }

    /// <summary>Sets the selections, clamping them to the text and merging any that overlap.</summary>
    /// <param name="selections">The new selections, in any order; at least one.</param>
    /// <param name="primary">Index in <paramref name="selections"/> of the one to follow, or -1 for the last.</param>
    public void SetSelections(IReadOnlyList<TextSelection> selections, int primary = -1)
    {
        ArgumentNullException.ThrowIfNull(selections);
        if (selections.Count == 0)
        {
            throw new ArgumentException("A document needs at least one selection.", nameof(selections));
        }

        var followed = selections[primary < 0 || primary >= selections.Count ? selections.Count - 1 : primary];
        (_selections, PrimaryIndex) = Normalize(selections, Clamp(followed));
    }

    /// <summary>
    /// Applies edits made against the current version as one change, then sets the selections.
    /// </summary>
    /// <param name="edits">Replacements in current offsets, in any order; overlapping ones are dropped.</param>
    /// <param name="selections">The selections afterwards, in the new version's offsets.</param>
    /// <param name="kind">How the change groups with the previous one for undo.</param>
    /// <param name="primary">Index of the selection to follow, or -1 for the last.</param>
    /// <returns>Whether the text changed.</returns>
    public bool Apply(IReadOnlyList<TextEdit> edits, IReadOnlyList<TextSelection> selections,
        EditKind kind = EditKind.Other, int primary = -1)
    {
        ArgumentNullException.ThrowIfNull(edits);
        var sorted = Sorted(edits);
        if (sorted.Count == 0)
        {
            SetSelections(selections, primary);
            return false;
        }

        var before = Buffer;
        var selectionsBefore = _selections;
        var primaryBefore = PrimaryIndex;
        var buffer = before;
        for (var i = sorted.Count - 1; i >= 0; i--)
        {
            buffer = buffer.Replace(sorted[i].Start, sorted[i].Length, sorted[i].Text);
        }

        Buffer = buffer;
        SetSelections(selections, primary);
        var firstLine = before.LineOf(sorted[0].Start);
        Record(new HistoryEntry(before, selectionsBefore, primaryBefore, buffer, _selections, PrimaryIndex, kind,
            DateTime.UtcNow, firstLine));
        Commit(before, firstLine, sorted);
        return true;
    }

    /// <summary>Restores the text and selections before the last step.</summary>
    /// <returns>Whether there was a step to undo.</returns>
    public bool Undo() => Step(_undo, _redo, undo: true);

    /// <summary>Applies the last undone step again.</summary>
    /// <returns>Whether there was a step to redo.</returns>
    public bool Redo() => Step(_redo, _undo, undo: false);

    /// <summary>Ends the current undo group, so the next edit starts a new step even if it would merge.</summary>
    public void BreakUndoGroup()
    {
        if (_undo.Count > 0)
        {
            _undo[^1] = _undo[^1] with { Kind = EditKind.Other };
        }
    }

    bool Step(List<HistoryEntry> from, List<HistoryEntry> to, bool undo)
    {
        if (from.Count == 0)
        {
            return false;
        }

        var entry = from[^1];
        from.RemoveAt(from.Count - 1);
        to.Add(entry with { Kind = EditKind.Other });
        var previous = Buffer;
        Buffer = undo ? entry.Before : entry.After;
        var selections = undo ? entry.SelectionsBefore : entry.SelectionsAfter;
        var primary = undo ? entry.PrimaryBefore : entry.PrimaryAfter;
        (_selections, PrimaryIndex) = Normalize(selections, selections[primary]);
        Commit(previous, entry.FirstLine, []);
        return true;
    }

    void Record(HistoryEntry entry)
    {
        _redo.Clear();
        if (_undo.Count > 0 && Merges(_undo[^1], entry))
        {
            _undo[^1] = _undo[^1] with
            {
                After = entry.After,
                SelectionsAfter = entry.SelectionsAfter,
                PrimaryAfter = entry.PrimaryAfter,
                Time = entry.Time,
                FirstLine = Math.Min(_undo[^1].FirstLine, entry.FirstLine)
            };
            return;
        }

        _undo.Add(entry);
    }

    static bool Merges(HistoryEntry previous, HistoryEntry next) =>
        next.Kind != EditKind.Other && previous.Kind == next.Kind
        && (next.Time - previous.Time).TotalSeconds < MergeSeconds
        && previous.SelectionsAfter.Length == next.SelectionsBefore.Length
        && previous.SelectionsAfter.AsSpan().SequenceEqual(next.SelectionsBefore);

    void Commit(ITextBuffer previous, int firstLine, IReadOnlyList<TextEdit> edits)
    {
        Version++;
        Changed?.Invoke(new DocumentChange(firstLine, Buffer.LineCount - previous.LineCount, edits));
    }

    /// <summary>The edits sorted by start, dropping any that overlap an earlier one or change nothing.</summary>
    List<TextEdit> Sorted(IReadOnlyList<TextEdit> edits)
    {
        var sorted = new List<TextEdit>(edits.Count);
        foreach (var edit in edits.OrderBy(edit => edit.Start).ThenBy(edit => edit.Length))
        {
            if (edit.Start < 0 || edit.End > Buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(edits), $"Edit {edit} is outside the text.");
            }

            if (ShouldSkipEdit(edit, sorted))
            {
                continue;
            }

            sorted.Add(edit);
        }

        return sorted;
    }

    /// <summary>Empty insertions and edits overlapping the last accepted edit do not change the document.</summary>
    static bool ShouldSkipEdit(TextEdit edit, List<TextEdit> sorted) =>
        (edit.Length == 0 && edit.Text.Length == 0) || (sorted.Count > 0 && edit.Start < sorted[^1].End);

    TextSelection Clamp(TextSelection selection) => new(Math.Clamp(selection.Anchor, 0, Buffer.Length),
        Math.Clamp(selection.Caret, 0, Buffer.Length), selection.Column);

    /// <summary>Sorts, clamps and merges selections; returns them with the index of the one to follow.</summary>
    (TextSelection[] Selections, int Primary) Normalize(IReadOnlyList<TextSelection> selections,
        TextSelection followed)
    {
        var sorted = selections.Select(Clamp).OrderBy(selection => selection.Start).ToList();
        var merged = new List<TextSelection>(sorted.Count);
        var primary = 0;
        foreach (var selection in sorted)
        {
            if (merged.Count > 0 && Overlaps(merged[^1], selection))
            {
                var last = merged[^1];
                var start = Math.Min(last.Start, selection.Start);
                var end = Math.Max(last.End, selection.End);
                merged[^1] = last.Caret >= last.Anchor ? new TextSelection(start, end) : new TextSelection(end, start);
            }
            else
            {
                merged.Add(selection);
            }

            if (selection == followed)
            {
                primary = merged.Count - 1;
            }
        }

        return ([.. merged], primary);
    }

    /// <summary>Whether two sorted selections share text, or touch where one of them is a bare caret.</summary>
    static bool Overlaps(TextSelection previous, TextSelection next) =>
        next.Start < previous.End || (next.Start == previous.End && (next.IsEmpty || previous.IsEmpty));
}
