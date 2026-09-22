namespace Guinevere;

/// <summary>
/// The colors and metrics a <see cref="DockLayout"/> is drawn with. A host that already has a
/// palette builds one of these from it rather than restyling every panel.
/// </summary>
public sealed class DockTheme
{
    /// <summary>The tab strip behind the tabs.</summary>
    public Color TabStrip { get; init; } = ControlPalette.Dark.BaseBackground;

    /// <summary>The active tab and the panel body.</summary>
    public Color Panel { get; init; } = ControlPalette.Dark.SurfaceActive;

    /// <summary>An inactive tab.</summary>
    public Color Tab { get; init; } = ControlPalette.Dark.Surface;

    /// <summary>A hovered tab, splitter or close button.</summary>
    public Color Hover { get; init; } = ControlPalette.Dark.SurfaceHover;

    /// <summary>Borders and the splitter at rest.</summary>
    public Color Border { get; init; } = ControlPalette.Dark.Border;

    /// <summary>Tab and panel text.</summary>
    public Color Ink { get; init; } = ControlPalette.Dark.Text;

    /// <summary>Text on an inactive tab.</summary>
    public Color InkDim { get; init; } = ControlPalette.Dark.TextDim;

    /// <summary>The accent bar under the active tab, and the drop-zone highlight.</summary>
    public Color Accent { get; init; } = ControlPalette.Dark.Accent;

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

    /// <summary>Creates a dock theme from a shared control palette.</summary>
    public static DockTheme FromPalette(ControlPalette palette) => new()
    {
        TabStrip = palette.BaseBackground,
        Panel = palette.SurfaceActive,
        Tab = palette.Surface,
        Hover = palette.SurfaceHover,
        Border = palette.Border,
        Ink = palette.Text,
        InkDim = palette.TextDim,
        Accent = palette.Accent
    };

    /// <summary>Creates a dock theme from the values inherited by the current control scope.</summary>
    public static DockTheme FromStyle(ControlStyleValues style) => new()
    {
        TabStrip = style.BaseBackground,
        Panel = style.SurfaceActive,
        Tab = style.Surface,
        Hover = style.SurfaceHover,
        Border = style.Border,
        Ink = style.Text,
        InkDim = style.TextDim,
        Accent = style.Accent
    };

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
