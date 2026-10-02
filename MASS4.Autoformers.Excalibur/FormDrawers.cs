namespace MASS4.Autoformers;

/// <summary>
/// A scope of drawer registrations. Every <c>Add</c> returns an <see cref="IDisposable"/> that removes exactly
/// that registration, so a plugin can release what it added when it unloads. A scope falls back to its parent;
/// <see cref="Default"/> holds the built-ins and cannot be changed.
/// </summary>
/// <remarks>
/// Lookups are cached per scope and discarded on the next lookup after any registration changes, so a disposed
/// drawer is no longer referenced once the scopes still in use have drawn again. Registration is thread-safe;
/// drawing belongs to the GUI thread.
/// </remarks>
public sealed class FormDrawers
{
    static int _epoch;

    readonly FormDrawers? _parent;
    bool _frozen;
    readonly Lock _gate = new();
    readonly Dictionary<Type, List<Slot<IPropertyDrawer>>> _types = [];
    readonly Dictionary<Type, List<Slot<IAttributeDrawer>>> _attributes = [];
    readonly List<Predicate> _predicates = [];
    long _sequence;

    int _cacheEpoch = -1;
    ConditionalWeakTable<Type, Lookup> _typeCache = new();
    Predicate[]? _predicateCache;

    sealed class Slot<T>(T drawer)
    {
        public T Drawer { get; } = drawer;
    }

    sealed record Predicate(Func<FormField, bool> When, IPropertyDrawer Drawer, int Order, int Depth, long Sequence);

    sealed record Lookup(IPropertyDrawer? Drawer);

    /// <summary>The built-in drawers: vectors, colors, tooltips, titles, required errors and tints. Read-only.</summary>
    public static FormDrawers Default { get; } = CreateDefault();

    /// <summary>Creates a scope that falls back to <paramref name="parent"/>, or to <see cref="Default"/>.</summary>
    public FormDrawers(FormDrawers? parent = null) => _parent = parent ?? Default;

    /// <summary>The root scope behind <see cref="Default"/>, which has no parent.</summary>
    FormDrawers(bool isRoot) => _parent = isRoot ? null : Default;

    /// <summary>
    /// Registers a drawer for a value type; it also serves derived types, implementations of an interface and
    /// <see cref="Nullable{T}"/> of a struct. Used only for writable fields. The latest registration wins.
    /// </summary>
    public IDisposable Add(Type valueType, IPropertyDrawer drawer)
    {
        ArgumentNullException.ThrowIfNull(valueType);
        ArgumentNullException.ThrowIfNull(drawer);
        return Register(_types, valueType, drawer);
    }

    /// <summary>
    /// Registers a drawer chosen by a predicate on the field, such as a reference picker. Checked after type
    /// drawers, by ascending <paramref name="order"/>, inner scopes first; the first match draws.
    /// </summary>
    public IDisposable Add(Func<FormField, bool> when, IPropertyDrawer drawer, int order = 0)
    {
        ArgumentNullException.ThrowIfNull(when);
        ArgumentNullException.ThrowIfNull(drawer);
        ThrowIfFrozen();

        Predicate entry;
        lock (_gate)
        {
            entry = new Predicate(when, drawer, order, 0, _sequence++);
            _predicates.Add(entry);
        }

        Changed();
        return new Registration(() =>
        {
            lock (_gate) _predicates.Remove(entry);
            Changed();
        });
    }

    /// <summary>Registers a decorator for an attribute type and the attributes derived from it.</summary>
    public IDisposable Add<TAttribute>(IAttributeDrawer drawer) where TAttribute : Attribute
    {
        ArgumentNullException.ThrowIfNull(drawer);
        return Register(_attributes, typeof(TAttribute), drawer);
    }

    /// <summary>
    /// Registers every concrete <see cref="IPropertyDrawer"/> in the assemblies that carries
    /// <see cref="CustomEditorAttribute"/>, once per edited type. Disposing removes them all.
    /// </summary>
    public IDisposable AddCustomEditors(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var registrations = new List<IDisposable>();
        foreach (var type in assemblies.SelectMany(assembly => assembly.GetTypes()))
        {
            if (type.IsAbstract || !typeof(IPropertyDrawer).IsAssignableFrom(type)) continue;

            var edited = type.GetCustomAttributes<CustomEditorAttribute>().ToArray();
            if (edited.Length == 0) continue;

            var drawer = (IPropertyDrawer)Activator.CreateInstance(type)!;
            registrations.AddRange(edited.Select(attribute => Add(attribute.EditorType, drawer)));
        }

        return new Registration(() => registrations.ForEach(registration => registration.Dispose()));
    }

    /// <summary>The type drawer for a value type, searching this scope, then its parents.</summary>
    internal IPropertyDrawer? TypeDrawer(Type valueType)
    {
        Refresh();
        if (_typeCache.TryGetValue(valueType, out var hit)) return hit.Drawer;

        var lookup = new Lookup(Resolve(valueType));
        _typeCache.AddOrUpdate(valueType, lookup);
        return lookup.Drawer;
    }

    /// <summary>The first predicate drawer accepting the field.</summary>
    internal IPropertyDrawer? PredicateDrawer(FormField field)
    {
        Refresh();
        _predicateCache ??= [.. Predicates(0).OrderBy(p => p.Order).ThenBy(p => p.Depth).ThenBy(p => p.Sequence)];

        foreach (var predicate in _predicateCache)
            if (predicate.When(field)) return predicate.Drawer;

        return null;
    }

    /// <summary>The decorator for an attribute type or its nearest registered base, inner scopes first.</summary>
    internal IAttributeDrawer? AttributeDrawer(Type attributeType)
    {
        for (var current = attributeType; current is not null && current != typeof(Attribute); current = current.BaseType)
            if (Exact(static scope => scope._attributes, current) is { } drawer) return drawer;

        return null;
    }

    static FormDrawers CreateDefault()
    {
        var drawers = new FormDrawers(isRoot: true);
        drawers.Add(typeof(Vector2), VectorDrawer.Instance);
        drawers.Add(typeof(Vector3), VectorDrawer.Instance);
        drawers.Add(typeof(Vector4), VectorDrawer.Instance);
        drawers.Add(typeof(Color), ColorDrawer.Instance);
        drawers.Add<TooltipAttribute>(TooltipDrawer.Instance);
        drawers.Add<TitleAttribute>(TitleDrawer.Instance);
        drawers.Add<RequiredAttribute>(RequiredDrawer.Instance);
        drawers.Add<GuiColorAttribute>(GuiColorDrawer.Instance);
        drawers._frozen = true;
        return drawers;
    }

    IDisposable Register<T>(Dictionary<Type, List<Slot<T>>> map, Type key, T drawer)
    {
        ThrowIfFrozen();

        var slot = new Slot<T>(drawer);
        lock (_gate)
        {
            if (!map.TryGetValue(key, out var slots)) map[key] = slots = [];
            slots.Add(slot);
        }

        Changed();
        return new Registration(() =>
        {
            lock (_gate)
                if (map.TryGetValue(key, out var slots) && slots.Remove(slot) && slots.Count == 0)
                    map.Remove(key);
            Changed();
        });
    }

    IPropertyDrawer? Resolve(Type valueType)
    {
        for (var current = valueType; current is not null; current = current.BaseType)
        {
            if (Exact(static scope => scope._types, current) is { } drawer) return drawer;
            foreach (var face in current.GetInterfaces())
                if (Exact(static scope => scope._types, face) is { } byInterface) return byInterface;
        }

        return Nullable.GetUnderlyingType(valueType) is { } underlying ? Resolve(underlying) : null;
    }

    /// <summary>The latest registration for exactly <paramref name="key"/>, this scope first.</summary>
    T? Exact<T>(Func<FormDrawers, Dictionary<Type, List<Slot<T>>>> map, Type key) where T : class
    {
        for (var scope = this; scope is not null; scope = scope._parent)
            lock (scope._gate)
                if (map(scope).TryGetValue(key, out var slots) && slots.Count > 0)
                    return slots[^1].Drawer;

        return null;
    }

    IEnumerable<Predicate> Predicates(int depth)
    {
        Predicate[] own;
        lock (_gate) own = [.. _predicates];

        var mine = own.Select(p => p with { Depth = depth });
        return _parent is null ? mine : mine.Concat(_parent.Predicates(depth + 1));
    }

    void Refresh()
    {
        var current = Volatile.Read(ref _epoch);
        if (_cacheEpoch == current) return;

        _typeCache = new ConditionalWeakTable<Type, Lookup>();
        _predicateCache = null;
        _cacheEpoch = current;
    }

    static void Changed() => Interlocked.Increment(ref _epoch);

    void ThrowIfFrozen()
    {
        if (_frozen) throw new InvalidOperationException("FormDrawers.Default is read-only; register in a child scope.");
    }

    sealed class Registration(Action remove) : IDisposable
    {
        Action? _remove = remove;

        public void Dispose() => Interlocked.Exchange(ref _remove, null)?.Invoke();
    }
}
