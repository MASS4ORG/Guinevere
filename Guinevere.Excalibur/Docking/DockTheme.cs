namespace Guinevere;

/// <summary>
/// The metrics a <see cref="DockLayout"/> is drawn with. Colors come from the GUI's sheets: the tab strip rules,
/// <c>dock-panel</c>, <c>dock-window</c>, <c>dock-grip</c>, <c>dock-empty</c>, <c>dock-ghost</c>,
/// <c>drop-indicator</c>, <c>drop-preview</c> and <c>splitter.dock</c>.
/// </summary>
public sealed class DockTheme
{
    /// <summary>Height of a tab strip.</summary>
    public float TabHeight { get; init; } = 26f;

    /// <summary>Font size for tab labels.</summary>
    public float FontSize { get; init; } = 12f;

    /// <summary>
    /// The square a tab's <see cref="DockPanelInfo.Icon"/> is drawn in. A strip's tabs all reserve the
    /// same width for it, so icons of differing sizes cannot make the row ragged.
    /// </summary>
    public float TabIconSize { get; init; } = 14f;

    /// <summary>Thickness of the splitter between two docked regions.</summary>
    public float SplitterThickness { get; init; } = 6f;

    /// <summary>How much of a region each edge drop zone covers.</summary>
    public float DropZoneFraction { get; init; } = 0.25f;

    /// <summary>The default metrics.</summary>
    public static DockTheme Default { get; } = new();

    /// <summary>Projects this theme onto the shared tab strip, so dock tabs and a host's own match.</summary>
    public TabStripTheme ToTabStripTheme() => new()
    {
        Height = TabHeight,
        FontSize = FontSize,
        IconSize = TabIconSize
    };
}
