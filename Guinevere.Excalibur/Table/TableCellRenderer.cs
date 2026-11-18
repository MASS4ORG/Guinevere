using System.Globalization;

namespace Guinevere;

/// <summary>What a cell renderer is told about the cell it draws.</summary>
/// <param name="Row">The row index in the source.</param>
/// <param name="Column">The column the cell belongs to.</param>
/// <param name="Value">The source's value for the cell.</param>
/// <param name="FontSize">The table's text size.</param>
/// <param name="Width">The cell's width, padding included.</param>
/// <param name="Height">The cell's height.</param>
public readonly record struct TableCellContext(
    int Row,
    TableColumn Column,
    object? Value,
    float FontSize,
    float Width,
    float Height);

/// <summary>
/// Draws the content of one table cell. The table builds the cell node, sizes and clips it and aligns its
/// content, so a renderer only adds what goes inside: text, a badge, a progress bar, an editor.
/// </summary>
public interface ITableCellRenderer
{
    /// <summary>Builds the cell's content in the current node; called in both passes for cells in view.</summary>
    /// <param name="gui">The GUI, positioned inside the cell node.</param>
    /// <param name="cell">The cell being drawn.</param>
    void Render(Gui gui, in TableCellContext cell);
}

/// <summary>Draws a cell's value as text, which is what a column without a renderer gets.</summary>
sealed class TableTextRenderer : ITableCellRenderer
{
    public static TableTextRenderer Instance { get; } = new();

    public void Render(Gui gui, in TableCellContext cell) =>
        gui.DrawText(Convert.ToString(cell.Value, CultureInfo.CurrentCulture) ?? "", cell.FontSize,
            centerInRect: false, clip: true);
}
