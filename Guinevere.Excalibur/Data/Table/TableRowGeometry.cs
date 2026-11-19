namespace Guinevere;

/// <summary>
/// Where a table's rows sit vertically. Rows have one height, so a row's place is arithmetic until some rows are
/// expanded; then an offset index holds the extra height of each. It is rebuilt only when the expansion, the
/// data or the row height changes, and resolving the expanded rows is the only time a source is asked where a
/// row is, never for the rows in between.
/// </summary>
sealed class TableRowGeometry
{
    int _expansion = -1;
    int _source = -1;
    int _count = -1;
    float _stride = -1f;
    object? _details;
    bool _ready;

    /// <summary>The offsets of the rows when some are expanded, or null while every row has the same height.</summary>
    public RowOffsetIndex? Offsets { get; private set; }

    /// <summary>
    /// Brings the geometry up to date for this frame's snapshot. It runs in the first pass; the second pass uses
    /// what the first built, so a change made by a handler in between shows in the next frame.
    /// </summary>
    /// <typeparam name="TRow">The source's row type.</typeparam>
    public void Update<TRow>(Gui gui, TableState state, ITableViewSource<TRow> source,
        ITableRowDetails<TRow>? details, float stride)
    {
        if (gui.Pass == Pass.Pass2Render && _ready)
        {
            return;
        }

        var expansion = state.Expansion;
        var count = state.FrameRowCount;
        if (_ready && _expansion == expansion.Version && _source == state.FrameSourceVersion && _count == count
            && _stride == stride && ReferenceEquals(_details, details))
        {
            return;
        }

        var moved = _source != state.FrameSourceVersion;
        _expansion = expansion.Version;
        _source = state.FrameSourceVersion;
        _count = count;
        _stride = stride;
        _details = details;
        _ready = true;
        if (details is null || expansion.Count == 0)
        {
            Offsets = null;
            return;
        }

        Offsets = Build(state, source, details, stride, count, moved);
    }

    /// <summary>One entry per expanded row that can be placed and has details; the others take no extra height.</summary>
    static RowOffsetIndex Build<TRow>(TableState state, ITableViewSource<TRow> source, ITableRowDetails<TRow> details,
        float stride, int count, bool moved)
    {
        var index = new RowOffsetIndex(count, stride);
        var lookup = source as ITableRowLookup;
        foreach (var (key, hint) in state.Expansion.Placed())
        {
            var at = lookup is not null ? lookup.IndexOf(new RowIdentity(key)) : moved ? -1 : hint;
            if (lookup is null && moved)
            {
                state.Expansion.Forget(key);
                continue;
            }

            var height = 0f;
            if ((uint)at < (uint)count)
            {
                var row = new TableRowContext<TRow>(at, source[at], new RowIdentity(key), state);
                height = details.HasDetails(row) ? Math.Max(0f, details.GetHeight(row)) : 0f;
            }

            if (height > 0f)
            {
                index.Set(at, height);
                state.Expansion.Place(key, at, height);
            }
            else
            {
                state.Expansion.Place(key, -1, 0f);
            }
        }

        return index;
    }
}
