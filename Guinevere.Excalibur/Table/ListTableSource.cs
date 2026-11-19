namespace Guinevere;

/// <summary>
/// A sortable source over rows held in memory. It orders an index permutation with the comparers of the
/// sort keys' columns, so the rows are never moved or copied, no sort code is written, and nothing is boxed
/// or hashed. A sort allocates the permutation once and reuses it for the next sort of the same row count.
/// </summary>
/// <remarks>
/// Changes follow the frame contract of <see cref="ITableViewSource{TRow}"/>: <see cref="Replace"/>,
/// <see cref="ApplySort"/> and <see cref="Refresh"/> only record the change, and the next
/// <see cref="BeginFrame"/> publishes it, so the rows a frame reads never change under it. Only
/// <see cref="Replace"/> may be called from another thread. A list that is changed in place on the UI thread
/// between frames is noticed at the next frame start through its count; call <see cref="Refresh"/> after editing
/// rows without changing the count.
/// </remarks>
/// <typeparam name="TRow">The type of a row.</typeparam>
public sealed class ListTableSource<TRow> : ISortableTableViewSource<TRow>, ITableRowLookup
{
    readonly Func<TRow, string> _identity;
    readonly OrderComparer _comparer;
    readonly Comparison<int> _comparison;
    readonly object _gate = new();

    // The published snapshot; touched on the UI thread only.
    IReadOnlyList<TRow> _rows;
    int _count;
    int[]? _order;
    long _lastFrame = -1;

    // Where each identity sits now, built the first time a row is looked up after a change.
    Dictionary<string, int>? _positions;

    // Changes waiting for the next frame; guarded by _gate.
    IReadOnlyList<TRow>? _pendingRows;
    TableSort[] _pendingKeys = [];
    int _pendingKeyCount;
    bool _pendingKeysSet;
    bool _pendingRefresh;

    /// <summary>Creates a source over a list.</summary>
    /// <param name="rows">
    /// The rows. Replace them with <see cref="Replace"/>; if the list itself is edited, do it on the UI thread
    /// between frames and call <see cref="Refresh"/> unless the count changed.
    /// </param>
    /// <param name="identity">The stable key of a row, which must not change when the order does.</param>
    public ListTableSource(IReadOnlyList<TRow> rows, Func<TRow, string> identity)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(identity);
        _rows = rows;
        _count = rows.Count;
        _identity = identity;
        _comparer = new OrderComparer(rows);
        _comparison = _comparer.Compare;
    }

    /// <inheritdoc />
    public int RowCount => _count;

    /// <summary>Whether more rows are on their way; set it while a load is in progress, on the UI thread.</summary>
    public bool IsLoading { get; set; }

    /// <inheritdoc />
    public event EventHandler? Invalidated;

    /// <inheritdoc />
    public TRow this[int index] => _order is { } order ? _rows[order[index]] : _rows[index];

    /// <inheritdoc />
    public RowIdentity GetRowIdentity(int index) => new(_identity(this[index]));

    /// <summary>
    /// Finds a row from its identity. The first lookup after a change reads every row's identity once, so the
    /// cost is paid only by tables that keep rows expanded, and only when the data changed.
    /// </summary>
    /// <param name="identity">The row's identity.</param>
    public int IndexOf(RowIdentity identity)
    {
        if (_positions is null)
        {
            _positions = new Dictionary<string, int>(_count, StringComparer.Ordinal);
            for (var i = 0; i < _count; i++)
            {
                _positions[GetRowIdentity(i).Key] = i;
            }
        }

        return _positions.TryGetValue(identity.Key, out var index) ? index : -1;
    }

    /// <inheritdoc />
    public void BeginFrame(long frame)
    {
        if (frame == _lastFrame)
        {
            return;
        }

        _lastFrame = frame;
        Publish();
    }

    /// <summary>
    /// Swaps in a new list of rows, from any thread. It takes effect at the next frame start, keeps the sort that
    /// is applied and keeps identities (they come from the rows), so selection and expansion kept against them
    /// survive. The list must not be changed afterwards by anyone: hand over a snapshot.
    /// </summary>
    /// <param name="rows">The new rows.</param>
    public void Replace(IReadOnlyList<TRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        lock (_gate)
        {
            _pendingRows = rows;
        }

        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Orders the rows by the keys, evaluated in order, ties keeping their original order. An empty list
    /// restores the original order. The order shows from the next frame start.
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

        lock (_gate)
        {
            if (_pendingKeys.Length < keys.Count)
            {
                _pendingKeys = new TableSort[keys.Count];
            }

            for (var i = 0; i < keys.Count; i++)
            {
                _pendingKeys[i] = keys[i];
            }

            _pendingKeyCount = keys.Count;
            _pendingKeysSet = true;
        }

        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Rebuilds the order from the rows as they are now, for a list edited in place without a change of count. It
    /// takes effect at the next frame start.
    /// </summary>
    public void Refresh()
    {
        lock (_gate)
        {
            _pendingRefresh = true;
        }

        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    void Publish()
    {
        IReadOnlyList<TRow>? rows;
        bool keysSet;
        bool refresh;
        lock (_gate)
        {
            rows = _pendingRows;
            _pendingRows = null;
            refresh = _pendingRefresh;
            _pendingRefresh = false;
            keysSet = _pendingKeysSet;
            _pendingKeysSet = false;
            if (keysSet)
            {
                _comparer.Set(_pendingKeys, _pendingKeyCount);
            }
        }

        var changed = refresh || keysSet;
        if (rows is not null)
        {
            _rows = rows;
            _comparer.Rows = rows;
            changed = true;
        }

        var count = _rows.Count;
        if (count != _count)
        {
            _count = count;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        _positions = null;
        if (_comparer.KeyCount == 0)
        {
            _order = null;
        }
        else
        {
            Resort();
        }
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
    sealed class OrderComparer(IReadOnlyList<TRow> initial)
    {
        TableColumn<TRow>[] _columns = [];
        bool[] _descending = [];
        int _count;

        public IReadOnlyList<TRow> Rows { get; set; } = initial;

        public int KeyCount => _count;

        /// <summary>Takes the keys, already checked to be sortable columns over this row type, reusing its arrays.</summary>
        public void Set(TableSort[] keys, int count)
        {
            if (_columns.Length != count)
            {
                _columns = new TableColumn<TRow>[count];
                _descending = new bool[count];
            }

            _count = count;
            for (var i = 0; i < count; i++)
            {
                _columns[i] = (TableColumn<TRow>)keys[i].Column;
                _descending[i] = keys[i].Direction == TableSortDirection.Descending;
            }
        }

        public int Compare(int left, int right)
        {
            var rows = Rows;
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
