namespace Guinevere;

/// <summary>
/// The part of a tab group's strip that the tabs do not use, handed to the host so it can put its own
/// controls there — a lock toggle, an overflow menu, a filter box.
/// </summary>
/// <param name="Leaf">The tab group this strip belongs to.</param>
/// <param name="ActivePanelId">The panel currently shown, or null if the group is empty.</param>
/// <param name="FreeArea">
/// The screen-space rect left over after the tabs. The callback already runs inside a node covering
/// this rect, so laying out children normally is enough; the rect is here for anyone who wants to
/// place content absolutely instead.
/// </param>
public readonly record struct DockTabStrip(DockLeaf Leaf, string? ActivePanelId, Rect FreeArea);
