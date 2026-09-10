namespace Guinevere;

/// <summary>
/// What the dock space needs to know about a panel to draw its tab.
/// </summary>
/// <param name="Title">The label shown on the tab.</param>
/// <param name="Closable">Whether the tab shows a close button.</param>
/// <param name="Icon">
/// Optional content drawn before the title, in a square of <see cref="DockTheme.TabIconSize"/> — a
/// glyph, a bitmap, or anything else. The resolver runs once per tab per pass, so hand back a cached
/// delegate rather than allocating one each call.
/// </param>
public readonly record struct DockPanelInfo(string Title, bool Closable = true, Action<Gui>? Icon = null);
