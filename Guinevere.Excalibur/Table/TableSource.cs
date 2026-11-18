namespace Guinevere;

/// <summary>
/// A stable key for one row of a table's data, which survives sorting and refreshes where a row index does not.
/// </summary>
/// <param name="Key">The key, unique among the rows of one source.</param>
public readonly record struct RowIdentity(string Key)
{
    /// <summary>
    /// The identity of a row by its position. This is <b>only correct for a source that is never filtered, sorted,
    /// reordered or changed</b>, such as a generated grid whose row is its index: an identity that is a position
    /// follows the position, not the row, so selection and expansion kept against it silently move to whatever row
    /// is there after a sort or an insert. Any source whose rows can move needs a key that belongs to the row.
    /// Allocates a string on every call.
    /// </summary>
    /// <param name="row">The row index.</param>
    public static RowIdentity UnstableIndex(int row) =>
        new(row.ToString(System.Globalization.CultureInfo.InvariantCulture));

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
/// <remarks>
/// <para>
/// <b>Threading and the frame contract.</b> A source is read only on the UI thread. For one frame, that is both
/// passes of it, <see cref="RowCount"/> and every index below it stay valid and return the same rows, and the same
/// row at the same index. Data that changes (rows appended by a worker, a list replaced, a new sort) therefore does
/// not show up the moment it changes: the source keeps the change aside and <b>publishes</b> it in
/// <see cref="BeginFrame"/>, which the table calls once at the start of each frame, before it reads anything. A
/// source that never changes behind the table's back can leave <see cref="BeginFrame"/> as it is.
/// </para>
/// <para>
/// The table reads <see cref="RowCount"/> and <see cref="IsLoading"/> once, in the first pass, and builds the second
/// pass from those values, so a source that does not honor the contract still cannot make the two passes build
/// different trees. It will, however, show rows that are not the ones counted.
/// </para>
/// <para>
/// Only <see cref="Invalidated"/> and the members that publish (<c>Append</c>, <c>Replace</c> and the like on the
/// built-in sources) may be called from other threads, and only if the source documents it.
/// </para>
/// </remarks>
/// <typeparam name="TRow">The type of a row; the columns read their values from it.</typeparam>
public interface ITableViewSource<TRow>
{
    /// <summary>
    /// Publishes pending changes, so that <see cref="RowCount"/> and the rows stay fixed for the frame that starts
    /// now. The table calls it once per frame, in the first pass. It must be idempotent for one frame number,
    /// because a source shared by two tables is asked by both: publish when <paramref name="frame"/> differs from
    /// the last one seen, and do nothing otherwise. The default does nothing.
    /// </summary>
    /// <param name="frame">The number of the frame, <see cref="Gui.FrameNumber"/>; it only ever increases.</param>
    void BeginFrame(long frame)
    {
    }

    /// <summary>
    /// The number of rows of the published snapshot; read by the table once per frame, and cheap enough to read
    /// again.
    /// </summary>
    int RowCount { get; }

    /// <summary>Whether more rows are on their way, shown below the last row or in place of an empty table.</summary>
    bool IsLoading => false;

    /// <summary>
    /// Raised when something is waiting to be published, so a host that draws on demand knows to draw. It may be
    /// raised from any thread, and a table asks for a frame when it is. A source must raise it whenever what
    /// <see cref="BeginFrame"/> will publish differs from the last publish, because wrappers such as a filter depend
    /// on it to know when to rebuild. A source that changes often raises it once per frame at most, never once per
    /// row.
    /// </summary>
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
    /// by <see cref="TableColumn.Id"/>, to its own ordering. Row identities must not change with the order. Like
    /// every change, the new order is published at <see cref="ITableViewSource{TRow}.BeginFrame"/>, so a frame
    /// never sees its rows move.
    /// </summary>
    /// <param name="keys">The keys in evaluation order, primary first; empty clears the order.</param>
    void ApplySort(IReadOnlyList<TableSort> keys);
}
