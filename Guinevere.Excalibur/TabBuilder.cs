namespace Guinevere;

/// <summary>Collects the tabs of a <c>Tabs</c>, <c>VerticalTabs</c> or <c>PillTabs</c> call.</summary>
public class TabBuilder
{
    readonly List<TabInfo> _tabs = [];

    /// <summary>Adds a tab.</summary>
    /// <param name="title">What the tab shows: text, an icon, a localized string or other widgets.</param>
    /// <param name="content">Builds the page shown while the tab is active.</param>
    /// <param name="enabled">Whether the tab can be picked.</param>
    /// <param name="closable">Whether the user may close the tab (middle click or the "×" button).</param>
    /// <param name="classes">Extra classes for this tab's <c>tab</c> node, to restyle it from a sheet.</param>
    /// <param name="key">Stable key reported on close and remembered while closed; defaults to the title's
    /// label, then to the tab's position. Give one when titles are not text or change at runtime.</param>
    /// <returns>This builder, for chaining.</returns>
    public TabBuilder Tab(Content title, Action? content = null, bool enabled = true,
        bool closable = false, IReadOnlyList<string>? classes = null, string? key = null)
    {
        _tabs.Add(new TabInfo
        {
            Title = title,
            Key = key,
            Content = content,
            Enabled = enabled,
            Closable = closable,
            Classes = classes
        });
        return this;
    }

    /// <summary>Adds a tab that cannot be picked.</summary>
    /// <param name="title">What the tab shows.</param>
    /// <param name="key">Stable key; see <see cref="Tab"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    public TabBuilder DisabledTab(Content title, string? key = null) => Tab(title, null, false, key: key);

    internal List<TabInfo> GetTabs() => [.. _tabs];
}
