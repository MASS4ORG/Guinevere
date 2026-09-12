namespace Guinevere;

/// <summary>
/// What a tree remembers between frames: which rows are collapsed, which is selected, and the
/// viewport the last frame measured.
/// </summary>
public sealed class TreeViewState
{
    private readonly HashSet<string> collapsed = [];
    private readonly HashSet<string> expanded = [];

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
        if (collapsed.Contains(id)) return true;
        if (expanded.Contains(id)) return false;

        return depth >= DefaultExpandedDepth;
    }

    /// <summary>Whether a row was explicitly collapsed by the user.</summary>
    /// <param name="id">The row id.</param>
    public bool IsCollapsed(string id) => collapsed.Contains(id);

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
        collapsed.Remove(id);
        this.expanded.Remove(id);

        if (expanded) this.expanded.Add(id);
        else collapsed.Add(id);
    }

    /// <summary>Expands every row, whatever its depth.</summary>
    public void ExpandAll()
    {
        collapsed.Clear();
        expanded.Clear();
        DefaultExpandedDepth = int.MaxValue;
    }

    /// <summary>
    /// Scroll offset and viewport height, sampled once per frame. Both passes of a frame must agree on
    /// which rows are visible, and the live scroll state only settles during the render pass.
    /// </summary>
    internal float FrameScrollY { get; set; }

    /// <summary>Set when a row was clicked, so the tree claims focus in its own scope.</summary>
    internal bool WantsFocus { get; set; }

    /// <summary>The viewport height the virtualisation used this frame.</summary>
    internal float FrameViewportHeight { get; set; } = 600f;

}
