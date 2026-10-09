namespace Guinevere;

/// <summary>Interns complete identity descriptors so hash collisions cannot alias unrelated nodes.</summary>
sealed class NodeIdentityRegistry
{
    readonly record struct CallSite(string FilePath, int LineNumber);
    readonly record struct Occurrence(int Parent, int Scope, int Site, bool Control);
    readonly record struct Identity(Occurrence Location, int Index, string? ExplicitId);
    readonly record struct ItemScope(int Parent, int Outer, int Item);

    readonly Dictionary<CallSite, int> _sites = new();
    readonly List<CallSite> _siteNames = [default];
    readonly Dictionary<Occurrence, int> _occurrences = new();
    readonly Dictionary<Identity, int> _identities = new();
    readonly List<Identity> _descriptors = [default];
    readonly List<string?> _names = [string.Empty];
    readonly Dictionary<Type, object> _items = new();
    readonly Dictionary<ItemScope, int> _scopes = new();
    readonly HashSet<int> _visitedScopes = [];
    int _itemCount;

    internal void BeginPass()
    {
        _occurrences.Clear();
        _visitedScopes.Clear();
    }

    internal int Automatic(int parent, int scope, string filePath, int lineNumber, bool control = false)
    {
        var location = new Occurrence(parent, scope, Site(filePath, lineNumber), control);
        _occurrences.TryGetValue(location, out var index);
        _occurrences[location] = index + 1;
        return Intern(new Identity(location, index, null));
    }

    internal int At(int parent, int scope, string filePath, int lineNumber, int index) =>
        Intern(new Identity(new Occurrence(parent, scope, Site(filePath, lineNumber), false), index, null));

    internal int Explicit(int parent, int scope, string id) =>
        Intern(new Identity(new Occurrence(parent, scope, 0, false), 0, id));

    internal int EnterItem<T>(int parent, int outer, T item) where T : notnull
    {
        if (!typeof(T).IsValueType && item is null) throw new ArgumentNullException(nameof(item));
        if (!_items.TryGetValue(typeof(T), out var table))
            _items[typeof(T)] = table = new Dictionary<T, int>();
        var items = (Dictionary<T, int>)table;
        if (!items.TryGetValue(item, out var token)) items.Add(item, token = checked(++_itemCount));
        var descriptor = new ItemScope(parent, outer, token);
        if (!_scopes.TryGetValue(descriptor, out var scope))
            _scopes.Add(descriptor, scope = checked(_scopes.Count + 1));
        if (!_visitedScopes.Add(scope))
            throw new InvalidOperationException("An item key must be unique within its parent and outer item scope.");
        return scope;
    }

    internal string Name(int key)
    {
        if (_names[key] is { } cached) return cached;
        var descriptor = _descriptors[key];
        var location = descriptor.Location;
        if (location.Scope != 0 || location.Control)
            return _names[key] = $"@node:{key}";
        if (descriptor.ExplicitId is { } explicitId) return _names[key] = explicitId;
        var site = _siteNames[location.Site];
        return _names[key] = $"{Name(location.Parent)}{site.FilePath}:{site.LineNumber} {descriptor.Index}";
    }

    int Site(string filePath, int lineNumber)
    {
        var site = new CallSite(filePath, lineNumber);
        if (_sites.TryGetValue(site, out var key)) return key;
        key = _siteNames.Count;
        _sites.Add(site, key);
        _siteNames.Add(site);
        return key;
    }

    int Intern(Identity descriptor)
    {
        if (_identities.TryGetValue(descriptor, out var key)) return key;
        key = _descriptors.Count;
        _identities.Add(descriptor, key);
        _descriptors.Add(descriptor);
        _names.Add(null);
        return key;
    }
}
