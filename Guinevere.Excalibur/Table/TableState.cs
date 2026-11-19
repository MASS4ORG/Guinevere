namespace Guinevere;

/// <summary>
/// What a table remembers between frames: the sort order its header shows, and the viewport the last
/// frame measured, which both passes of the next frame build their visible window from.
/// </summary>
public sealed class TableState
{
    /// <summary>
    /// The order the header marks: a primary key and the secondary keys retained behind it. The table only
    /// draws it, because a table over a virtual dataset cannot sort rows it never holds;
    /// <see cref="SortBy{TRow}(ISortableTableViewSource{TRow}, TableColumn)"/> updates it and asks the source to
    /// apply it in one step. When the table is given a rebuilt column list, the keys follow their columns by id.
    /// </summary>
    public TableSortList Sort { get; } = new();

    /// <summary>
    /// Sorts the way a header click does: updates <see cref="Sort"/> (the clicked column becomes the primary, or
    /// flips direction when it already is) and asks the source to apply the whole list.
    /// </summary>
    /// <typeparam name="TRow">The source's row type.</typeparam>
    /// <param name="source">The source that owns the ordering.</param>
    /// <param name="column">The clicked column.</param>
    /// <exception cref="ArgumentException">
    /// The column cannot be sorted, or is not one of the columns this state's table last drew. Before the first
    /// frame there is no such list and only sortability is checked.
    /// </exception>
    public void SortBy<TRow>(ISortableTableViewSource<TRow> source, TableColumn column)
    {
        ArgumentNullException.ThrowIfNull(source);
        Require(column);
        Sort.Click(column);
        source.ApplySort([.. Sort]);
    }

    /// <summary>
    /// Sorts by an explicit list: replaces <see cref="Sort"/> and asks the source to apply it. An empty list
    /// clears the order.
    /// </summary>
    /// <typeparam name="TRow">The source's row type.</typeparam>
    /// <param name="source">The source that owns the ordering.</param>
    /// <param name="keys">The keys in evaluation order, primary first.</param>
    /// <exception cref="ArgumentException">A key's column fails the checks of the single-column overload.</exception>
    public void SortBy<TRow>(ISortableTableViewSource<TRow> source, IReadOnlyList<TableSort> keys)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keys);
        foreach (var key in keys)
        {
            Require(key.Column);
        }

        Sort.Set(keys);
        source.ApplySort([.. Sort]);
    }

    /// <summary>
    /// What the user chose about the columns: which are shown, their order and their widths. The table's header
    /// changes it on resize, drag and the column menu; a host reads and writes it to keep the choices.
    /// </summary>
    public TableColumnState Columns { get; } = new();

    /// <summary>Layout, ids and text the table derives from its inputs and reuses until they change.</summary>
    internal TableCache Cache { get; } = new();

    /// <summary>The number of rows of the snapshot this frame is built from; both passes use it.</summary>
    internal int FrameRowCount { get; private set; }

    /// <summary>Whether the source was loading in this frame's snapshot.</summary>
    internal bool FrameLoading { get; private set; }

    object? _source;
    Action? _unbind;
    long _frameStamp = -1;

    /// <summary>
    /// Takes this frame's snapshot of the source: asks it to publish, then keeps its row count and loading flag, so
    /// the second pass builds the tree the first one did even if the source changes in between. It runs in the
    /// first pass, and in the second only when no first pass of this frame ran with this source. A source that
    /// replaces the previous one is subscribed to, so its <see cref="ITableViewSource{TRow}.Invalidated"/> asks
    /// for a frame, and the old one is let go.
    /// </summary>
    internal void BeginFrame<TRow>(Gui gui, ITableViewSource<TRow> source)
    {
        var replaced = !ReferenceEquals(_source, source);
        if (replaced)
        {
            Bind(gui, source);
        }

        var frame = gui.FrameNumber;
        if (!replaced && gui.Pass != Pass.Pass1Build && _frameStamp == frame)
        {
            return;
        }

        _frameStamp = frame;
        source.BeginFrame(frame);
        FrameRowCount = source.RowCount;
        FrameLoading = source.IsLoading;
    }

    void Bind<TRow>(Gui gui, ITableViewSource<TRow> source)
    {
        _unbind?.Invoke();
        var relay = new InvalidationRelay(gui);
        source.Invalidated += relay.Handle;
        _source = source;
        _unbind = () => source.Invalidated -= relay.Handle;
    }

    /// <summary>
    /// Asks for a frame when a source says it has something to publish. It may be called from any thread, so it
    /// holds the Gui weakly: a long-lived source then keeps no window alive.
    /// </summary>
    sealed class InvalidationRelay(Gui gui)
    {
        readonly WeakReference<Gui> _gui = new(gui);

        public void Handle(object? sender, EventArgs e)
        {
            if (_gui.TryGetTarget(out var target))
            {
                target.RequestFrame();
            }
        }
    }

    /// <summary>Horizontal scroll offset of the body, which the sticky header follows.</summary>
    internal float FrameScrollX { get; set; }

    /// <summary>Vertical scroll offset of the body.</summary>
    internal float FrameScrollY { get; set; }

    /// <summary>The body's viewport width inside its padding; columns share it.</summary>
    internal float FrameViewportWidth { get; set; } = 800f;

    /// <summary>The body's viewport height; rows are built to fill it.</summary>
    internal float FrameViewportHeight { get; set; } = 600f;

    void Require(TableColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        if (Cache.Columns is { } columns && !columns.Contains(column))
        {
            throw new ArgumentException($"Column '{column.Title}' is not one of this table's columns.", nameof(column));
        }

        if (!column.Sortable)
        {
            throw new ArgumentException($"Column '{column.Title}' cannot be sorted.", nameof(column));
        }
    }
}
