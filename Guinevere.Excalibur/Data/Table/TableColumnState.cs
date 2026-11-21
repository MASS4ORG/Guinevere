namespace Guinevere;

/// <summary>
/// One column's remembered choices, as persistable data: its id, whether it is shown, and the width the user
/// gave it. A null width means the column keeps the width it was defined with.
/// </summary>
/// <param name="Id">The column's <see cref="TableColumn.Id"/>.</param>
/// <param name="Visible">Whether the column is shown.</param>
/// <param name="Width">The width the user chose, or null for the defined one.</param>
public readonly record struct TableColumnSetting(string Id, bool Visible, TableLength? Width);

/// <summary>
/// What a table keeps between sessions: the column choices and the sort, both by column id. It is plain data, so a
/// host serializes it however it likes; <see cref="TableState.Save"/> makes one and <see cref="TableState.Restore{TRow}"/>
/// applies it.
/// </summary>
/// <param name="Columns">Each column's visibility and width, in display order.</param>
/// <param name="Sort">The sort keys, primary first.</param>
public sealed record TableSnapshot(TableColumnSetting[] Columns, (string Id, TableSortDirection Direction)[] Sort);

/// <summary>
/// What the user chose about a table's columns: which are shown, in what order, and how wide. The choices are
/// kept by column id, so a rebuilt column list picks them up again and <see cref="Save"/> and
/// <see cref="Restore"/> move them to wherever the host keeps its settings. Columns never mentioned keep
/// the order, visibility and width they were defined with.
/// </summary>
public sealed class TableColumnState
{
    const float KeyboardStep = 16f;

    sealed class Entry(string id)
    {
        public readonly string Id = id;
        public bool? Visible;
        public TableLength? Width;
    }

    readonly List<Entry> _entries = [];
    readonly Dictionary<string, Entry> _byId = new(StringComparer.Ordinal);
    TableColumnSet? _set;

    /// <summary>Incremented by every change, so a table knows when to arrange its columns again.</summary>
    public int Version { get; private set; }

    /// <summary>Whether the table shows this column.</summary>
    /// <param name="column">A column of the table.</param>
    public bool IsVisible(TableColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        return _byId.TryGetValue(column.Id, out var entry) && entry.Visible is { } visible
            ? visible
            : column.InitiallyVisible;
    }

    /// <summary>
    /// Shows or hides a column. The last shown column cannot be hidden, so the table never loses its header.
    /// </summary>
    /// <param name="column">A column of the table.</param>
    /// <param name="visible">Whether it should be shown.</param>
    /// <returns>Whether anything changed.</returns>
    public bool SetVisible(TableColumn column, bool visible)
    {
        ArgumentNullException.ThrowIfNull(column);
        if (IsVisible(column) == visible || (!visible && _set is { } set && ShownCount(set) <= 1))
        {
            return false;
        }

        EntryFor(column.Id).Visible = visible;
        Version++;
        return true;
    }

    /// <summary>
    /// Moves a column to the place another one holds, which moves that one a step away. Pinned columns
    /// reorder only among those pinned to the same edge, so a pin stays where the column's definition put it.
    /// </summary>
    /// <param name="column">The column to move.</param>
    /// <param name="target">The column whose place it takes.</param>
    /// <returns>Whether anything changed.</returns>
    /// <exception cref="InvalidOperationException">The table has not drawn yet, so there are no columns to order.</exception>
    public bool MoveTo(TableColumn column, TableColumn target)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(target);
        var order = Materialize();
        var from = order.FindIndex(entry => entry.Id == column.Id);
        var to = order.FindIndex(entry => entry.Id == target.Id);
        if (from < 0 || to < 0 || from == to || column.Pin != target.Pin)
        {
            return false;
        }

        var moved = order[from];
        order.RemoveAt(from);
        order.Insert(to, moved);
        Commit(order);
        return true;
    }

    /// <summary>
    /// Moves a column one place toward the start or the end, past the next shown column pinned to the same edge.
    /// </summary>
    /// <param name="column">The column to move.</param>
    /// <param name="direction">Negative toward the start, positive toward the end.</param>
    /// <returns>Whether anything changed.</returns>
    public bool Nudge(TableColumn column, int direction)
    {
        ArgumentNullException.ThrowIfNull(column);
        if (direction == 0 || _set is null)
        {
            return false;
        }

        var order = Order(_set);
        var at = Array.FindIndex(order, candidate => candidate.Id == column.Id);
        for (var i = at + Math.Sign(direction); at >= 0 && i >= 0 && i < order.Length; i += Math.Sign(direction))
        {
            if (order[i].Pin == column.Pin && IsVisible(order[i]))
            {
                return MoveTo(column, order[i]);
            }
        }

        return false;
    }

    /// <summary>Gives a column a width of its own, replacing the one it was defined with.</summary>
    /// <param name="column">A column of the table.</param>
    /// <param name="width">The new width; limits of the column still apply when it is laid out.</param>
    /// <returns>Whether anything changed.</returns>
    public bool SetWidth(TableColumn column, TableLength width)
    {
        ArgumentNullException.ThrowIfNull(column);
        var entry = EntryFor(column.Id);
        if (entry.Width == width)
        {
            return false;
        }

        entry.Width = width;
        Version++;
        return true;
    }

    /// <summary>Puts a column back to the width it was defined with.</summary>
    /// <param name="column">A column of the table.</param>
    /// <returns>Whether anything changed.</returns>
    public bool ResetWidth(TableColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        if (!_byId.TryGetValue(column.Id, out var entry) || entry.Width is null)
        {
            return false;
        }

        entry.Width = null;
        Version++;
        return true;
    }

    /// <summary>Forgets every choice: order, visibility and widths go back to the definitions.</summary>
    public void Reset()
    {
        _entries.Clear();
        _byId.Clear();
        Version++;
    }

    /// <summary>
    /// The choices as persistable data, columns in display order, hidden ones included, followed by what
    /// was restored for columns this table does not have, so settings survive a column list that lacks them for a while.
    /// </summary>
    public TableColumnSetting[] Save()
    {
        var saved = new List<TableColumnSetting>(_entries.Count);
        var known = new HashSet<string>(StringComparer.Ordinal);
        if (_set is { } set)
        {
            foreach (var column in Order(set))
            {
                known.Add(column.Id);
                saved.Add(new TableColumnSetting(column.Id, IsVisible(column),
                    _byId.TryGetValue(column.Id, out var entry) ? entry.Width : null));
            }
        }

        foreach (var entry in _entries)
        {
            if (!known.Contains(entry.Id))
            {
                saved.Add(new TableColumnSetting(entry.Id, entry.Visible ?? true, entry.Width));
            }
        }

        return [.. saved];
    }

    /// <summary>
    /// Replaces every choice with saved ones: the order is the order given, and a column not listed follows
    /// the listed ones as defined. An id no column has is kept without effect, and a repeated id keeps its
    /// first entry.
    /// </summary>
    /// <param name="saved">What <see cref="Save"/> returned.</param>
    public void Restore(IEnumerable<TableColumnSetting> saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        _entries.Clear();
        _byId.Clear();
        foreach (var setting in saved)
        {
            ArgumentNullException.ThrowIfNull(setting.Id);
            if (_byId.ContainsKey(setting.Id))
            {
                continue;
            }

            var entry = EntryFor(setting.Id);
            entry.Visible = setting.Visible;
            entry.Width = setting.Width;
        }

        Version++;
    }

    /// <summary>Points the state at the columns of the table that last drew.</summary>
    internal void Bind(TableColumnSet set) => _set = set;

    /// <summary>Every column of the set in display order: the ordered entries first, then columns never mentioned.</summary>
    internal TableColumn[] Order(TableColumnSet set)
    {
        if (_entries.Count == 0)
        {
            return set.All;
        }

        var order = new List<TableColumn>(set.All.Length);
        var placed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in _entries)
        {
            if (set.Find(entry.Id) is { } column && placed.Add(entry.Id))
            {
                order.Add(column);
            }
        }

        foreach (var column in set.All)
        {
            if (placed.Add(column.Id))
            {
                order.Add(column);
            }
        }

        return [.. order];
    }

    /// <summary>The width the user gave a column, or null.</summary>
    internal TableLength? WidthOf(TableColumn column) =>
        _byId.TryGetValue(column.Id, out var entry) ? entry.Width : null;

    /// <summary>
    /// Resizes the column at a position of the laid-out columns to a width, the way a drag or a key does. A star
    /// column changes its weight so that it takes the width and the other star columns share what is left; when
    /// they cannot give that much, or it is the only star column, it becomes a fixed width instead. Any other
    /// column becomes a fixed width.
    /// </summary>
    /// <param name="layout">This frame's layout, which says what every column is now.</param>
    /// <param name="index">The column's position in <paramref name="layout"/>.</param>
    /// <param name="desired">The width wanted, in pixels, before the column's limits.</param>
    /// <returns>Whether anything changed.</returns>
    internal bool Resize(TableLayout layout, int index, float desired)
    {
        var column = layout.Columns[index];
        var width = Math.Clamp(desired, column.MinWidth, Math.Max(column.MinWidth, column.MaxWidth));
        if (layout.Lengths[index].Kind == TableLengthKind.Star)
        {
            var otherWeight = 0f;
            var pool = layout.Widths[index];
            var floor = 0f;
            for (var i = 0; i < layout.Columns.Length; i++)
            {
                if (i != index && layout.Lengths[i].Kind == TableLengthKind.Star)
                {
                    otherWeight += layout.Lengths[i].Value;
                    pool += layout.Widths[i];
                    floor += layout.Columns[i].MinWidth;
                }
            }

            if (otherWeight > 0f && width <= pool - Math.Max(floor, 1f))
            {
                return SetWidth(column, TableLength.Star(otherWeight * width / (pool - width)));
            }
        }

        return SetWidth(column, TableLength.Pixels(width));
    }

    /// <summary>Resizes by one keyboard step, wider for a positive direction and narrower for a negative one.</summary>
    internal bool ResizeByStep(TableLayout layout, int index, int direction) =>
        Resize(layout, index, layout.Widths[index] + (Math.Sign(direction) * KeyboardStep));

    int ShownCount(TableColumnSet set)
    {
        var count = 0;
        foreach (var column in set.All)
        {
            count += IsVisible(column) ? 1 : 0;
        }

        return count;
    }

    Entry EntryFor(string id)
    {
        if (!_byId.TryGetValue(id, out var entry))
        {
            entry = new Entry(id);
            _byId[id] = entry;
            _entries.Add(entry);
        }

        return entry;
    }

    /// <summary>An entry for every column of the set in display order, so a move has a complete order to change.</summary>
    List<Entry> Materialize()
    {
        var set = _set ?? throw new InvalidOperationException(
            "The table has not drawn yet, so its columns are not known. Move columns after the first frame.");
        var order = Order(set);
        foreach (var column in order)
        {
            EntryFor(column.Id);
        }

        var ordered = new List<Entry>(_entries.Count);
        foreach (var column in order)
        {
            ordered.Add(_byId[column.Id]);
        }

        foreach (var entry in _entries)
        {
            if (!ordered.Contains(entry))
            {
                ordered.Add(entry);
            }
        }

        return ordered;
    }

    void Commit(List<Entry> order)
    {
        _entries.Clear();
        _entries.AddRange(order);
        Version++;
    }
}
