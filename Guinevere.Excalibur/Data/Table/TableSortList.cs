using System.Collections;

namespace Guinevere;

/// <summary>
/// The order a table is sorted in: a primary key and the secondary keys retained behind it, which the source
/// evaluates in that order. Keys refer to column instances and are compared by reference. A table only draws
/// this list; the source owns the comparators and the ordering.
/// </summary>
public sealed class TableSortList : IReadOnlyList<TableSort>
{
    readonly List<TableSort> _keys = [];

    /// <summary>The number of keys, primary included.</summary>
    public int Count => _keys.Count;

    /// <summary>Incremented by every change, so a host can tell the order moved.</summary>
    public int Version { get; private set; }

    /// <summary>The first key, or null when nothing is sorted.</summary>
    public TableSort? Primary => _keys.Count > 0 ? _keys[0] : null;

    /// <summary>The number of retained keys behind the primary.</summary>
    public int SecondaryCount => Math.Max(0, _keys.Count - 1);

    /// <summary>A key by position; zero is the primary.</summary>
    /// <param name="index">Position in evaluation order.</param>
    public TableSort this[int index] => _keys[index];

    /// <summary>Where a column sits in evaluation order, or -1 when it is not part of the sort.</summary>
    /// <param name="column">The column instance to look for.</param>
    public int IndexOf(TableColumn column)
    {
        for (var i = 0; i < _keys.Count; i++)
        {
            if (ReferenceEquals(_keys[i].Column, column))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Makes a key the primary and demotes the previous primary into the first secondary slot, keeping the keys
    /// retained earlier in their order. A key already in the list moves to the front instead of repeating, so
    /// "name, then category" survives as category first and name second.
    /// </summary>
    /// <param name="key">The column and direction to sort by first.</param>
    public void Promote(TableSort key)
    {
        ArgumentNullException.ThrowIfNull(key.Column);
        var existing = IndexOf(key.Column);
        if (existing >= 0)
        {
            _keys.RemoveAt(existing);
        }

        _keys.Insert(0, key);
        Version++;
    }

    /// <summary>
    /// What a click on a column header does: the primary column flips its direction and keeps the secondaries;
    /// any other column becomes the ascending primary and demotes the previous one.
    /// </summary>
    /// <param name="column">The clicked column.</param>
    public void Click(TableColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        if (Primary is { } primary && ReferenceEquals(primary.Column, column))
        {
            _keys[0] = primary with
            {
                Direction = primary.Direction == TableSortDirection.Ascending
                    ? TableSortDirection.Descending
                    : TableSortDirection.Ascending
            };
            Version++;
            return;
        }

        Promote(new TableSort(column));
    }

    /// <summary>Replaces the whole order. A column given twice keeps its first position.</summary>
    /// <param name="keys">The keys in evaluation order, primary first.</param>
    public void Set(IEnumerable<TableSort> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        _keys.Clear();
        foreach (var key in keys)
        {
            ArgumentNullException.ThrowIfNull(key.Column);
            if (IndexOf(key.Column) < 0)
            {
                _keys.Add(key);
            }
        }

        Version++;
    }

    /// <summary>Drops every key, the primary and the retained ones.</summary>
    public void Clear()
    {
        _keys.Clear();
        Version++;
    }

    /// <summary>
    /// The order as persistable data: each key's column id and direction, in evaluation order. Column ids are
    /// plain strings, so what is stored stays valid for as long as the columns keep their ids.
    /// </summary>
    public (string Id, TableSortDirection Direction)[] Save() =>
        [.. _keys.Select(static key => (key.Column.Id, key.Direction))];

    /// <summary>
    /// Replaces the order with saved keys, resolved against the current columns. An id no column has any more,
    /// or one whose column cannot be sorted, is dropped without complaint, so state saved by an older version of
    /// the columns still loads.
    /// </summary>
    /// <typeparam name="TRow">The columns' row type.</typeparam>
    /// <param name="saved">What <see cref="Save"/> returned.</param>
    /// <param name="columns">The columns to resolve the ids against.</param>
    public void Restore<TRow>(IEnumerable<(string Id, TableSortDirection Direction)> saved,
        TableColumns<TRow> columns)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(columns);
        Bind(saved, columns.Set);
    }

    /// <summary>
    /// Points the keys at the columns of a rebuilt column list by id, dropping those it no longer has, so
    /// rebuilding the columns does not orphan the sort. The same resolution as <see cref="Restore{TRow}"/>.
    /// </summary>
    internal void Rebind(TableColumnSet columns) => Bind(Save(), columns);

    void Bind(IEnumerable<(string Id, TableSortDirection Direction)> saved, TableColumnSet columns)
    {
        List<TableSort> next = [];
        foreach (var (id, direction) in saved)
        {
            if (columns.Find(id) is { Sortable: true } column && next.TrueForAll(key => !ReferenceEquals(key.Column, column)))
            {
                next.Add(new TableSort(column, direction));
            }
        }

        if (next.SequenceEqual(_keys))
        {
            return;
        }

        _keys.Clear();
        _keys.AddRange(next);
        Version++;
    }

    /// <inheritdoc />
    public IEnumerator<TableSort> GetEnumerator() => _keys.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
