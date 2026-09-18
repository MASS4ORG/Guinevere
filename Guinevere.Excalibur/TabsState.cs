namespace Guinevere;

class TabsState
{
    public int ActiveTabIndex { get; set; }
    public List<TabInfo> Tabs { get; set; } = [];
    public float TabBarHeight { get; set; } = 32;

    /// <summary>Titles the user closed with a middle-click. Filtered out of the rebuilt tab list every frame.</summary>
    public HashSet<string> Closed { get; } = [];

    /// <summary>
    /// Set by the tab bar when a tab was middle-clicked. Consumed and cleared by the <c>Tabs</c>
    /// call that rendered it, which moves the tab into <see cref="Closed"/>.
    /// </summary>
    public (int Index, string Title)? TabToClose { get; set; }
}
