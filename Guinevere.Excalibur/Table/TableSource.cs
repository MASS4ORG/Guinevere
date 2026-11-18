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

/// <summary>A sort request: the column and the direction.</summary>
/// <param name="ColumnId">The <see cref="TableColumn.Id"/> to order by.</param>
/// <param name="Direction">The direction.</param>
public readonly record struct TableSort(TableColumnId ColumnId, TableSortDirection Direction = TableSortDirection.Ascending);

/// <summary>
/// The only path a table reads data through. Everything is counted and indexed, so a table with
/// millions of rows reads only the cells in view and never enumerates the dataset.
/// </summary>
public interface ITableViewSource
{
    /// <summary>The number of rows; read every frame, so it must be cheap.</summary>
    int RowCount { get; }

    /// <summary>Whether more rows are on their way, shown below the last row or in place of an empty table.</summary>
    bool IsLoading => false;

    /// <summary>Raised when rows or their values changed wholesale, for hosts that draw on demand.</summary>
    event EventHandler? Invalidated;

    /// <summary>The value of one cell, read only for cells in view.</summary>
    /// <param name="row">Row index, zero up to <see cref="RowCount"/>.</param>
    /// <param name="column">The column whose value is wanted.</param>
    object? GetValue(int row, TableColumn column);

    /// <summary>The stable key of a row, which selection and row-level state are kept against.</summary>
    /// <param name="row">Row index, zero up to <see cref="RowCount"/>.</param>
    RowIdentity GetRowIdentity(int row);
}

/// <summary>A source that orders its own rows, so a table never sorts a dataset it cannot hold.</summary>
public interface ISortableTableViewSource : ITableViewSource
{
    /// <summary>Reorders the rows the source exposes; its indices then follow the new order.</summary>
    /// <param name="key">The column and direction to order by.</param>
    void ApplySort(TableSort key);
}
