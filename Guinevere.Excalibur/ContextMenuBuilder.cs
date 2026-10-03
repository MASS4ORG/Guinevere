namespace Guinevere;

/// <summary>
/// Helper class for building context menus
/// </summary>
public class ContextMenuBuilder
{
    internal readonly List<FlyoutItem> Items = [];

    /// <summary>
    /// Adds an item to the context menu with the specified text, action, and enabled state.
    /// </summary>
    /// <param name="text">The displayed text of the menu item.</param>
    /// <param name="action">The action to be executed when the menu item is clicked.</param>
    /// <param name="enabled">Specifies whether the menu item is enabled. Defaults to <c>true</c>.</param>
    /// <returns>The current <c>ContextMenuBuilder</c> instance with the added item.</returns>
    public ContextMenuBuilder Item(string text, Action action, bool enabled = true)
    {
        Items.Add(new FlyoutItem { Text = text, Action = action, Enabled = enabled });
        return this;
    }

    /// <summary>
    /// Adds a separator to the context menu.
    /// </summary>
    /// <returns>The current <c>ContextMenuBuilder</c> instance with the added separator.</returns>
    public ContextMenuBuilder Separator()
    {
        Items.Add(new FlyoutItem { IsSeparator = true });
        return this;
    }

    /// <summary>Adds a nested context menu with the same pointer tolerance as menu bars and flyouts.</summary>
    public ContextMenuBuilder Submenu(string text, Action<ContextMenuBuilder> buildSubmenu, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(buildSubmenu);
        var builder = new ContextMenuBuilder();
        buildSubmenu(builder);
        Items.Add(new FlyoutItem { Text = text, Submenu = builder.Items, Enabled = enabled });
        return this;
    }
}
