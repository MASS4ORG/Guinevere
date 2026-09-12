namespace Guinevere;

/// <summary>
/// The editing state of one text field: the value being edited, where the caret sits, what is
/// selected and whether the caret is currently visible. Owned by <see cref="TextEditor"/> and read by
/// whichever control paints the field, so a skin never has to re-derive any of it.
/// </summary>
public sealed class TextEditState
{
    /// <summary>Creates an empty state.</summary>
    public TextEditState()
    {
    }

    /// <summary>Creates a state holding an initial value, with the caret at its end.</summary>
    /// <param name="text">The initial value.</param>
    public TextEditState(string text) => SetValue(text);

    /// <summary>The value being edited.</summary>
    public string Text { get; set; } = "";

    /// <summary>Caret offset into <see cref="Text"/>.</summary>
    public int CursorPosition { get; set; }

    /// <summary>
    /// Where the current selection was started. Equal to <see cref="CursorPosition"/> when nothing is
    /// selected.
    /// </summary>
    public int SelectionAnchor { get; set; }

    /// <summary>Whether the field holds keyboard focus.</summary>
    public bool IsFocused { get; set; }

    /// <summary>Set while the pointer is dragging out a selection.</summary>
    public bool IsSelecting { get; set; }

    /// <summary>Seconds since the caret last changed visibility.</summary>
    public float BlinkTimer { get; set; }

    /// <summary>Whether the caret is in its visible half of the blink.</summary>
    public bool ShowCursor { get; set; } = true;

    /// <summary>The last value the caller supplied, so an external change can be told from an edit.</summary>
    internal string External { get; set; } = "";

    /// <summary>Lower end of the selection.</summary>
    public int SelectionStart => Math.Min(SelectionAnchor, CursorPosition);

    /// <summary>Upper end of the selection.</summary>
    public int SelectionEnd => Math.Max(SelectionAnchor, CursorPosition);

    /// <summary>Whether any text is selected.</summary>
    public bool HasSelection => SelectionStart != SelectionEnd;

    /// <summary>The selected run, or an empty string when there is no selection.</summary>
    public string SelectedText => HasSelection
        ? Text[Math.Clamp(SelectionStart, 0, Text.Length)..Math.Clamp(SelectionEnd, 0, Text.Length)]
        : string.Empty;

    /// <summary>Moves the caret, extending the selection when <paramref name="extend"/> is set.</summary>
    /// <param name="position">Target offset; clamped into the text.</param>
    /// <param name="extend">True to keep the selection anchor, false to collapse the selection.</param>
    public void MoveTo(int position, bool extend)
    {
        CursorPosition = Math.Clamp(position, 0, Text.Length);
        if (!extend) SelectionAnchor = CursorPosition;

        ShowCursor = true;
        BlinkTimer = 0f;
    }

    /// <summary>Removes the selected run and leaves the caret where it was.</summary>
    /// <returns>True when something was removed.</returns>
    public bool DeleteSelection()
    {
        if (!HasSelection) return false;

        var start = Math.Clamp(SelectionStart, 0, Text.Length);
        var end = Math.Clamp(SelectionEnd, 0, Text.Length);

        Text = Text.Remove(start, end - start);
        CursorPosition = start;
        SelectionAnchor = start;
        return true;
    }

    /// <summary>Replaces the selection, or inserts at the caret when there is none.</summary>
    /// <param name="value">The text to insert.</param>
    public void Insert(string value)
    {
        DeleteSelection();
        Text = Text.Insert(Math.Clamp(CursorPosition, 0, Text.Length), value);
        MoveTo(CursorPosition + value.Length, extend: false);
    }

    /// <summary>
    /// Replaces the value and re-baselines what the caller counts as having supplied, so the next
    /// frame does not read the change back as an external edit. Leaves the caret at the end.
    /// </summary>
    /// <param name="text">The new value.</param>
    public void SetValue(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        Text = text;
        External = text;
        MoveTo(text.Length, extend: false);
    }

    /// <summary>Selects the whole value.</summary>
    public void SelectAll()
    {
        SelectionAnchor = 0;
        CursorPosition = Text.Length;
    }
}
