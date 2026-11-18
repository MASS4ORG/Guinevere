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

        Shown = [.. All.Where(static column => column.InitiallyVisible)];
        HeaderIds = [.. Shown.Select(static column => "table/header/" + column.Id)];
        CellSuffixes = [.. Shown.Select(static column => "/" + column.Id)];
    }

    /// <summary>Every column, in the order given.</summary>
    public TableColumn[] All { get; }

    /// <summary>The columns laid out: those visible by default, in the order given.</summary>
    public TableColumn[] Shown { get; }

    /// <summary>Node id of each shown column's header cell.</summary>
    public string[] HeaderIds { get; }

    /// <summary>The tail of the node id of each shown column's cells.</summary>
    public string[] CellSuffixes { get; }

    /// <summary>The column with this persisted id, or null; used to restore and re-bind state.</summary>
    public TableColumn? Find(string id) => _index.TryGetValue(id, out var index) ? All[index] : null;

    /// <summary>Whether this exact column instance is one of the columns.</summary>
    public bool Contains(TableColumn column) => ReferenceEquals(Find(column.Id), column);
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
        Shown = [.. _columns.Where(static column => column.InitiallyVisible)];
    }

    /// <summary>The number of columns, hidden ones included.</summary>
    public int Count => _columns.Length;

    /// <summary>A column by position in display order.</summary>
    /// <param name="index">Position, zero up to <see cref="Count"/>.</param>
    public TableColumn<TRow> this[int index] => _columns[index];

    /// <summary>The same columns without the type, with their id map.</summary>
    internal TableColumnSet Set { get; }

    /// <summary>The columns laid out, typed so the table can draw their cells.</summary>
    internal TableColumn<TRow>[] Shown { get; }

    /// <inheritdoc />
    public IEnumerator<TableColumn<TRow>> GetEnumerator() => ((IEnumerable<TableColumn<TRow>>)_columns).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
