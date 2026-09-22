namespace Guinevere;

/// <summary>Colors and metrics a <c>TabStrip</c> draws with.</summary>
public sealed class TabStripTheme
{
    /// <summary>Height of the strip and its tabs.</summary>
    public float Height { get; init; } = 26f;

    /// <summary>Label size.</summary>
    public float FontSize { get; init; } = 12f;

    /// <summary>Fill behind the tabs.</summary>
    public Color Strip { get; init; } = ControlPalette.Dark.BaseBackground;

    /// <summary>Fill of the active tab.</summary>
    public Color Active { get; init; } = ControlPalette.Dark.SurfaceActive;

    /// <summary>Fill of an inactive tab.</summary>
    public Color Tab { get; init; } = ControlPalette.Dark.Surface;

    /// <summary>Fill of a hovered tab or close button.</summary>
    public Color Hover { get; init; } = ControlPalette.Dark.SurfaceHover;

    /// <summary>Label color of the active tab.</summary>
    public Color Ink { get; init; } = ControlPalette.Dark.Text;

    /// <summary>Label color of an inactive tab.</summary>
    public Color InkDim { get; init; } = ControlPalette.Dark.TextDim;

    /// <summary>The bar marking the active tab, and the unsaved dot.</summary>
    public Color Accent { get; init; } = ControlPalette.Dark.Accent;

    /// <summary>Square reserved for a tab's icon.</summary>
    public float IconSize { get; init; } = 12f;

    /// <summary>The default dark theme, matching the dock space.</summary>
    public static TabStripTheme Default { get; } = new();

    /// <summary>Creates a tab-strip theme from a shared control palette.</summary>
    public static TabStripTheme FromPalette(ControlPalette palette) => new()
    {
        Strip = palette.BaseBackground,
        Active = palette.SurfaceActive,
        Tab = palette.Surface,
        Hover = palette.SurfaceHover,
        Ink = palette.Text,
        InkDim = palette.TextDim,
        Accent = palette.Accent
    };

    /// <summary>Creates a tab-strip theme from the values inherited by the current control scope.</summary>
    public static TabStripTheme FromStyle(ControlStyleValues style) => new()
    {
        Strip = style.BaseBackground,
        Active = style.SurfaceActive,
        Tab = style.Surface,
        Hover = style.SurfaceHover,
        Ink = style.Text,
        InkDim = style.TextDim,
        Accent = style.Accent
    };
}
