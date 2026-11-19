namespace Guinevere;

/// <summary>
/// The payload a dragged dock tab carries. <see cref="Leaf"/> is the group it came from, which lets a
/// drop target tell a reorder inside one group from a move between two.
/// </summary>
/// <param name="PanelId">The panel being dragged.</param>
/// <param name="Leaf">The tab group the drag started in.</param>
public sealed record DockTabPayload(string PanelId, DockLeaf Leaf);
