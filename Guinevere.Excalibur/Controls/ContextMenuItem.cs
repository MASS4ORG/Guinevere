namespace Guinevere;

/// <summary>
/// Represents a context menu item
/// </summary>
public class ContextMenuItem
{
    /// <summary>
    /// Gets or sets the display text of the <c>ContextMenuItem</c>.
    /// This text is presented to the user as the label for the menu item.
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Gets or sets the action to be executed when this <c>ContextMenuItem</c> is clicked.
    /// This property defines the behavior or response triggered by interacting with the item.
    /// </summary>
    public Action? Action { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this <c>ContextMenuItem</c> is enabled.
    /// An enabled item can be interacted with, while a disabled item is typically visually
    /// distinct and non-interactive.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether this <c>ContextMenuItem</c> represents a separator.
    /// A separator is a visual divider within a context menu typically used to group items.
    /// </summary>
    public bool IsSeparator { get; set; }
}
