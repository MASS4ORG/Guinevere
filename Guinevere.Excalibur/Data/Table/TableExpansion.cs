namespace Guinevere;

/// <summary>
/// An optional capability of a source: finding where a row is now from its identity. A table needs it to keep
/// rows expanded across a sort or a change, because a row's index moves while its identity does not. A source
/// without it still shows expanded rows, but a change to its data collapses them, since the table never scans
/// a dataset to find a row.
/// </summary>
public interface ITableRowLookup
{
    /// <summary>The index of the row with this identity in the published snapshot, or -1 when it is not there.</summary>
    /// <param name="identity">The row's identity.</param>
    int IndexOf(RowIdentity identity);
}

/// <summary>
/// The rows a table shows expanded, kept by row identity. Expanding a row shows its details (see
/// <see cref="ITableRowDetails{TRow}"/>) below it. The table changes it when the user presses an expander or
/// Right and Left on the keyboard row; a host can change it too.
/// </summary>
public sealed class TableExpansion
{
    readonly Dictionary<string, Entry> _rows = new(StringComparer.Ordinal);

    /// <summary>The number of expanded rows.</summary>
    public int Count => _rows.Count;

    /// <summary>Incremented by every change, so a table knows to lay its rows out again.</summary>
    public int Version { get; private set; }

    /// <summary>Whether a row is expanded.</summary>
    /// <param name="identity">The row's identity.</param>
    public bool IsExpanded(RowIdentity identity) => _rows.ContainsKey(identity.Key);

    /// <summary>Expands or collapses a row.</summary>
    /// <param name="identity">The row's identity.</param>
    /// <param name="expanded">Whether it should be expanded.</param>
    /// <param name="index">
    /// The row's index now. It lets a source without <see cref="ITableRowLookup"/> place the row until its data
    /// changes; a source with the lookup does not need it.
    /// </param>
    /// <returns>Whether anything changed.</returns>
    public bool SetExpanded(RowIdentity identity, bool expanded, int? index = null)
    {
        if (!expanded)
        {
            return Changed(_rows.Remove(identity.Key));
        }

        if (_rows.TryGetValue(identity.Key, out var entry))
        {
            if (index is { } known && entry.Index != known)
            {
                entry.Index = known;
                Version++;
                return true;
            }

            return false;
        }

        _rows[identity.Key] = new Entry { Index = index ?? -1 };
        Version++;
        return true;
    }

    /// <summary>Expands a collapsed row and collapses an expanded one.</summary>
    /// <param name="identity">The row's identity.</param>
    /// <param name="index">The row's index now; see <see cref="SetExpanded"/>.</param>
    /// <returns>Whether the row is expanded now.</returns>
    public bool Toggle(RowIdentity identity, int? index = null)
    {
        var expand = !IsExpanded(identity);
        SetExpanded(identity, expand, index);
        return expand;
    }

    /// <summary>Collapses every row.</summary>
    /// <returns>Whether any row was expanded.</returns>
    public bool Clear()
    {
        var had = _rows.Count > 0;
        _rows.Clear();
        return Changed(had);
    }

    internal bool IsExpanded(string key) => _rows.Count > 0 && _rows.ContainsKey(key);

    /// <summary>The height of a row's details when it is expanded at that index, else zero.</summary>
    internal float DetailHeight(string key, int row) =>
        _rows.Count > 0 && _rows.TryGetValue(key, out var entry) && entry.Index == row ? entry.Height : 0f;

    /// <summary>The expanded rows with where they were last placed, copied so the table can resolve them while it edits.</summary>
    internal KeyValuePair<string, int>[] Placed() =>
        [.. _rows.Select(static pair => new KeyValuePair<string, int>(pair.Key, pair.Value.Index))];

    /// <summary>Records where a row is and how tall its details are, which does not change what the user chose.</summary>
    internal void Place(string key, int index, float height)
    {
        if (_rows.TryGetValue(key, out var entry))
        {
            entry.Index = index;
            entry.Height = height;
        }
    }

    /// <summary>Collapses a row the table could not place, as a source without a lookup must after a change.</summary>
    internal void Forget(string key) => Changed(_rows.Remove(key));

    bool Changed(bool changed)
    {
        if (changed)
        {
            Version++;
        }

        return changed;
    }

    sealed class Entry
    {
        public int Index = -1;
        public float Height;
    }
}
