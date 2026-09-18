namespace Guinevere;

/// <summary>Colors and metrics a <c>TreeView</c> draws with.</summary>
public sealed class TreeViewTheme
{
    /// <summary>Height of one row.</summary>
    public float RowHeight { get; init; } = 20f;

    /// <summary>Horizontal step per nesting level.</summary>
    public float IndentWidth { get; init; } = 14f;

    /// <summary>Label font size.</summary>
    public float FontSize { get; init; } = 12f;

    /// <summary>Width reserved for the expander arrow.</summary>
    public float ExpanderWidth { get; init; } = 10f;

    /// <summary>Square reserved for a row's icon.</summary>
    public float IconSize { get; init; } = 12f;

    /// <summary>Space left of the first indent level, so rows do not touch the panel edge.</summary>
    public float ContentPadding { get; init; } = 6f;

    /// <summary>Label color for the selected row.</summary>
    public Color Ink { get; init; } = Color.FromArgb(255, 215, 218, 224);

    /// <summary>Label color for an unselected row.</summary>
    public Color InkDim { get; init; } = Color.FromArgb(255, 139, 146, 156);

    /// <summary>Background of a hovered row.</summary>
    public Color Hover { get; init; } = Color.FromArgb(60, 120, 140, 180);

    /// <summary>Background of the selected row.</summary>
    public Color Selected { get; init; } = Color.FromArgb(120, 84, 143, 224);

    /// <summary>The default theme.</summary>
    public static TreeViewTheme Default { get; } = new();
}
