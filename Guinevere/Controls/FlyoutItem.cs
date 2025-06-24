namespace Guinevere;

/// <summary>
/// Represents a flyout menu item
/// </summary>
public class FlyoutItem
{
    /// <summary>
    /// Gets or sets the display text of the menu item
    /// </summary>
    public string Text { get; set; } = "";

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
    /// Gets whether this item has a submenu
    /// </summary>
    public bool HasSubmenu => Submenu != null && Submenu.Count > 0;
}