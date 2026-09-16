namespace Guinevere;

/// <summary>
/// Builder for creating flyout menus
/// </summary>
public class FlyoutBuilder
{
    internal readonly List<FlyoutItem> Items = new();

    /// <summary>
    /// Adds a menu item with an action
    /// </summary>
    public FlyoutBuilder Item(string text, Action? action = null, string shortcut = "", bool enabled = true,
        Action? onHover = null)
    {
        Items.Add(new FlyoutItem
        {
            Text = text, Action = action, Shortcut = shortcut, Enabled = enabled, OnHover = onHover
        });
        return this;
    }

    /// <summary>
    /// Adds a menu item with a submenu
    /// </summary>
    public FlyoutBuilder Submenu(string text, Action<FlyoutBuilder> buildSubmenu, bool enabled = true)
    {
        var submenuBuilder = new FlyoutBuilder();
        buildSubmenu(submenuBuilder);

        Items.Add(new FlyoutItem { Text = text, Submenu = submenuBuilder.Items, Enabled = enabled });
        return this;
    }

    /// <summary>
    /// Adds a separator line
    /// </summary>
    public FlyoutBuilder Separator()
    {
        Items.Add(new FlyoutItem { IsSeparator = true });
        return this;
    }

    /// <summary>
    /// Adds a checkable menu item. The item shows a check when <paramref name="isChecked"/> returns
    /// true; activating it calls <paramref name="onChange"/> with the target value.
    /// </summary>
    public FlyoutBuilder CheckItem(string text, Func<bool> isChecked, Action<bool> onChange,
        string shortcut = "", bool enabled = true, Action? onHover = null)
    {
        ArgumentNullException.ThrowIfNull(isChecked);
        ArgumentNullException.ThrowIfNull(onChange);

        Items.Add(new FlyoutItem
        {
            Text = text,
            IsChecked = isChecked,
            OnCheckChanged = onChange,
            Shortcut = shortcut,
            Enabled = enabled,
            OnHover = onHover
        });
        return this;
    }
}
