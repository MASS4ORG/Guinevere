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

    /// <summary>
    /// How far the pointer may travel while a row is held and the gesture still counts as a click on
    /// it. Past this the press is a drag, and the row is left alone — an editor that shows something
    /// different for the selection would otherwise swap it away under the gesture.
    /// </summary>
    public float DragThreshold { get; init; } = 4f;

    /// <summary>The default theme.</summary>
    public static TreeViewTheme Default { get; } = new();
}
