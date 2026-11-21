namespace Guinevere;

/// <summary>
/// Helper class for building context menus
/// </summary>
public class ContextMenuBuilder
{
    internal readonly List<FlyoutItem> Items = [];

    /// <summary>
    /// Adds an item to the context menu with the specified content, action, and enabled state.
    /// </summary>
    /// <param name="content">What the item shows; its label also names it for assistive tools.</param>
    /// <param name="action">The action to be executed when the menu item is clicked.</param>
    /// <param name="enabled">Specifies whether the menu item is enabled. Defaults to <c>true</c>.</param>
    /// <returns>The current <c>ContextMenuBuilder</c> instance with the added item.</returns>
    public ContextMenuBuilder Item(Content content, Action action, bool enabled = true)
    {
        Items.Add(new FlyoutItem
        {
            Content = content,
            Key = FlyoutItem.KeyFor(content, Items.Count),
            Action = action,
            Enabled = enabled
        });
        return this;
    }

    /// <summary>
    /// Adds a separator to the context menu.
    /// </summary>
    /// <returns>The current <c>ContextMenuBuilder</c> instance with the added separator.</returns>
    public ContextMenuBuilder Separator()
    {
        Items.Add(new FlyoutItem { IsSeparator = true, Key = $"#{Items.Count}" });
        return this;
    }

    /// <summary>Adds a nested context menu with the same pointer tolerance as menu bars and flyouts.</summary>
    public ContextMenuBuilder Submenu(Content content, Action<ContextMenuBuilder> buildSubmenu, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(buildSubmenu);
        var builder = new ContextMenuBuilder();
        buildSubmenu(builder);
        Items.Add(new FlyoutItem
        {
            Content = content,
            Key = FlyoutItem.KeyFor(content, Items.Count),
            Submenu = builder.Items,
            Enabled = enabled
        });
        return this;
    }
}
