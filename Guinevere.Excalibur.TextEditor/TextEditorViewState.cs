namespace Guinevere;

/// <summary>What the editor control remembers between frames that is not part of the editing state.</summary>
sealed class TextEditorViewState
{
    /// <summary>Seconds since the caret last changed visibility.</summary>
    internal double BlinkTimer;

    /// <summary>Whether the caret is in its visible half of the blink.</summary>
    internal bool CaretVisible = true;

    /// <summary>Whether the pointer is dragging out a selection.</summary>
    internal bool Selecting;

    /// <summary>Whether the pointer is dragging the scrollbar thumb, and where on the thumb it grabbed.</summary>
    internal float? ThumbGrab;

    /// <summary>The primary caret's rectangle relative to the editor node, for popups.</summary>
    internal Rect CaretRect;

    /// <summary>Where the pointer last moved, and when, for hover delays.</summary>
    internal Vector2 PointerAt = new(float.NaN);

    /// <summary>Clock time of the last pointer move.</summary>
    internal double PointerMovedAt;

    /// <summary>The widest line drawn, so horizontal scrolling has an extent.</summary>
    internal float WidestLine;

    /// <summary>Whether the completion list is drawn this frame; fixed in the build pass so both passes agree.</summary>
    internal bool ShowCompletion;

    /// <summary>The completion items drawn this frame.</summary>
    internal IReadOnlyList<CompletionItem> CompletionItems = [];

    /// <summary>The highlighted completion drawn this frame.</summary>
    internal int CompletionSelected;

    /// <summary>Whether the hover box is drawn this frame; fixed in the build pass.</summary>
    internal string? HoverText;

    /// <summary>Where the hover box is drawn, relative to the editor node.</summary>
    internal Vector2 HoverAt;

    /// <summary>Whether the find bar is drawn this frame; fixed in the build pass.</summary>
    internal bool ShowFind;

    /// <summary>Whether the find bar shows its replace field this frame.</summary>
    internal bool ShowReplace;

    /// <summary>Set when the find bar opens, so its query field takes focus.</summary>
    internal bool FocusFind;

    /// <summary>The bracket pair at the primary caret, cached per version and caret.</summary>
    internal (int Version, int Caret, (int Open, int Close)? Pair) Brackets = (-1, -1, null);
}
