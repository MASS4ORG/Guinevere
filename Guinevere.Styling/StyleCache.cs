using System.Diagnostics.CodeAnalysis;

namespace Guinevere;

/// <summary>
/// Bounded memo of cascade results per <see cref="StyleTarget"/>, owned by a <see cref="StyleSheetCollection"/>
/// and cleared whenever the collection's version changes. Lookups do not allocate.
/// </summary>
sealed class StyleCache
{
    /// <summary>Entry limit; reaching it clears the cache so dynamic class names cannot grow it unbounded.</summary>
    public const int Capacity = 4096;

    readonly Dictionary<StyleCacheKey, StyleCacheEntry> _entries = new(StyleCacheKeyComparer.Instance);

    /// <summary>Number of cached targets.</summary>
    public int Count => _entries.Count;

    /// <summary>Finds the entry for <paramref name="target"/>, comparing its lists element by element.</summary>
    public bool TryGet(in StyleTarget target, [NotNullWhen(true)] out StyleCacheEntry? entry) =>
        _entries.TryGetValue(new StyleCacheKey(target), out entry);

    /// <summary>Stores an entry under a snapshot of <paramref name="target"/>, so later list reuse is safe.</summary>
    public void Add(in StyleTarget target, StyleCacheEntry entry)
    {
        if (_entries.Count >= Capacity) _entries.Clear();
        _entries[new StyleCacheKey(Snapshot(target, includeAncestors: true))] = entry;
    }

    /// <summary>Drops every entry.</summary>
    public void Clear() => _entries.Clear();

    static StyleTarget Snapshot(in StyleTarget target, bool includeAncestors)
    {
        StyleTarget[]? ancestors = null;
        if (includeAncestors && target.Ancestors is { Count: > 0 } source)
        {
            ancestors = new StyleTarget[source.Count];
            for (var i = 0; i < ancestors.Length; i++) ancestors[i] = Snapshot(source[i], includeAncestors: false);
        }
        return new StyleTarget(target.Type, target.Id, Copy(target.Classes) ?? [], target.State,
            Copy(target.Modifiers), ancestors);
    }

    static string[]? Copy(IReadOnlyList<string>? list)
    {
        if (list is null) return null;
        var copy = new string[list.Count];
        for (var i = 0; i < copy.Length; i++) copy[i] = list[i];
        return copy;
    }
}

/// <summary>A <see cref="StyleTarget"/> used as a cache key with element-wise list equality.</summary>
readonly struct StyleCacheKey(StyleTarget target)
{
    public readonly StyleTarget Target = target;
}

/// <summary>
/// Compares cache keys by type, id, state, classes, modifiers and nearest-first ancestors. Null and empty lists are
/// equal because selectors cannot tell them apart; an ancestor's own ancestors are ignored for the same reason.
/// </summary>
sealed class StyleCacheKeyComparer : IEqualityComparer<StyleCacheKey>
{
    public static readonly StyleCacheKeyComparer Instance = new();

    public bool Equals(StyleCacheKey x, StyleCacheKey y)
    {
        if (!SameElement(x.Target, y.Target)) return false;
        var a = x.Target.Ancestors;
        var b = y.Target.Ancestors;
        var count = a?.Count ?? 0;
        if (count != (b?.Count ?? 0)) return false;
        for (var i = 0; i < count; i++)
            if (!SameElement(a![i], b![i])) return false;
        return true;
    }

    public int GetHashCode(StyleCacheKey key)
    {
        var hash = new HashCode();
        AddElement(ref hash, key.Target);
        var ancestors = key.Target.Ancestors;
        var count = ancestors?.Count ?? 0;
        hash.Add(count);
        for (var i = 0; i < count; i++) AddElement(ref hash, ancestors![i]);
        return hash.ToHashCode();
    }

    static bool SameElement(in StyleTarget a, in StyleTarget b) =>
        a.State == b.State
        && string.Equals(a.Type, b.Type, StringComparison.Ordinal)
        && string.Equals(a.Id, b.Id, StringComparison.Ordinal)
        && SameList(a.Classes, b.Classes)
        && SameList(a.Modifiers, b.Modifiers);

    static bool SameList(IReadOnlyList<string>? a, IReadOnlyList<string>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        var count = a?.Count ?? 0;
        if (count != (b?.Count ?? 0)) return false;
        for (var i = 0; i < count; i++)
            if (!string.Equals(a![i], b![i], StringComparison.Ordinal)) return false;
        return true;
    }

    static void AddElement(ref HashCode hash, in StyleTarget target)
    {
        hash.Add(target.Type);
        hash.Add(target.Id);
        hash.Add((int)target.State);
        AddList(ref hash, target.Classes);
        AddList(ref hash, target.Modifiers);
    }

    static void AddList(ref HashCode hash, IReadOnlyList<string>? list)
    {
        var count = list?.Count ?? 0;
        hash.Add(count);
        for (var i = 0; i < count; i++) hash.Add(list![i]);
    }
}
