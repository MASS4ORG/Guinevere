namespace Guinevere;

/// <summary>One row as the table's typed hooks see it.</summary>
/// <typeparam name="TRow">The type of a row.</typeparam>
/// <param name="Index">The row's index in the source now.</param>
/// <param name="Item">The row.</param>
/// <param name="Identity">The row's stable key.</param>
/// <param name="State">The table's state: selection, expansion and column choices.</param>
public readonly record struct TableRowContext<TRow>(int Index, TRow Item, RowIdentity Identity, TableState State);

/// <summary>
/// Content a table shows below an expanded row. The table builds a node of the height given, as wide as the
/// columns, and the provider draws inside it. A row is expanded by the user or by <see cref="TableExpansion"/>.
/// </summary>
/// <typeparam name="TRow">The type of a row.</typeparam>
public interface ITableRowDetails<TRow>
{
    /// <summary>Whether this row has details, which decides whether an expander shows and whether it can expand.</summary>
    /// <param name="row">The row.</param>
    bool HasDetails(in TableRowContext<TRow> row);

    /// <summary>The height of the row's details in pixels; asked when the row is laid out, not every frame.</summary>
    /// <param name="row">The row.</param>
    float GetHeight(in TableRowContext<TRow> row);

    /// <summary>Draws the details in the node the table made; called in both passes while the row is in view.</summary>
    /// <param name="gui">The GUI, positioned inside the details node.</param>
    /// <param name="row">The row.</param>
    /// <param name="width">The details node's width.</param>
    /// <param name="height">The details node's height.</param>
    void Render(Gui gui, in TableRowContext<TRow> row, float width, float height);
}

/// <summary>
/// <see cref="TableOptions"/> with the hooks that need to know the row type: a menu for a right-clicked row, what
/// activating a row does, and details shown below an expanded row. Pass it where a <see cref="TableOptions"/> goes.
/// </summary>
/// <typeparam name="TRow">The type of a row, as the table's source returns it.</typeparam>
public sealed class TableOptions<TRow> : TableOptions
{
    /// <summary>
    /// Builds the menu of the row the user right-clicked, which is selected first unless it already is. Read
    /// <see cref="TableState.Selection"/> to act on several selected rows. Null shows no row menu.
    /// </summary>
    public Action<ContextMenuBuilder, TableRowContext<TRow>>? RowMenu { get; init; }

    /// <summary>Called when a row is double-clicked or Enter is pressed on the keyboard row.</summary>
    public Action<TableRowContext<TRow>>? RowActivated { get; init; }

    /// <summary>Content for expanded rows, or null for a table whose rows do not expand.</summary>
    public ITableRowDetails<TRow>? RowDetails { get; init; }
}
