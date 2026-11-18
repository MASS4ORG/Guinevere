namespace Guinevere;

/// <summary>
/// A sortable source over rows held in memory. It orders an index permutation with the comparers of the
/// sort keys' columns, so the rows are never moved or copied, no sort code is written, and nothing is boxed
/// or hashed. A sort allocates the permutation once and reuses it for the next sort of the same row count.
/// </summary>
/// <typeparam name="TRow">The type of a row.</typeparam>
public sealed class ListTableSource<TRow> : ISortableTableViewSource<TRow>
{
    readonly IReadOnlyList<TRow> _rows;
    readonly Func<TRow, string> _identity;
    readonly OrderComparer _comparer;
    readonly Comparison<int> _comparison;

    int[]? _order;

    /// <summary>Creates a source over a list.</summary>
    /// <param name="rows">
    /// The rows, read live. When rows are added or removed, call <see cref="Refresh"/> so the order is rebuilt;
    /// a change of the row count is also noticed on the next read.
    /// </param>
    /// <param name="identity">The stable key of a row, which must not change when the order does.</param>
    public ListTableSource(IReadOnlyList<TRow> rows, Func<TRow, string> identity)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(identity);
        _rows = rows;
        _identity = identity;
        _comparer = new OrderComparer(rows);
        _comparison = _comparer.Compare;
    }

    /// <inheritdoc />
    public int RowCount => _rows.Count;

    /// <summary>Whether more rows are on their way; set it while a load is in progress.</summary>
    public bool IsLoading { get; set; }

    /// <inheritdoc />
    public event EventHandler? Invalidated;

    /// <inheritdoc />
    public TRow this[int index]
    {
        get
        {
            if (_order is not { } order)
            {
                return _rows[index];
            }

            if (order.Length != _rows.Count)
            {
                Resort();
                order = _order;
            }

            return _rows[order[index]];
        }
    }

    /// <inheritdoc />
    public RowIdentity GetRowIdentity(int index) => new(_identity(this[index]));

    /// <summary>
    /// Orders the rows by the keys, evaluated in order, ties keeping their original order. An empty list
    /// restores the original order.
    /// </summary>
    /// <param name="keys">The keys in evaluation order, primary first.</param>
    /// <exception cref="ArgumentException">A key's column is not a sortable column over this row type.</exception>
    public void ApplySort(IReadOnlyList<TableSort> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        // An indexed loop: foreach over the interface would allocate an enumerator for every sort.
        for (var i = 0; i < keys.Count; i++)
        {
            if (keys[i].Column is not TableColumn<TRow> column)
            {
                throw new ArgumentException(
                    $"Column '{keys[i].Column.Title}' is not a column over {typeof(TRow).Name}.", nameof(keys));
            }

            if (!column.Sortable)
            {
                throw new ArgumentException($"Column '{column.Title}' cannot be sorted.", nameof(keys));
            }
        }

        _comparer.Set(keys);
        if (keys.Count == 0)
        {
            _order = null;
        }
        else
        {
            Resort();
        }

        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Rebuilds the order after the rows changed, and raises <see cref="Invalidated"/>.</summary>
    public void Refresh()
    {
        if (_order is not null)
        {
            Resort();
        }

        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    void Resort()
    {
        var count = _rows.Count;
        if (_order is null || _order.Length != count)
        {
            _order = new int[count];
        }

        for (var i = 0; i < count; i++)
        {
            _order[i] = i;
        }

        Array.Sort(_order, _comparison);
    }

    /// <summary>Compares two row indices by the sort keys, then by index so equal rows stay in order.</summary>
    sealed class OrderComparer(IReadOnlyList<TRow> rows)
    {
        TableColumn<TRow>[] _columns = [];
        bool[] _descending = [];

        /// <summary>Takes the keys, already checked to be sortable columns over this row type, reusing its arrays.</summary>
        public void Set(IReadOnlyList<TableSort> keys)
        {
            if (_columns.Length != keys.Count)
            {
                _columns = new TableColumn<TRow>[keys.Count];
                _descending = new bool[keys.Count];
            }

            for (var i = 0; i < keys.Count; i++)
            {
                _columns[i] = (TableColumn<TRow>)keys[i].Column;
                _descending[i] = keys[i].Direction == TableSortDirection.Descending;
            }
        }

        public int Compare(int left, int right)
        {
            var a = rows[left];
            var b = rows[right];
            for (var i = 0; i < _columns.Length; i++)
            {
                var result = _columns[i].Compare(a, b);
                if (result != 0)
                {
                    return _descending[i] ? -result : result;
                }
            }

            return left.CompareTo(right);
        }
    }
}
