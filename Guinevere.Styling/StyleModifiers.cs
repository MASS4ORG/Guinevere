namespace Guinevere;

/// <summary>
/// Standard semantic modifiers that controls pass to <c>gui.StyledNode</c> or <c>gui.ResolveStyle</c> and sheets
/// match as <c>:name</c>. Built-in states (<c>:hover</c>, <c>:active</c>, <c>:focus</c>, <c>:disabled</c>) are
/// <see cref="StyleState"/> flags instead.
/// </summary>
public static class StyleModifiers
{
    /// <summary>A checkbox, toggle, radio button or checkable menu item is on.</summary>
    public const string Checked = "checked";

    /// <summary>A checkbox stands for a mix of on and off values, such as a multi-selection.</summary>
    public const string Mixed = "mixed";

    /// <summary>A row, tab, chip or list item is part of the current selection.</summary>
    public const string Selected = "selected";

    /// <summary>A dropdown, menu, flyout, popup or tree branch is expanded.</summary>
    public const string Open = "open";

    /// <summary>The element is the source of an active drag.</summary>
    public const string Dragging = "dragging";
}
