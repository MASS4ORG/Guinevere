namespace Guinevere;

/// <summary>
/// Where the rows of a virtual list sit vertically. A window asks only for a row's top and for the row at a
/// position, so the storage behind it can change (a tree of offsets, say) without touching the windowing.
/// </summary>
interface IRowOffsets
{
    /// <summary>Rows in the list.</summary>
    int Count { get; }

    /// <summary>Height of the whole list, which is the top of the row past the last one.</summary>
    float Total { get; }

    /// <summary>The top edge of a row. <see cref="Count"/> gives <see cref="Total"/>.</summary>
    /// <param name="row">Row index, from 0 to <see cref="Count"/> inclusive.</param>
    float TopOf(int row);

    /// <summary>
    /// The row whose span holds a vertical position, including any extra height below it. Positions above
    /// the list give the first row and positions below it the last.
    /// </summary>
    /// <param name="y">Vertical position from the top of the list.</param>
    int RowAt(float y);
}

/// <summary>
/// Row offsets for rows of one stride, where some rows are followed by extra height such as an expanded
/// detail. Only the rows with extra height are stored, sorted by row with the running total of extra height
/// up to and including each, so a lookup is a binary search and a change rewrites only the tail after it.
/// </summary>
sealed class RowOffsetIndex : IRowOffsets
{
    readonly float _stride;
    Entry[] _entries = [];
    int _used;

    /// <summary>Creates an index with no extra height; a list whose rows change count needs a new index.</summary>
    /// <param name="rows">Rows in the list, zero or more.</param>
    /// <param name="stride">Height of a row without extra height; greater than zero.</param>
    public RowOffsetIndex(int rows, float stride)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stride);
        Count = rows;
        _stride = stride;
    }

    /// <inheritdoc/>
    public int Count { get; }

    /// <inheritdoc/>
    public float Total => Count * _stride + Tail;

    /// <summary>Extra height of every stored row, the sum of all of them.</summary>
    float Tail => _used == 0 ? 0f : _entries[_used - 1].Cumulative;

    /// <summary>Sets the extra height below a row, such as an expanded detail. Zero removes it.</summary>
    /// <param name="row">Row index, from 0 to <see cref="Count"/> minus one.</param>
    /// <param name="extra">Extra height below the row, zero or more.</param>
    public void Set(int row, float extra)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(extra);
        if ((uint)row >= (uint)Count)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }

        var index = LowerBound(row);
        var present = index < _used && _entries[index].Row == row;
        if (extra == 0f)
        {
            if (!present)
            {
                return;
            }

            Remove(index);
        }
        else if (present)
        {
            _entries[index].Extra = extra;
        }
        else
        {
            Insert(index, row, extra);
        }

        Accumulate(index);
    }

    /// <inheritdoc/>
    public float TopOf(int row)
    {
        if ((uint)row > (uint)Count)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }

        var above = LowerBound(row);
        return row * _stride + (above == 0 ? 0f : _entries[above - 1].Cumulative);
    }

    /// <inheritdoc/>
    public int RowAt(float y)
    {
        if (Count == 0 || y <= 0f)
        {
            return 0;
        }

        if (y >= Total)
        {
            return Count - 1;
        }

        // Region r holds the rows after stored row r-1 up to and including stored row r, all shifted by the
        // extra height above them. The last region that starts at or above y holds the row.
        var low = 0;
        var high = _used;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            if (RegionStart(mid) <= y)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        var first = RegionFirst(low);
        var last = low < _used ? _entries[low].Row : Count - 1;
        var row = (int)MathF.Floor((y - RegionShift(low)) / _stride);
        return Math.Min(Math.Max(row, first), last);
    }

    int RegionFirst(int region) => region == 0 ? 0 : _entries[region - 1].Row + 1;

    float RegionShift(int region) => region == 0 ? 0f : _entries[region - 1].Cumulative;

    float RegionStart(int region) => RegionFirst(region) * _stride + RegionShift(region);

    /// <summary>The number of stored rows before a row, which is also the position a row would take.</summary>
    int LowerBound(int row)
    {
        var low = 0;
        var high = _used;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (_entries[mid].Row < row)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }

    void Insert(int index, int row, float extra)
    {
        if (_used == _entries.Length)
        {
            Array.Resize(ref _entries, Math.Max(4, _used * 2));
        }

        Array.Copy(_entries, index, _entries, index + 1, _used - index);
        _entries[index] = new Entry { Row = row, Extra = extra };
        _used++;
    }

    void Remove(int index)
    {
        _used--;
        Array.Copy(_entries, index + 1, _entries, index, _used - index);
    }

    /// <summary>Rewrites the running totals from a stored row to the end.</summary>
    void Accumulate(int from)
    {
        var running = from == 0 ? 0f : _entries[from - 1].Cumulative;
        for (var i = from; i < _used; i++)
        {
            running += _entries[i].Extra;
            _entries[i].Cumulative = running;
        }
    }

    /// <summary>A stored row with extra height: 12 bytes, so a thousand expanded rows cost 12 KB.</summary>
    struct Entry
    {
        public int Row;
        public float Extra;
        public float Cumulative;
    }
}
