namespace Guinevere;

/// <summary>
/// The table's measurements, read from the <c>table-row</c>, <c>table-header</c> and <c>table-cell</c> rules (and the
/// <c>table</c> font size) where virtualization needs them before the nodes exist; each falls back to a default.
/// </summary>
readonly record struct TableMetrics(float RowHeight, float HeaderHeight, float CellPadding, float FontSize)
{
    /// <summary>The cell and header text size when the sheet sets none on the <c>table</c>.</summary>
    public const float DefaultFontSize = 12f;

    const float DefaultRowHeight = 24f;
    const float DefaultHeaderHeight = 28f;
    const float DefaultCellPadding = 8f;

    /// <summary>
    /// Reads the metrics for the current <c>table</c> node: <c>table-row</c> <c>height</c>, <c>table-header</c>
    /// <c>height</c> and <c>table-cell</c> <c>inset</c> (the space between a cell's edge and its content, on each
    /// side, for cells and header cells).
    /// </summary>
    public static TableMetrics Read(Gui gui, float fontSize) =>
        new(gui.ResolvePart("table-row").GetLength("height") ?? DefaultRowHeight,
            gui.ResolvePart("table-header").GetLength("height") ?? DefaultHeaderHeight,
            gui.ResolvePart("table-cell").GetLength("inset") ?? DefaultCellPadding,
            fontSize);
}
