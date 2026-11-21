namespace Guinevere;

/// <summary>
/// Interns complete identity descriptors so hash collisions cannot alias unrelated nodes. Identities, item scopes,
/// item keys and data scopes that go unused for <see cref="RetainPasses"/> passes are evicted, so memory follows what
/// is on screen rather than everything ever shown; persistent data pins its identity and all it depends on.
/// </summary>
sealed class NodeIdentityRegistry
{
    readonly record struct CallSite(string FilePath, int LineNumber);
    readonly record struct Occurrence(int Parent, int Scope, int Site, bool Control, int Data = 0);
    readonly record struct Identity(Occurrence Location, int Index, string? ExplicitId);
    readonly record struct ItemScope(int Parent, int Outer, int Item, int Data);
    readonly record struct DataScopeKey(int Outer, int ItemScope, int Key);

    const int SweepInterval = 64;

    readonly Dictionary<CallSite, int> _sites = [];
    readonly List<CallSite> _siteNames = [default];
    readonly Dictionary<Occurrence, int> _occurrences = [];
    readonly NodeIdentityKeySpace _identityKeys = new();
    readonly NodeIdentityKeySpace _scopeKeys = new();
    readonly NodeIdentityKeySpace _itemKeys = new();
    readonly NodeIdentityKeySpace _dataKeys = new();
    readonly NodeIdentityInterner<Identity> _identities;
    readonly NodeIdentityInterner<ItemScope> _scopes;
    readonly NodeIdentityInterner<DataScopeKey> _dataScopes;
    readonly Dictionary<Type, object> _items = [];
    readonly HashSet<int> _visitedScopes = [];
    string?[] _names = new string?[64];
    long _nameSerial;
    int _pass;

    internal NodeIdentityRegistry()
    {
        _identities = new NodeIdentityInterner<Identity>(_identityKeys, ForgetName);
        _scopes = new NodeIdentityInterner<ItemScope>(_scopeKeys);
        _dataScopes = new NodeIdentityInterner<DataScopeKey>(_dataKeys);
    }

    /// <summary>How many passes (two per frame) an unused, unpinned entry is kept before eviction.</summary>
    internal int RetainPasses { get; set; } = 1200;

    /// <summary>Number of identities currently interned.</summary>
    internal int Count => _identityKeys.Count;

    internal void BeginPass()
    {
        _occurrences.Clear();
        _visitedScopes.Clear();
        _pass++;
        if (_pass % SweepInterval == 0)
        {
            var horizon = _pass - RetainPasses;
            _identityKeys.Sweep(_pass, horizon);
            _scopeKeys.Sweep(_pass, horizon);
            _dataKeys.Sweep(_pass, horizon);
            _itemKeys.Sweep(_pass, horizon);
        }
    }

    internal int Automatic(int parent, int scope, string filePath, int lineNumber, bool control = false,
        int data = 0)
    {
        var location = new Occurrence(parent, scope, Site(filePath, lineNumber), control, data);
        _occurrences.TryGetValue(location, out var index);
        _occurrences[location] = index + 1;
        return _identities.Intern(new Identity(location, index, null), _pass);
    }

    internal int At(int parent, int scope, string filePath, int lineNumber, int index, int data = 0) =>
        _identities.Intern(new Identity(new Occurrence(parent, scope, Site(filePath, lineNumber), false, data), index,
            null), _pass);

    internal int Explicit(int parent, int scope, string id, int data = 0) =>
        _identities.Intern(new Identity(new Occurrence(parent, scope, 0, false, data), 0, id), _pass);

    internal int DataScope<T>(int outer, int itemScope, T key) where T : notnull =>
        _dataScopes.Intern(new DataScopeKey(outer, itemScope, ItemToken(key)), _pass);

    internal int EnterItem<T>(int parent, int outer, T item, int data = 0) where T : notnull
    {
        var scope = _scopes.Intern(new ItemScope(parent, outer, ItemToken(item), data), _pass);
        if (!_visitedScopes.Add(scope))
        {
            throw new InvalidOperationException("An item key must be unique within its parent and outer item scope.");
        }

        return scope;
    }

    /// <summary>
    /// Keeps <paramref name="identity"/> and everything it depends on (parents, item scopes, item keys, data scopes)
    /// for the life of the registry, because persistent data is stored under it.
    /// </summary>
    internal void Pin(int identity)
    {
        if (!_identityKeys.Pin(identity))
        {
            return;
        }

        var location = _identities[identity].Location;
        Pin(location.Parent);
        PinScope(location.Scope);
        PinData(location.Data);
    }

    /// <summary>Whether <paramref name="identity"/> is kept forever.</summary>
    internal bool IsPinned(int identity) => _identityKeys.IsPinned(identity);

    void PinScope(int scope)
    {
        if (!_scopeKeys.Pin(scope))
        {
            return;
        }

        var descriptor = _scopes[scope];
        Pin(descriptor.Parent);
        PinScope(descriptor.Outer);
        _itemKeys.Pin(descriptor.Item);
        PinData(descriptor.Data);
    }

    void PinData(int data)
    {
        if (!_dataKeys.Pin(data))
        {
            return;
        }

        var descriptor = _dataScopes[data];
        PinData(descriptor.Outer);
        PinScope(descriptor.ItemScope);
        _itemKeys.Pin(descriptor.Key);
    }

    int ItemToken<T>(T item) where T : notnull
    {
        if (!typeof(T).IsValueType && item is null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        if (!_items.TryGetValue(typeof(T), out var table))
        {
            _items[typeof(T)] = table = new NodeIdentityInterner<T>(_itemKeys);
        }

        return ((NodeIdentityInterner<T>)table).Intern(item, _pass);
    }

    /// <summary>
    /// The identity's string name: its explicit id, or its parent's name plus its call site and occurrence. Item-,
    /// data- and control-scoped identities get a serial name that is never handed out twice, so a reused key never
    /// inherits state kept under an earlier name.
    /// </summary>
    internal string Name(int key)
    {
        if (key == 0)
        {
            return string.Empty;
        }

        if (key < _names.Length && _names[key] is { } cached)
        {
            return cached;
        }

        var descriptor = _identities[key];
        var location = descriptor.Location;
        string name;
        if (location.Scope != 0 || location.Control || location.Data != 0)
        {
            name = $"@node:{++_nameSerial}";
        }
        else if (descriptor.ExplicitId is { } explicitId)
        {
            name = explicitId;
        }
        else
        {
            var site = _siteNames[location.Site];
            name = $"{Name(location.Parent)}{site.FilePath}:{site.LineNumber} {descriptor.Index}";
        }

        if (key >= _names.Length)
        {
            Array.Resize(ref _names, Math.Max(_names.Length * 2, _identityKeys.Capacity));
        }

        return _names[key] = name;
    }

    internal (string File, int Line) Source(int identity)
    {
        var site = _siteNames[_identities[identity].Location.Site];
        return (site.FilePath ?? string.Empty, site.LineNumber);
    }

    void ForgetName(int key)
    {
        if (key < _names.Length)
        {
            _names[key] = null;
        }
    }

    int Site(string filePath, int lineNumber)
    {
        var site = new CallSite(filePath, lineNumber);
        if (_sites.TryGetValue(site, out var key))
        {
            return key;
        }

        key = _siteNames.Count;
        _sites.Add(site, key);
        _siteNames.Add(site);
        return key;
    }
}
