namespace Guinevere;

/// <summary>
/// Represents a flyout menu item
/// </summary>
public class FlyoutItem
{
    /// <summary>
    /// Gets or sets what the menu item shows: text, an icon, other widgets or a custom type.
    /// </summary>
    public Content Content { get; set; }

    /// <summary>
    /// Gets or sets the item's stable id within its menu: the content's label, else its position. It names the item's
    /// submenu and persistent state, so it should survive translating or restyling the content.
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>The content's readable text, used for width estimates and semantics; empty without one.</summary>
    internal string Label => Content.Label ?? "";

    /// <summary>
    /// Gets or sets the action to execute when clicked
    /// </summary>
    public Action? Action { get; set; }

    /// <summary>
    /// Gets or sets the submenu items
    /// </summary>
    public List<FlyoutItem>? Submenu { get; set; }

    /// <summary>
    /// Gets or sets the keyboard shortcut text
    /// </summary>
    public string Shortcut { get; set; } = "";

    /// <summary>
    /// Gets or sets whether the item is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this is a separator
    /// </summary>
    public bool IsSeparator { get; set; }

    /// <summary>
    /// Reads whether a checkable item is currently checked. When set, the item renders a check column.
    /// </summary>
    public Func<bool>? IsChecked { get; set; }

    /// <summary>
    /// Receives the target value when a checkable item is activated.
    /// </summary>
    public Action<bool>? OnCheckChanged { get; set; }

    /// <summary>
    /// Called every frame the pointer rests on the row, for an item that previews what choosing it
    /// would do. The row has no "stopped hovering" event: a caller that needs one treats a frame
    /// without a call as the end of the hover.
    /// </summary>
    public Action? OnHover { get; set; }

    /// <summary>
    /// Gets whether this item has a submenu
    /// </summary>
    public bool HasSubmenu => Submenu is { Count: > 0 };

    /// <summary>The label of <paramref name="content"/>, or the position among the <paramref name="count"/> earlier items.</summary>
    internal static string KeyFor(Content content, int count) => content.Label is { Length: > 0 } label ? label : $"#{count}";
}
