namespace Guinevere;

/// <summary>
/// What a tree remembers between frames: which rows are collapsed, which is selected, and the
/// viewport the last frame measured.
/// </summary>
public sealed class TreeViewState
{
    private readonly HashSet<string> collapsed = [];

    /// <summary>The selected row's id, or null.</summary>
    public string? SelectedId { get; set; }

    /// <summary>Whether a row is collapsed.</summary>
    /// <param name="id">The row id.</param>
    public bool IsCollapsed(string id) => collapsed.Contains(id);

    /// <summary>Collapses an expanded row, or expands a collapsed one.</summary>
    /// <param name="id">The row id.</param>
    public void Toggle(string id)
    {
        if (!collapsed.Add(id)) collapsed.Remove(id);
    }

    /// <summary>Sets a row's expansion explicitly.</summary>
    /// <param name="id">The row id.</param>
    /// <param name="expanded">True to expand.</param>
    public void SetExpanded(string id, bool expanded)
    {
        if (expanded) collapsed.Remove(id);
        else collapsed.Add(id);
    }

    /// <summary>Expands every row.</summary>
    public void ExpandAll() => collapsed.Clear();

    /// <summary>
    /// Scroll offset and viewport height, sampled once per frame. Both passes of a frame must agree on
    /// which rows are visible, and the live scroll state only settles during the render pass.
    /// </summary>
    internal float FrameScrollY { get; set; }

    /// <summary>The viewport height the virtualisation used this frame.</summary>
    internal float FrameViewportHeight { get; set; } = 600f;

}
