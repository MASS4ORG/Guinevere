using System.Collections.ObjectModel;

namespace Guinevere;

/// <summary>
/// Active stylesheets, lowest priority first, plus host token overrides layered above every sheet. Any change bumps
/// <see cref="Version"/> and clears the resolved-style cache.
/// </summary>
public sealed class StyleSheetCollection : Collection<StyleSheet>
{
    readonly Dictionary<string, string> _tokens = new(StringComparer.Ordinal);
    readonly StyleCache _cache = new();
    Dictionary<string, string>? _globals;

    /// <summary>Incremented whenever a sheet is added, removed or replaced, or a host token changes.</summary>
    public int Version { get; private set; }

    /// <summary>Host token overrides, keyed as <c>--name</c>.</summary>
    public IReadOnlyDictionary<string, string> Tokens => _tokens;

    internal int CachedCount => _cache.Count;

    /// <summary>Overrides the <c>$name</c> token for every sheet, above any sheet's own value.</summary>
    /// <param name="name">Token name, with or without the <c>$</c> or <c>--</c> prefix.</param>
    /// <param name="value">The token's declaration text.</param>
    public void SetToken(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _tokens[TokenKey(name)] = value;
        Invalidate();
    }

    /// <summary>Removes a host token override; returns whether one existed.</summary>
    /// <param name="name">Token name, with or without the <c>$</c> or <c>--</c> prefix.</param>
    public bool RemoveToken(string name)
    {
        if (!_tokens.Remove(TokenKey(name))) return false;
        Invalidate();
        return true;
    }

    /// <summary>
    /// Resolves <paramref name="target"/> through the cache. A hit without call-site variables, or whose style
    /// references no variables, returns the cached instance without allocating.
    /// </summary>
    /// <param name="target">The element being styled.</param>
    /// <param name="scopedVariables">Call-site variables, which override sheet and host tokens.</param>
    public ResolvedStyle Resolve(in StyleTarget target, IReadOnlyList<StyleVariable>? scopedVariables = null)
    {
        if (Count == 0) return ResolvedStyle.Empty;
        var globals = _globals ??= StyleResolver.LayerVariables(this, _tokens);
        if (!_cache.TryGet(target, out var entry))
        {
            entry = StyleResolver.ResolveEntry(this, target, globals);
            _cache.Add(target, entry);
        }
        return StyleResolver.ApplyScoped(entry, scopedVariables, globals);
    }

    /// <inheritdoc/>
    protected override void InsertItem(int index, StyleSheet item)
    {
        ArgumentNullException.ThrowIfNull(item);
        base.InsertItem(index, item);
        Invalidate();
    }

    /// <inheritdoc/>
    protected override void SetItem(int index, StyleSheet item)
    {
        ArgumentNullException.ThrowIfNull(item);
        base.SetItem(index, item);
        Invalidate();
    }

    /// <inheritdoc/>
    protected override void RemoveItem(int index)
    {
        base.RemoveItem(index);
        Invalidate();
    }

    /// <inheritdoc/>
    protected override void ClearItems()
    {
        base.ClearItems();
        Invalidate();
    }

    void Invalidate()
    {
        Version++;
        _cache.Clear();
        _globals = null;
    }

    static string TokenKey(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.StartsWith("--", StringComparison.Ordinal)) return name;
        return name[0] == '$' ? $"--{name[1..]}" : $"--{name}";
    }
}
