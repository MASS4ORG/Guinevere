namespace Guinevere;

/// <summary>What a cell renderer is told about the cell it draws.</summary>
/// <typeparam name="TRow">The type of a row.</typeparam>
/// <typeparam name="TValue">The type of the cell's value.</typeparam>
/// <param name="Index">The row index in the source.</param>
/// <param name="Item">The row, as the source's indexer returned it.</param>
/// <param name="Value">The column's value read from the row.</param>
/// <param name="Column">The column the cell belongs to.</param>
/// <param name="FontSize">The table's text size.</param>
/// <param name="Width">The cell's width, padding included.</param>
/// <param name="Height">The cell's height.</param>
/// <param name="Source">The source the row comes from.</param>
/// <param name="State">The table's state, which row-level state such as expansion is kept in.</param>
public readonly record struct TableCellContext<TRow, TValue>(
    int Index,
    TRow Item,
    TValue Value,
    TableColumn Column,
    float FontSize,
    float Width,
    float Height,
    ITableViewSource<TRow> Source,
    TableState State)
{
    /// <summary>
    /// The stable key of the cell's row, read from the source only when asked for, so read it while rendering:
    /// a context kept past the frame resolves against the source's current order. Anything that must survive
    /// sorting or scrolling, such as selection or expansion, is kept against this and not against
    /// <see cref="Index"/>.
    /// </summary>
    public RowIdentity Identity => Source.GetRowIdentity(Index);
}

/// <summary>
/// Draws the content of one table cell. The table builds the cell node, sizes and clips it and aligns its
/// content, so a renderer only adds what goes inside: text, a badge, a progress bar, an editor.
/// </summary>
/// <typeparam name="TRow">The type of a row.</typeparam>
/// <typeparam name="TValue">The type of the column's value.</typeparam>
public interface ITableCellRenderer<TRow, TValue>
{
    /// <summary>Builds the cell's content in the current node; called in both passes for cells in view.</summary>
    /// <param name="gui">The GUI, positioned inside the cell node.</param>
    /// <param name="cell">The cell being drawn.</param>
    void Render(Gui gui, in TableCellContext<TRow, TValue> cell);
}

/// <summary>What the table hands a column to draw one cell: where it is and what it may reuse.</summary>
/// <typeparam name="TRow">The type of a row.</typeparam>
/// <param name="Index">The row index in the source.</param>
/// <param name="Column">The column's position among the shown columns.</param>
/// <param name="Slot">The strings the table keeps for this row.</param>
/// <param name="Cache">The table's cache, which holds the column's text cache.</param>
/// <param name="Source">The source the row comes from.</param>
/// <param name="State">The table's state.</param>
/// <param name="FontSize">The table's text size.</param>
/// <param name="Width">The cell's width.</param>
/// <param name="Height">The cell's height.</param>
readonly record struct TableCellFrame<TRow>(
    int Index,
    int Column,
    TableRowSlot Slot,
    TableCache Cache,
    ITableViewSource<TRow> Source,
    TableState State,
    float FontSize,
    float Width,
    float Height);
