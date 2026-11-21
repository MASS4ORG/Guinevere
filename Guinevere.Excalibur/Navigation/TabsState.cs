namespace Guinevere;

/// <summary>Per-call-site state of a tab container.</summary>
class TabsState
{
    /// <summary>The active tab after this frame's input.</summary>
    public int ActiveTabIndex { get; set; }

    /// <summary>The tabs built this frame, without the closed ones.</summary>
    public List<TabInfo> Tabs { get; set; } = [];

    /// <summary>Keys of the tabs the user closed. Filtered out of the rebuilt tab list every frame.</summary>
    public HashSet<string> Closed { get; } = [];

    /// <summary>
    /// Set by the tab bar when a tab was closed. Consumed and cleared by the <c>Tabs</c> call that rendered it,
    /// which moves the tab into <see cref="Closed"/>.
    /// </summary>
    public (int Index, string Key)? TabToClose { get; set; }

    /// <summary>
    /// Set when a tab closed, so the next layout pass ignores a selection delivered for the same click: tabs shift
    /// after a close, and the activation would land on the closed tab's neighbor.
    /// </summary>
    public bool SuppressActivation { get; set; }

    /// <summary>Keyboard selection detected during render and applied before the next layout pass.</summary>
    public int? RequestedActiveTabIndex { get; set; }
}
