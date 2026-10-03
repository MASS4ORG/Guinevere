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
    /// Adds a menu whose <paramref name="text"/> is shown in the bar. Clicking the title drops the
    /// menu below it.
    /// </summary>
    public MenuBarBuilder Menu(string text, Action<FlyoutBuilder> buildMenu)
    {
        ArgumentNullException.ThrowIfNull(buildMenu);

        var builder = new FlyoutBuilder();
        buildMenu(builder);
        Menus.Add(new MenuBarMenu(text, builder.Items));
        return this;
    }
}

/// <summary>One top-level menu: its bar title and the items that drop down below it.</summary>
sealed record MenuBarMenu(string Title, List<FlyoutItem> Items);
