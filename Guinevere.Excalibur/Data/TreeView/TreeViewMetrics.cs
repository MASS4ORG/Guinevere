namespace Guinevere;

/// <summary>
/// The tree's measurements, read from the <c>tree-row</c>, <c>tree-expander</c> and <c>tree-icon</c> rules (and the
/// <c>treeview</c> font size) where virtualization needs them before the nodes exist; each falls back to a default.
/// </summary>
readonly record struct TreeViewMetrics(float RowHeight, float IndentWidth, float ContentPadding,
    float ExpanderWidth, float IconSize, float FontSize)
{
    /// <summary>The label font size when the sheet sets none on the <c>treeview</c>.</summary>
    public const float DefaultFontSize = 12f;

    /// <summary>Pointer travel in pixels past which a held row is a drag rather than a click.</summary>
    public const float DragThreshold = 4f;

    const float DefaultRowHeight = 20f;
    const float DefaultIndentWidth = 14f;
    const float DefaultContentPadding = 6f;
    const float DefaultExpanderWidth = 10f;
    const float DefaultIconSize = 12f;

    /// <summary>
    /// Reads the metrics for the current <c>treeview</c> node: <c>tree-row</c> <c>height</c>, <c>indent</c> (per
    /// nesting level) and <c>inset</c> (left of the first level), <c>tree-expander</c> and <c>tree-icon</c>
    /// <c>width</c>.
    /// </summary>
    public static TreeViewMetrics Read(Gui gui, float fontSize)
    {
        var row = gui.ResolvePart("tree-row");
        return new TreeViewMetrics(
            row.GetLength("height") ?? DefaultRowHeight,
            row.GetLength("indent") ?? DefaultIndentWidth,
            row.GetLength("inset") ?? DefaultContentPadding,
            gui.ResolvePart("tree-expander").GetLength("width") ?? DefaultExpanderWidth,
            gui.ResolvePart("tree-icon").GetLength("width") ?? DefaultIconSize,
            fontSize);
    }
}
