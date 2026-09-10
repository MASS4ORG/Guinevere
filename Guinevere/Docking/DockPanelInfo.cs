namespace Guinevere;

/// <summary>
/// What the dock space needs to know about a panel to draw its tab.
/// </summary>
/// <param name="Title">The label shown on the tab.</param>
/// <param name="Closable">Whether the tab shows a close button.</param>
public readonly record struct DockPanelInfo(string Title, bool Closable = true);
