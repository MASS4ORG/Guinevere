namespace Guinevere;

/// <summary>
/// Helper class for building context menus
/// </summary>
public class ContextMenuBuilder
{
    internal readonly List<ContextMenuItem> Items = new();

    /// <summary>
    /// Adds an item to the context menu with the specified text, action, and enabled state.
    /// </summary>
    /// <param name="text">The displayed text of the menu item.</param>
    /// <param name="action">The action to be executed when the menu item is clicked.</param>
    /// <param name="enabled">Specifies whether the menu item is enabled. Defaults to <c>true</c>.</param>
    /// <returns>The current <c>ContextMenuBuilder</c> instance with the added item.</returns>
    public ContextMenuBuilder Item(string text, Action action, bool enabled = true)
    {
        Items.Add(new ContextMenuItem { Text = text, Action = action, Enabled = enabled });
        return this;
    }

    /// <summary>
    /// Adds a separator to the context menu.
    /// </summary>
    /// <returns>The current <c>ContextMenuBuilder</c> instance with the added separator.</returns>
    public ContextMenuBuilder Separator()
    {
        Items.Add(new ContextMenuItem { Text = "---", IsSeparator = true });
        return this;
    }

    internal float CalculateWidth()
    {
        var font = new SKFont { Size = 12 };
        var maxWidth = 0f;

        foreach (var item in Items.Where(i => !i.IsSeparator))
        {
            font.MeasureText(item.Text, out var textBounds);
            maxWidth = Math.Max(maxWidth, textBounds.Width + 16); // 16 for padding
        }

        return maxWidth;
    }
}
