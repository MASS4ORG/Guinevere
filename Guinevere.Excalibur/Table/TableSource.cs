namespace Guinevere;

/// <summary>
/// A stable key for one row of a table's data, which survives sorting and refreshes where a row index does not.
/// </summary>
/// <param name="Key">The key, unique among the rows of one source.</param>
public readonly record struct RowIdentity(string Key)
{
    /// <summary>The identity of a source whose rows never move: the row index.</summary>
    /// <param name="row">The row index.</param>
    public static RowIdentity FromIndex(int row) => new(row.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <inheritdoc />
    public override string ToString() => Key;
}

/// <summary>The direction a column is sorted in.</summary>
public enum TableSortDirection
{
    /// <summary>Smallest first.</summary>
    Ascending,

    /// <summary>Largest first.</summary>
    Descending
}

/// <summary>A sort key: the column instance and the direction.</summary>
/// <param name="Column">The column to order by.</param>
/// <param name="Direction">The direction.</param>
public readonly record struct TableSort(TableColumn Column, TableSortDirection Direction = TableSortDirection.Ascending);

/// <summary>
/// The only path a table reads data through. Everything is counted and indexed, so a table with millions of
/// rows reads only the rows in view and never enumerates the dataset.
/// </summary>
/// <typeparam name="TRow">The type of a row; the columns read their values from it.</typeparam>
public interface ITableViewSource<TRow>
{
    /// <summary>The number of rows; read every frame, so it must be cheap.</summary>
    int RowCount { get; }

    /// <summary>Whether more rows are on their way, shown below the last row or in place of an empty table.</summary>
    bool IsLoading => false;

    /// <summary>Raised when rows or their values changed wholesale, for hosts that draw on demand.</summary>
    event EventHandler? Invalidated;

    /// <summary>One row, read only for rows in view and once per row and pass.</summary>
    /// <param name="index">Row index, zero up to <see cref="RowCount"/>.</param>
    TRow this[int index] { get; }

    /// <summary>The stable key of a row, which selection and row-level state are kept against.</summary>
    /// <param name="index">Row index, zero up to <see cref="RowCount"/>.</param>
    RowIdentity GetRowIdentity(int index);
}

/// <summary>A source that orders its own rows, so a table never sorts a dataset it cannot hold.</summary>
/// <typeparam name="TRow">The type of a row.</typeparam>
public interface ISortableTableViewSource<TRow> : ITableViewSource<TRow>
{
    /// <summary>
    /// Reorders the rows the source exposes by several keys, evaluated in the order given; its indices then
    /// follow the new order. A source backed by a database or a service maps <c>key.Column</c>, by reference or
    /// by <see cref="TableColumn.Id"/>, to its own ordering. Row identities must not change with the order.
    /// </summary>
    /// <param name="keys">The keys in evaluation order, primary first; empty clears the order.</param>
    void ApplySort(IReadOnlyList<TableSort> keys);
}
