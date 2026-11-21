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

    /// <summary>
    /// The rows the user selected, kept by identity. Set <see cref="TableOptions.Selection"/> to let the table change
    /// it; a host may also read it, set it or clear it at any time between frames.
    /// </summary>
    public TableSelection Selection { get; } = new();

    /// <summary>The rows shown expanded, kept by identity; see <see cref="ITableRowDetails{TRow}"/>.</summary>
    public TableExpansion Expansion { get; } = new();

    /// <summary>The details provider of this frame's options, which an expander column asks whether a row has any.</summary>
    internal object? Details { get; set; }

    /// <summary>Where the rows sit vertically, rebuilt only when expansion or the data moves.</summary>
    internal TableRowGeometry Geometry { get; } = new();

    /// <summary>The host's accessibility tree during the render pass, or null when it has none.</summary>
    internal IAccessibilityTreeSink? Accessibility { get; set; }

    /// <summary>The table node's id, which the ids of its accessible rows, cells and headers hang from.</summary>
    internal string? AccessibleId { get; set; }

    /// <summary>The selection and sort versions already announced, so each change is announced once.</summary>
    internal int AnnouncedSelection { get; set; }

    internal int AnnouncedSort { get; set; }

    /// <summary>Whether the column menu in the header is open.</summary>
    internal bool HeaderMenuOpen;

    /// <summary>Whether the menu of a right-clicked row is open.</summary>
    internal bool RowMenuOpen;

    /// <summary>The row the open menu belongs to, by index and identity.</summary>
    internal int MenuRow = -1;

    internal string? MenuKey;

    /// <summary>Whether a press on a row started a drag that selects the rows it passes.</summary>
    internal bool DragSelecting;

    /// <summary>
    /// The version of the data this frame's snapshot is of. It moves when the source says it has something to
    /// publish or its count changes, and it is what ids and identities cached per row slot are checked against.
    /// </summary>
    internal int FrameSourceVersion { get; private set; }

    int _sourceVersion;
    int _lastCount = -1;

    /// <summary>
    /// Everything the user chose that outlives a session: the columns (order, visibility, widths) and the sort. Store
    /// it wherever the host keeps settings; selection and expansion are not part of it, since rows are not stable
    /// across sessions.
    /// </summary>
    public TableSnapshot Save() => new(Columns.Save(), Sort.Save());

    /// <summary>
    /// Brings back what <see cref="Save"/> returned. The sort is resolved against <paramref name="columns"/>,
    /// dropping ids that no longer exist, and applied to <paramref name="source"/> when one is given, so the rows
    /// come back in the saved order. Call it before the first frame or at any time between frames.
    /// </summary>
    /// <typeparam name="TRow">The columns' row type.</typeparam>
    /// <param name="snapshot">What <see cref="Save"/> returned.</param>
    /// <param name="columns">The table's columns.</param>
    /// <param name="source">The source to apply the sort to, or null to leave the rows as the source has them.</param>
    public void Restore<TRow>(TableSnapshot snapshot, TableColumns<TRow> columns,
        ISortableTableViewSource<TRow>? source = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(columns);
        Columns.Restore(snapshot.Columns);
        Sort.Restore(snapshot.Sort, columns);
        source?.ApplySort([.. Sort]);
    }

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
        if (replaced || FrameRowCount != _lastCount)
        {
            Interlocked.Increment(ref _sourceVersion);
        }

        _lastCount = FrameRowCount;
        var version = Volatile.Read(ref _sourceVersion);
        if (version != FrameSourceVersion)
        {
            FrameSourceVersion = version;
            Selection.Reconcile(source);
        }
    }

    void MarkSourceChanged() => Interlocked.Increment(ref _sourceVersion);

    void Bind<TRow>(Gui gui, ITableViewSource<TRow> source)
    {
        _unbind?.Invoke();
        var relay = new InvalidationRelay(gui, this);
        source.Invalidated += relay.Handle;
        _source = source;
        _unbind = () => source.Invalidated -= relay.Handle;
    }

    /// <summary>
    /// Asks for a frame when a source says it has something to publish. It may be called from any thread, so it
    /// holds the Gui weakly: a long-lived source then keeps no window alive.
    /// </summary>
    sealed class InvalidationRelay(Gui gui, TableState state)
    {
        readonly WeakReference<Gui> _gui = new(gui);
        readonly WeakReference<TableState> _state = new(state);

        public void Handle(object? sender, EventArgs e)
        {
            if (_state.TryGetTarget(out var target))
            {
                target.MarkSourceChanged();
            }

            if (_gui.TryGetTarget(out var window))
            {
                window.RequestFrame();
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
