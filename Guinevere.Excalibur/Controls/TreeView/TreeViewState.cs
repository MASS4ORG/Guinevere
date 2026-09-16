namespace Guinevere;

/// <summary>
/// What a tree remembers between frames: which rows are collapsed, which is selected, and the
/// viewport the last frame measured.
/// </summary>
public sealed class TreeViewState
{
    private readonly HashSet<string> _collapsed = [];
    private readonly HashSet<string> _expanded = [];

    /// <summary>The selected row's id, or null.</summary>
    public string? SelectedId { get; set; }

    /// <summary>
    /// How deep the tree opens before the user touches it. Rows at or below this depth start open,
    /// deeper ones start closed; one means only the roots are open. Rows the user has folded or
    /// unfolded keep their own state regardless.
    /// </summary>
    public int DefaultExpandedDepth { get; set; } = 1;

    /// <summary>Whether a row is collapsed, taking <see cref="DefaultExpandedDepth"/> into account.</summary>
    /// <param name="id">The row id.</param>
    /// <param name="depth">The row's nesting level.</param>
    public bool IsCollapsed(string id, int depth)
    {
        if (_collapsed.Contains(id)) return true;
        if (_expanded.Contains(id)) return false;

        return depth >= DefaultExpandedDepth;
    }

    /// <summary>Whether a row was explicitly collapsed by the user.</summary>
    /// <param name="id">The row id.</param>
    public bool IsCollapsed(string id) => _collapsed.Contains(id);

    /// <summary>Collapses an expanded row, or expands a collapsed one.</summary>
    /// <param name="id">The row id.</param>
    /// <param name="depth">The row's nesting level, for the default state.</param>
    public void Toggle(string id, int depth) => SetExpanded(id, IsCollapsed(id, depth));

    /// <summary>Collapses an expanded row, or expands a collapsed one.</summary>
    /// <param name="id">The row id.</param>
    public void Toggle(string id) => Toggle(id, 0);

    /// <summary>Sets a row's expansion explicitly.</summary>
    /// <param name="id">The row id.</param>
    /// <param name="expanded">True to expand.</param>
    public void SetExpanded(string id, bool expanded)
    {
        _collapsed.Remove(id);
        _expanded.Remove(id);

        if (expanded) _expanded.Add(id);
        else _collapsed.Add(id);
    }

    /// <summary>The row being renamed in place, or null when nothing is being edited.</summary>
    public string? EditingId { get; private set; }

    /// <summary>The text in the rename box, kept here so the tree can rebuild its rows freely.</summary>
    public string EditingText { get; set; } = "";

    /// <summary>Starts an inline rename.</summary>
    /// <param name="id">The row to edit.</param>
    /// <param name="initialText">The value the box opens with, normally the row's label.</param>
    public void BeginRename(string id, string initialText)
    {
        EditingId = id;
        EditingText = initialText;
    }

    /// <summary>Ends an inline rename without committing it.</summary>
    public void CancelRename()
    {
        EditingId = null;
        EditingText = "";
    }

    /// <summary>
    /// Asks the next frame to scroll <see cref="SelectedId"/> into view. The caller is responsible for
    /// expanding the row's ancestors first; a row hidden under a collapsed parent cannot be scrolled to.
    /// </summary>
    public void Reveal() => WantsReveal = true;

    /// <summary>Expands every row, whatever its depth.</summary>
    public void ExpandAll()
    {
        _collapsed.Clear();
        _expanded.Clear();
        DefaultExpandedDepth = int.MaxValue;
    }

    /// <summary>
    /// Scroll offset and viewport height, sampled once per frame. Both passes of a frame must agree on
    /// which rows are visible, and the live scroll state only settles during the render pass.
    /// </summary>
    internal float FrameScrollY { get; set; }

    /// <summary>Set when a row was clicked, so the tree claims focus in its own scope.</summary>
    internal bool WantsFocus { get; set; }

    /// <summary>Set by <see cref="Reveal"/>, cleared once the tree has scrolled the selection into view.</summary>
    internal bool WantsReveal { get; set; }

    /// <summary>The viewport height the virtualisation used this frame.</summary>
    internal float FrameViewportHeight { get; set; } = 600f;

}
