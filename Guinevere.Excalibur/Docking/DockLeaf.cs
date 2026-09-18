namespace Guinevere;

/// <summary>
/// A tab group: one or more panels sharing a region, one of them active.
/// </summary>
public sealed class DockLeaf : DockNode
{
    /// <summary>
    /// Creates a leaf holding the given panels, with the first one active.
    /// </summary>
    /// <param name="panelIds">The panels in tab order.</param>
    public DockLeaf(params string[] panelIds) => PanelIds.AddRange(panelIds);

    /// <summary>
    /// Which edge of the layout this group was seeded against, or <see cref="DockZone.Center"/> for
    /// the main region. Purely a seeding hint: it tells <see cref="DockLayout.DockAtEdge"/> and
    /// <see cref="DockLayout.EnsurePanel"/> which existing group a newly registered or reopened panel
    /// belongs with. Where the group has since been dragged does not change it.
    /// </summary>
    public DockZone Zone { get; set; } = DockZone.Center;

    /// <summary>
    /// The panels in this group, in tab order.
    /// </summary>
    public List<string> PanelIds { get; } = [];

    /// <summary>
    /// The index into <see cref="PanelIds"/> of the panel currently shown. Reading it clamps to a
    /// valid tab, so removing the active panel cannot leave the group showing nothing.
    /// </summary>
    public int ActiveIndex
    {
        get => PanelIds.Count == 0 ? 0 : Math.Clamp(_activeIndex, 0, PanelIds.Count - 1);
        set => _activeIndex = value;
    }

    int _activeIndex;

    /// <summary>
    /// The panel currently shown, or null when the group is empty.
    /// </summary>
    public string? ActivePanelId => PanelIds.Count == 0 ? null : PanelIds[ActiveIndex];
}
