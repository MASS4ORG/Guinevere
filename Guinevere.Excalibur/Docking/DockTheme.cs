namespace Guinevere;

/// <summary>
/// The colors and metrics a <see cref="DockLayout"/> is drawn with. A host that already has a
/// palette builds one of these from it rather than restyling every panel.
/// </summary>
public sealed class DockTheme
{
    /// <summary>The tab strip behind the tabs.</summary>
    public Color TabStrip { get; init; } = Color.FromArgb(255, 27, 30, 36);

    /// <summary>The active tab and the panel body.</summary>
    public Color Panel { get; init; } = Color.FromArgb(255, 35, 39, 46);

    /// <summary>An inactive tab.</summary>
    public Color Tab { get; init; } = Color.FromArgb(255, 30, 33, 39);

    /// <summary>A hovered tab, splitter or close button.</summary>
    public Color Hover { get; init; } = Color.FromArgb(255, 55, 60, 70);

    /// <summary>Borders and the splitter at rest.</summary>
    public Color Border { get; init; } = Color.FromArgb(255, 51, 56, 66);

    /// <summary>Tab and panel text.</summary>
    public Color Ink { get; init; } = Color.FromArgb(255, 215, 218, 224);

    /// <summary>Text on an inactive tab.</summary>
    public Color InkDim { get; init; } = Color.FromArgb(255, 139, 146, 156);

    /// <summary>The accent bar under the active tab, and the drop-zone highlight.</summary>
    public Color Accent { get; init; } = Color.FromArgb(255, 84, 143, 224);

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

    /// <summary>The default dark theme.</summary>
    public static DockTheme Dark { get; } = new();

    /// <summary>Projects this theme onto the shared tab strip, so dock tabs and a host's own match.</summary>
    public TabStripTheme ToTabStripTheme() => new()
    {
        Height = TabHeight,
        FontSize = FontSize,
        Strip = TabStrip,
        Active = Panel,
        Tab = Tab,
        Hover = Hover,
        Ink = Ink,
        InkDim = InkDim,
        Accent = Accent,
        IconSize = TabIconSize
    };
}
