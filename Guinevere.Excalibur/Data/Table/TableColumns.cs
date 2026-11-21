using System.Collections;

namespace Guinevere;

/// <summary>
/// The columns of one table, in display order, frozen when built. Ids are validated once here, so a table
/// never re-checks them, and a duplicate or empty id fails at the line that builds the columns. A column whose
/// title gives no id gets <c>column</c> and its position.
/// </summary>
sealed class TableColumnSet
{
    readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);

    public TableColumnSet(IReadOnlyList<TableColumn> columns)
    {
        All = [.. columns];
        for (var i = 0; i < All.Length; i++)
        {
            var column = All[i] ?? throw new ArgumentException("A column list cannot contain null.", nameof(columns));
            column.AssignPositionalId(i);
            var id = column.Id;
            if (id.Length == 0)
            {
                throw new ArgumentException(
                    $"Column {i + 1} ('{column.Title}') has an empty Id. Set Id = \"…\" to a non-empty value, or leave it out.",
                    nameof(columns));
            }

            if (!_index.TryAdd(id, i))
            {
                throw new ArgumentException(
                    $"Columns '{All[_index[id]].Title}' and '{column.Title}' have the same id '{id}'. Set Id = \"…\" on one of them or give them unique titles.",
                    nameof(columns));
            }
        }

        _headerIds = [.. All.Select(static column => "table/header/" + column.Id)];
        _cellSuffixes = [.. All.Select(static column => "/" + column.Id)];
        _ghosts = [.. All.Select(static column => (Action<Gui>)(gui => gui.DrawText(column.Title, 12f)))];
    }

    readonly string[] _headerIds;
    readonly string[] _cellSuffixes;
    readonly Action<Gui>[] _ghosts;

    /// <summary>Every column, in the order given.</summary>
    public TableColumn[] All { get; }

    /// <summary>The column with this persisted id, or null; used to restore and re-bind state.</summary>
    public TableColumn? Find(string id) => _index.TryGetValue(id, out var index) ? All[index] : null;

    /// <summary>Whether this exact column instance is one of the columns.</summary>
    public bool Contains(TableColumn column) => ReferenceEquals(Find(column.Id), column);

    /// <summary>
    /// Arranges the columns the way a table lays them out: the shown ones in the state's order, left-pinned
    /// first, then the others, then right-pinned, each group keeping that order. Without a state, the columns
    /// visible by default in the order given.
    /// </summary>
    /// <param name="state">What the user chose, or null for the definitions alone.</param>
    public TableColumnView Arrange(TableColumnState? state = null)
    {
        var order = state?.Order(this) ?? All;
        var positions = new List<int>(order.Length);
        foreach (var pin in (ReadOnlySpan<TablePin>)[TablePin.Left, TablePin.None, TablePin.Right])
        {
            foreach (var column in order)
            {
                if (column.Pin == pin && (state?.IsVisible(column) ?? column.InitiallyVisible))
                {
                    positions.Add(_index[column.Id]);
                }
            }
        }

        var shown = new TableColumn[positions.Count];
        var lengths = new TableLength[positions.Count];
        var headerIds = new string[positions.Count];
        var suffixes = new string[positions.Count];
        var ghosts = new Action<Gui>[positions.Count];
        for (var i = 0; i < shown.Length; i++)
        {
            var at = positions[i];
            shown[i] = All[at];
            lengths[i] = state?.WidthOf(All[at]) ?? All[at].Width;
            headerIds[i] = _headerIds[at];
            suffixes[i] = _cellSuffixes[at];
            ghosts[i] = _ghosts[at];
        }

        return new TableColumnView(shown, lengths, headerIds, suffixes, ghosts);
    }
}

/// <summary>
/// The columns a table lays out in one arrangement: which are shown, in what order, their defined or chosen
/// widths, and the node ids built from their keys. It is made again only when the columns or the user's choices change.
/// </summary>
/// <param name="Shown">The shown columns in display order.</param>
/// <param name="Lengths">How each shown column's width is decided now.</param>
/// <param name="HeaderIds">Node id of each shown column's header cell.</param>
/// <param name="CellSuffixes">The tail of the node id of each shown column's cells.</param>
/// <param name="Ghosts">What follows the pointer while each shown column's header is dragged.</param>
sealed record TableColumnView(TableColumn[] Shown, TableLength[] Lengths, string[] HeaderIds, string[] CellSuffixes,
    Action<Gui>[] Ghosts)
{
    /// <summary>The node id of a header's resize grab zone, derived from its header id and kept.</summary>
    public string ResizeId(int shown) => (_resizeIds ??= new string?[Shown.Length])[shown] ??= HeaderIds[shown] + "/resize";

    /// <summary>The key a header's resize drag keeps its starting width under.</summary>
    public string AnchorId(int shown) => (_anchorIds ??= new string?[Shown.Length])[shown] ??= ResizeId(shown) + "/anchor";

    string?[]? _resizeIds;
    string?[]? _anchorIds;

    object? _typed;

    /// <summary>The shown columns as columns of one row type, converted once.</summary>
    /// <typeparam name="TRow">The row type of the table's columns.</typeparam>
    public TableColumn<TRow>[] Typed<TRow>() =>
        (TableColumn<TRow>[])(_typed ??= Array.ConvertAll(Shown, static column => (TableColumn<TRow>)column));
}

/// <summary>
/// The columns of a table over rows of <typeparamref name="TRow"/>, in display order. Build one and share it
/// between frames: building validates the ids, so a duplicate id throws here, with a message
/// that asks for an explicit <see cref="TableColumn.Id"/> or unique titles. Columns with no title text get
/// the id <c>column</c> plus their position.
/// </summary>
/// <typeparam name="TRow">The type of a row, as the source's indexer returns it.</typeparam>
public sealed class TableColumns<TRow> : IReadOnlyList<TableColumn<TRow>>
{
    readonly TableColumn<TRow>[] _columns;

    /// <summary>Freezes the columns.</summary>
    /// <param name="columns">The columns in display order.</param>
    /// <exception cref="ArgumentException">An explicit id is empty, or an id is used by two columns.</exception>
    public TableColumns(IEnumerable<TableColumn<TRow>> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        _columns = [.. columns];
        Set = new TableColumnSet(_columns);
    }

    /// <summary>The number of columns, hidden ones included.</summary>
    public int Count => _columns.Length;

    /// <summary>A column by position in display order.</summary>
    /// <param name="index">Position, zero up to <see cref="Count"/>.</param>
    public TableColumn<TRow> this[int index] => _columns[index];

    /// <summary>The same columns without the type, with their id map.</summary>
    internal TableColumnSet Set { get; }

    /// <inheritdoc />
    public IEnumerator<TableColumn<TRow>> GetEnumerator() => ((IEnumerable<TableColumn<TRow>>)_columns).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
