namespace Guinevere;

/// <summary>One tab in a <c>TabStrip</c>.</summary>
/// <param name="Id">Stable key, also used for the tab's layout node.</param>
/// <param name="Label">The text on the tab.</param>
/// <param name="Closable">Whether the tab shows a close affordance.</param>
/// <param name="Modified">Shows a dot instead of the close cross, the way editors mark unsaved work.</param>
/// <param name="Icon">Optional content drawn before the label.</param>
/// <param name="Tag">Whatever the caller needs handed back.</param>
public readonly record struct TabStripItem(
    string Id,
    string Label,
    bool Closable = true,
    bool Modified = false,
    Action<Gui>? Icon = null,
    object? Tag = null);
