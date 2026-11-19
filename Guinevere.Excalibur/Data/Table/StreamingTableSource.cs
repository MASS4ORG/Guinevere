using System.Globalization;

namespace Guinevere;

/// <summary>
/// An append-only source for rows that arrive over time, such as a log or a feed. Any thread may
/// <see cref="Append"/>; the table reads on the UI thread without locks. Rows live in fixed-size chunks that never
/// move and are never copied, so an append costs a store and the rows in view stay put while thousands arrive.
/// </summary>
/// <remarks>
/// <para>
/// Appended rows are counted at once but published at the start of the next frame (<see cref="BeginFrame"/>), so a
/// frame sees one fixed count whatever the producers do meanwhile. The first append after a publish raises
/// <see cref="Invalidated"/>, and further appends stay quiet until the next frame has published, so a burst of any
/// size asks for one frame, never one per row.
/// </para>
/// <para>
/// A row's identity is its sequence number, the order it was appended in, which never changes: selection kept
/// against it stays on the same row while rows arrive. The source is not sortable and keeps every row it was
/// given; a retention cap with scroll anchoring, a follow-tail option and sorting over a stream are not built yet.
/// </para>
/// </remarks>
/// <typeparam name="TRow">The type of a row.</typeparam>
public sealed class StreamingTableSource<TRow> : ITableViewSource<TRow>, ITableRowLookup
{
    const int ChunkShift = 12;
    const int ChunkSize = 1 << ChunkShift;
    const int ChunkMask = ChunkSize - 1;

    readonly object _gate = new();

    // The directory of chunks. It is replaced when it fills (copying references, never rows), and a chunk is filled
    // before the count that covers it is published, so a reader that saw the count sees its chunk.
    TRow[]?[] _chunks = new TRow[]?[16];

    // Written under the gate and published with a volatile write; read by the UI thread at the start of a frame.
    int _count;

    // The snapshot a frame reads from; written and read on the UI thread.
    int _published;
    long _lastFrame = -1;
    int _notified;

    /// <inheritdoc />
    public int RowCount => _published;

    /// <summary>Whether more rows are on their way; set it on the UI thread.</summary>
    public bool IsLoading { get; set; }

    /// <summary>The number of rows appended so far, including those not yet published to the table.</summary>
    public int AppendedCount => Volatile.Read(ref _count);

    /// <inheritdoc />
    public event EventHandler? Invalidated;

    /// <inheritdoc />
    public TRow this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_published)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index,
                    $"The published snapshot has {_published} rows.");
            }

            return Volatile.Read(ref _chunks)[index >> ChunkShift]![index & ChunkMask];
        }
    }

    /// <inheritdoc />
    public RowIdentity GetRowIdentity(int index)
    {
        if ((uint)index >= (uint)_published)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"The published snapshot has {_published} rows.");
        }

        // Rows are never removed, so a sequence number is the index; a retention cap would offset it.
        return new RowIdentity(index.ToString(CultureInfo.InvariantCulture));
    }

    /// <inheritdoc />
    public int IndexOf(RowIdentity identity) =>
        int.TryParse(identity.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < _published
            ? index
            : -1;

    /// <inheritdoc />
    public void BeginFrame(long frame)
    {
        if (frame == _lastFrame)
        {
            return;
        }

        _lastFrame = frame;

        // Re-arm before reading the count: an append after this line raises the event again, one before it is
        // counted below.
        Interlocked.Exchange(ref _notified, 0);
        _published = Volatile.Read(ref _count);
    }

    /// <summary>Adds a row at the end. Safe to call from any thread.</summary>
    /// <param name="row">The row.</param>
    public void Append(TRow row)
    {
        lock (_gate)
        {
            Add(row);
        }

        Notify();
    }

    /// <summary>
    /// Adds rows at the end, in order and together: a frame sees all of them or none of them. Safe to call from any
    /// thread. The enumeration runs under the lock, so pass a collection, not a slow generator.
    /// </summary>
    /// <param name="rows">The rows.</param>
    public void AppendRange(IEnumerable<TRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        lock (_gate)
        {
            foreach (var row in rows)
            {
                Add(row);
            }
        }

        Notify();
    }

    void Add(TRow row)
    {
        var index = _count;
        if (index == int.MaxValue)
        {
            throw new InvalidOperationException("A streaming source holds at most 2,147,483,647 rows.");
        }

        var chunkIndex = index >> ChunkShift;
        if (chunkIndex == _chunks.Length)
        {
            var larger = new TRow[]?[_chunks.Length * 2];
            Array.Copy(_chunks, larger, _chunks.Length);
            Volatile.Write(ref _chunks, larger);
        }

        var chunk = _chunks[chunkIndex] ??= new TRow[ChunkSize];
        chunk[index & ChunkMask] = row;
        Volatile.Write(ref _count, index + 1);
    }

    void Notify()
    {
        if (Interlocked.Exchange(ref _notified, 1) == 0)
        {
            Invalidated?.Invoke(this, EventArgs.Empty);
        }
    }
}
