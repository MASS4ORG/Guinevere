namespace Guinevere;

/// <summary>How many rows of a table the user can select.</summary>
public enum TableSelectionMode
{
    /// <summary>Rows cannot be selected; clicks and keys do nothing to them.</summary>
    None,

    /// <summary>One row at a time.</summary>
    Single,

    /// <summary>Any number: Ctrl toggles a row, Shift selects a range, and dragging across rows selects them.</summary>
    Multiple
}

/// <summary>
/// The rows a table has selected, kept by row identity so a selection survives sorting, scrolling and refreshes.
/// The table changes it from clicks, drags and keys; a host reads it, sets it, or clears it. It also remembers
/// the row the keyboard is on. A key the source no longer has stays in the selection until it is removed or
/// the selection is cleared, because a table never scans a dataset to find out.
/// </summary>
public sealed class TableSelection
{
    readonly HashSet<string> _keys = new(StringComparer.Ordinal);

    /// <summary>The number of selected rows.</summary>
    public int Count => _keys.Count;

    /// <summary>Incremented by every change, so a host can tell the selection moved.</summary>
    public int Version { get; private set; }

    /// <summary>The row the keyboard is on, which Enter activates and an arrow moves, or null.</summary>
    public RowIdentity? Current => CurrentKey is { } key ? new RowIdentity(key) : null;

    /// <summary>Where a range selection started: the last row selected without Shift.</summary>
    internal string? AnchorKey { get; private set; }

    internal int AnchorIndex { get; private set; } = -1;

    internal string? CurrentKey { get; private set; }

    internal int CurrentIndex { get; private set; } = -1;

    /// <summary>Whether a row is selected.</summary>
    /// <param name="identity">The row's identity.</param>
    public bool IsSelected(RowIdentity identity) => _keys.Contains(identity.Key);

    /// <summary>The selected rows' identities, in no particular order.</summary>
    public RowIdentity[] ToArray() => [.. _keys.Select(static key => new RowIdentity(key))];

    /// <summary>Selects exactly one row and nothing else.</summary>
    /// <param name="identity">The row to select.</param>
    /// <returns>Whether the selection changed.</returns>
    public bool Select(RowIdentity identity) => SetOnly(identity.Key);

    /// <summary>Adds a row to the selection.</summary>
    /// <param name="identity">The row to add.</param>
    /// <returns>Whether it was not selected before.</returns>
    public bool Add(RowIdentity identity) => Changed(_keys.Add(identity.Key));

    /// <summary>Removes a row from the selection.</summary>
    /// <param name="identity">The row to remove.</param>
    /// <returns>Whether it was selected.</returns>
    public bool Remove(RowIdentity identity) => Changed(_keys.Remove(identity.Key));

    /// <summary>Selects an unselected row and unselects a selected one.</summary>
    /// <param name="identity">The row to toggle.</param>
    /// <returns>Whether the row is selected now.</returns>
    public bool Toggle(RowIdentity identity) => ToggleKey(identity.Key);

    /// <summary>Replaces the selection with a set of rows.</summary>
    /// <param name="identities">The rows to select.</param>
    public void Set(IEnumerable<RowIdentity> identities)
    {
        ArgumentNullException.ThrowIfNull(identities);
        _keys.Clear();
        foreach (var identity in identities)
        {
            _keys.Add(identity.Key);
        }

        Version++;
    }

    /// <summary>Unselects every row and forgets where the range started.</summary>
    /// <returns>Whether anything was selected.</returns>
    public bool Clear()
    {
        var had = _keys.Count > 0;
        _keys.Clear();
        AnchorKey = null;
        AnchorIndex = -1;
        return Changed(had);
    }

    /// <summary>Whether a key is selected; false at once when nothing is, which keeps the common frame free.</summary>
    internal bool Contains(string key) => _keys.Count > 0 && _keys.Contains(key);

    /// <summary>
    /// What a click on a row does: a plain click selects only that row, Ctrl toggles it, and Shift selects the
    /// range from the anchor to it (added to the selection with Ctrl). Single mode ignores both modifiers.
    /// </summary>
    internal bool Click<TRow>(ITableViewSource<TRow> source, TableSelectionMode mode, int row, string key, bool ctrl,
        bool shift)
    {
        bool changed;
        if (mode == TableSelectionMode.Multiple && shift && AnchorIndex >= 0)
        {
            changed = SelectRange(source, AnchorIndex, row, ctrl);
        }
        else if (mode == TableSelectionMode.Multiple && ctrl)
        {
            changed = ToggleKey(key);
            SetAnchor(row, key);
        }
        else
        {
            changed = SetOnly(key);
            SetAnchor(row, key);
        }

        SetCurrent(row, key);
        return changed;
    }

    /// <summary>
    /// Selects every row from one index to another, both included, reading each identity once. The cost is
    /// the length of the range, so it is for an explicit action such as Shift+click or a drag, never a frame.
    /// </summary>
    internal bool SelectRange<TRow>(ITableViewSource<TRow> source, int from, int to, bool additive)
    {
        var low = Math.Max(0, Math.Min(from, to));
        var high = Math.Min(source.RowCount - 1, Math.Max(from, to));
        var before = _keys.Count;
        var cleared = !additive && before > 0;
        if (!additive)
        {
            _keys.Clear();
        }

        var added = 0;
        for (var i = low; i <= high; i++)
        {
            added += _keys.Add(source.GetRowIdentity(i).Key) ? 1 : 0;
        }

        // A range replacing a selection that already was exactly that range changes nothing.
        return Changed(added > 0 || (cleared && _keys.Count != before));
    }

    /// <summary>Selects every row of the source; the cost is the row count, so it is for an explicit action.</summary>
    internal bool SelectAll<TRow>(ITableViewSource<TRow> source) => SelectRange(source, 0, source.RowCount - 1, true);

    /// <summary>
    /// Moves the keyboard to a row. A plain move selects it alone, Shift extends the range from the anchor, and
    /// Ctrl only moves, leaving the selection as it is.
    /// </summary>
    internal bool MoveTo<TRow>(ITableViewSource<TRow> source, TableSelectionMode mode, int row, bool ctrl, bool shift)
    {
        var key = source.GetRowIdentity(row).Key;
        var changed = false;
        if (mode != TableSelectionMode.None && !ctrl)
        {
            if (mode == TableSelectionMode.Multiple && shift)
            {
                if (AnchorIndex < 0)
                {
                    SetAnchor(CurrentIndex >= 0 ? CurrentIndex : row, CurrentKey ?? key);
                }

                changed = SelectRange(source, AnchorIndex, row, false);
            }
            else
            {
                changed = SetOnly(key);
                SetAnchor(row, key);
            }
        }

        SetCurrent(row, key);
        return changed;
    }

    /// <summary>Selects or unselects the keyboard's row, as Space does.</summary>
    internal bool ToggleCurrent(TableSelectionMode mode)
    {
        if (mode == TableSelectionMode.None || CurrentKey is not { } key)
        {
            return false;
        }

        SetAnchor(CurrentIndex, key);
        return mode == TableSelectionMode.Single ? SetOnly(key) : ToggleKey(key);
    }

    /// <summary>Remembers where the keyboard is, without touching the selection.</summary>
    internal void SetCurrent(int row, string key)
    {
        CurrentIndex = row;
        CurrentKey = key;
    }

    /// <summary>
    /// Keeps the remembered positions honest after the source's data changed: a position whose row no longer
    /// has the remembered identity is forgotten, so a range never starts from a row that moved away.
    /// </summary>
    internal void Reconcile<TRow>(ITableViewSource<TRow> source)
    {
        if (AnchorIndex >= 0 && !Holds(source, AnchorIndex, AnchorKey))
        {
            AnchorIndex = -1;
            AnchorKey = null;
        }

        if (CurrentIndex >= 0 && !Holds(source, CurrentIndex, CurrentKey))
        {
            CurrentIndex = -1;
            CurrentKey = null;
        }
    }

    void SetAnchor(int row, string key)
    {
        AnchorIndex = row;
        AnchorKey = key;
    }

    bool SetOnly(string key)
    {
        if (_keys.Count == 1 && _keys.Contains(key))
        {
            return false;
        }

        _keys.Clear();
        _keys.Add(key);
        Version++;
        return true;
    }

    bool ToggleKey(string key)
    {
        var selected = !_keys.Remove(key);
        if (selected)
        {
            _keys.Add(key);
        }

        Version++;
        return selected;
    }

    bool Changed(bool changed)
    {
        if (changed)
        {
            Version++;
        }

        return changed;
    }

    static bool Holds<TRow>(ITableViewSource<TRow> source, int index, string? key) =>
        index < source.RowCount && source.GetRowIdentity(index).Key == key;
}
