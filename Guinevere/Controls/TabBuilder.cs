namespace Guinevere;

/// <summary>
/// Helper class for building tabs
/// </summary>
public class TabBuilder
{
    private readonly List<TabInfo> _tabs = [];

    /// <summary>
    /// Adds a new tab with the specified configuration options to the current tab list.
    /// </summary>
    /// <param name="title">The title of the tab to be displayed.</param>
    /// <param name="content">The action that defines the content to be displayed within the tab.</param>
    /// <param name="enabled">A value indicating whether the tab is enabled or disabled. Defaults to true.</param>
    /// <param name="backgroundColor">The background color of the tab. Defaults to null.</param>
    /// <param name="textColor">The text color of the tab. Defaults to null.</param>
    /// <returns>Returns the current TabBuilder instance with the newly added tab, allowing for further configuration.</returns>
    public TabBuilder Tab(string title, Action? content = null, bool enabled = true,
        Color? backgroundColor = null, Color? textColor = null)
    {
        _tabs.Add(new TabInfo
        {
            Title = title,
            Content = content,
            Enabled = enabled,
            BackgroundColor = backgroundColor,
            TextColor = textColor
        });
        return this;
    }

    /// <summary>
    /// Adds a new tab to the current tab list with the specified title and content.
    /// </summary>
    /// <param name="title">The title of the tab to be displayed.</param>
    /// <param name="content">The action that defines the content to be displayed within the tab.</param>
    /// <returns>A TabBuilder instance with the tab added, enabling further configuration.</returns>
    public TabBuilder Tab(string title, Action content)
    {
        return Tab(title, content, true);
    }

    /// <summary>
    /// Adds a new disabled tab to the current tab list.
    /// </summary>
    /// <param name="title">The title displayed on the tab.</param>
    /// <returns>A TabBuilder instance with the disabled tab added, allowing for further configuration.</returns>
    public TabBuilder DisabledTab(string title)
    {
        return Tab(title, null, false);
    }

    internal List<TabInfo> GetTabs()
    {
        return [.._tabs];
    }
}
