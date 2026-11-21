namespace Guinevere;

/// <summary>
/// Builder for creating flyout menus
/// </summary>
public class FlyoutBuilder
{
    internal readonly List<FlyoutItem> Items = [];

    /// <summary>
    /// Adds a menu item with an action
    /// </summary>
    /// <param name="content">What the item shows; its label also names it for assistive tools.</param>
    /// <param name="action">Runs when the item is activated.</param>
    /// <param name="shortcut">Shortcut text shown at the end of the row.</param>
    /// <param name="enabled">Whether the item can be activated.</param>
    /// <param name="onHover">Called every frame the pointer rests on the row.</param>
    public FlyoutBuilder Item(Content content, Action? action = null, string shortcut = "", bool enabled = true,
        Action? onHover = null)
    {
        Items.Add(new FlyoutItem
        {
            Content = content,
            Key = FlyoutItem.KeyFor(content, Items.Count),
            Action = action,
            Shortcut = shortcut,
            Enabled = enabled,
            OnHover = onHover
        });
        return this;
    }

    /// <summary>
    /// Adds a menu item with a submenu
    /// </summary>
    public FlyoutBuilder Submenu(Content content, Action<FlyoutBuilder> buildSubmenu, bool enabled = true)
    {
        var submenuBuilder = new FlyoutBuilder();
        buildSubmenu(submenuBuilder);

        Items.Add(new FlyoutItem
        {
            Content = content,
            Key = FlyoutItem.KeyFor(content, Items.Count),
            Submenu = submenuBuilder.Items,
            Enabled = enabled
        });
        return this;
    }

    /// <summary>
    /// Adds a separator line
    /// </summary>
    public FlyoutBuilder Separator()
    {
        Items.Add(new FlyoutItem { IsSeparator = true, Key = $"#{Items.Count}" });
        return this;
    }

    /// <summary>
    /// Adds a checkable menu item. The item shows a check when <paramref name="isChecked"/> returns
    /// true; activating it calls <paramref name="onChange"/> with the target value.
    /// </summary>
    public FlyoutBuilder CheckItem(Content content, Func<bool> isChecked, Action<bool> onChange,
        string shortcut = "", bool enabled = true, Action? onHover = null)
    {
        ArgumentNullException.ThrowIfNull(isChecked);
        ArgumentNullException.ThrowIfNull(onChange);

        Items.Add(new FlyoutItem
        {
            Content = content,
            Key = FlyoutItem.KeyFor(content, Items.Count),
            IsChecked = isChecked,
            OnCheckChanged = onChange,
            Shortcut = shortcut,
            Enabled = enabled,
            OnHover = onHover
        });
        return this;
    }
}
