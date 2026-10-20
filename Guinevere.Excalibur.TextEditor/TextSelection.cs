namespace Guinevere;

/// <summary>One caret and the end its selection was started from; equal ends mean no selection.</summary>
/// <param name="Anchor">The fixed end of the selection.</param>
/// <param name="Caret">The moving end, where the caret is drawn.</param>
/// <param name="Column">The visual column vertical moves aim for, or -1 to use the caret's own column.</param>
public readonly record struct TextSelection(int Anchor, int Caret, int Column = -1)
{
    /// <summary>The lower offset.</summary>
    public int Start => Math.Min(Anchor, Caret);

    /// <summary>The upper offset.</summary>
    public int End => Math.Max(Anchor, Caret);

    /// <summary>Whether nothing is selected.</summary>
    public bool IsEmpty => Anchor == Caret;

    /// <summary>Number of selected code units.</summary>
    public int Length => End - Start;

    /// <summary>A caret with no selection.</summary>
    public static TextSelection At(int offset) => new(offset, offset);

    /// <summary>This selection with the caret moved, keeping the anchor when <paramref name="extend"/> is set.</summary>
    public TextSelection MoveTo(int caret, bool extend, int column = -1) =>
        new(extend ? Anchor : caret, caret, column);
}

/// <summary>A replacement of a range in one buffer version.</summary>
/// <param name="Start">First replaced offset.</param>
/// <param name="Length">Number of replaced code units.</param>
/// <param name="Text">The inserted text.</param>
public readonly record struct TextEdit(int Start, int Length, string Text)
{
    /// <summary>Offset after the replaced range.</summary>
    public int End => Start + Length;

    /// <summary>How much the edit grows the text.</summary>
    public int Delta => Text.Length - Length;

    /// <summary>
    /// Where an offset of the edited version lands after a set of edits sorted by start that do not overlap.
    /// An offset inside a replaced range moves to the end of its replacement.
    /// </summary>
    public static int Map(int offset, IReadOnlyList<TextEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        var shift = 0;
        foreach (var edit in edits)
        {
            if (offset < edit.Start || (offset == edit.Start && edit.Length > 0))
            {
                break;
            }

            if (offset < edit.End)
            {
                return edit.Start + shift + edit.Text.Length;
            }

            shift += edit.Delta;
        }

        return offset + shift;
    }
}
