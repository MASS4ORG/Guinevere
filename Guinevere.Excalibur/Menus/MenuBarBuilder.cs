namespace Guinevere;

/// <summary>
/// Builder for a top-level menu bar: one <see cref="Menu"/> call per menu whose title appears in the
/// bar, with the menu's contents described by a <see cref="FlyoutBuilder"/>.
/// </summary>
public class MenuBarBuilder
{
    internal readonly List<MenuBarMenu> Menus = [];
    internal string? CollapsedLabel;

    /// <summary>Shows a compact toggle that reveals the menu titles while a menu is open.</summary>
    public MenuBarBuilder Collapsible(string label = "☰")
    {
        CollapsedLabel = label;
        return this;
    }

    /// <summary>
    /// Adds a menu whose <paramref name="title"/> is shown in the bar. Clicking the title drops the
    /// menu below it.
    /// </summary>
    /// <param name="title">What the bar shows for the menu; its label also keys the menu.</param>
    /// <param name="buildMenu">Fills the menu.</param>
    public MenuBarBuilder Menu(Content title, Action<FlyoutBuilder> buildMenu)
    {
        ArgumentNullException.ThrowIfNull(buildMenu);

        var builder = new FlyoutBuilder();
        buildMenu(builder);
        Menus.Add(new MenuBarMenu(title, FlyoutItem.KeyFor(title, Menus.Count), builder.Items));
        return this;
    }
}

/// <summary>One top-level menu: its bar title, its key and the items that drop down below it.</summary>
sealed record MenuBarMenu(Content Title, string Key, List<FlyoutItem> Items);
