namespace Guinevere;

/// <summary>Colours and metrics a <c>TabStrip</c> draws with.</summary>
public sealed class TabStripTheme
{
    /// <summary>Height of the strip and its tabs.</summary>
    public float Height { get; init; } = 26f;

    /// <summary>Label size.</summary>
    public float FontSize { get; init; } = 12f;

    /// <summary>Fill behind the tabs.</summary>
    public Color Strip { get; init; } = Color.FromArgb(255, 27, 30, 36);

    /// <summary>Fill of the active tab.</summary>
    public Color Active { get; init; } = Color.FromArgb(255, 35, 39, 46);

    /// <summary>Fill of an inactive tab.</summary>
    public Color Tab { get; init; } = Color.FromArgb(255, 30, 33, 39);

    /// <summary>Fill of a hovered tab or close button.</summary>
    public Color Hover { get; init; } = Color.FromArgb(255, 55, 60, 70);

    /// <summary>Label colour of the active tab.</summary>
    public Color Ink { get; init; } = Color.FromArgb(255, 215, 218, 224);

    /// <summary>Label colour of an inactive tab.</summary>
    public Color InkDim { get; init; } = Color.FromArgb(255, 139, 146, 156);

    /// <summary>The bar marking the active tab, and the unsaved dot.</summary>
    public Color Accent { get; init; } = Color.FromArgb(255, 84, 143, 224);

    /// <summary>Square reserved for a tab's icon.</summary>
    public float IconSize { get; init; } = 12f;

    /// <summary>The default dark theme, matching the dock space.</summary>
    public static TabStripTheme Default { get; } = new();
}
