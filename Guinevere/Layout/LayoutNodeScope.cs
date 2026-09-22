namespace Guinevere;

/// <summary>
/// Represents a scope management class for layout nodes, which facilitates entering, exiting,
/// and managing specific properties of a layout node in a 2D or UI rendering context.
/// </summary>
public class LayoutNodeScope(ILayoutNodeEnterExit? nodeManager, LayoutNode node) : IDisposable
{
    /// <summary>
    /// Provides access to the current layout node within a specific scope.
    /// The property represents the layout node that is managed during the
    /// lifetime of the associated <see cref="LayoutNodeScope"/> instance.
    /// </summary>
    public LayoutNode Node { get; } = node;


    /// <summary>
    /// Releases all resources used by the <see cref="LayoutNodeScope"/> instance and exits the current layout node context.
    /// </summary>
    /// <remarks>
    /// This method ensures that any resources associated with the current layout node scope are properly released,
    /// and that the <see cref="ILayoutNodeEnterExit"/> manager, if provided, is notified to exit the node context.
    /// </remarks>
    public void Dispose()
    {
        nodeManager?.Exit();
    }

    /// <summary>
    /// Enters the current layout node context, typically used to prepare and set up the environment for drawing operations or related activities.
    /// </summary>
    /// <returns>Returns the current <see cref="LayoutNodeScope"/> after entering the layout node context.</returns>
    public LayoutNodeScope Enter()
    {
        nodeManager?.Enter(Node);
        return this;
    }

    /// <summary>
    /// Exits the current layout node context, performing necessary cleanup operations.
    /// </summary>
    public void Exit()
    {
        nodeManager?.Exit();
    }

    /// <summary>
    /// Sets the Z-index value for the layout node, determining its stacking order.
    /// </summary>
    /// <param name="index">The Z-index value to assign to the layout node.</param>
    /// <returns>Returns the current <see cref="LayoutNodeScope"/> instance for method chaining.</returns>
    public LayoutNodeScope SetZIndex(int index)
    {
        return Set(new LayoutNodeScopeZIndex { Value = index });
    }

    object?[] _records = node.Parent?.Scope._records ?? [];
    bool[]? _localRecords;
    bool _ownsRecords = node.Parent is null;

    /// <summary>
    /// Stores a record of the specified generic type <typeparamref name="T"/> within the current layout node scope.
    /// </summary>
    /// <typeparam name="T">The type of the record to store, which must be a reference type.</typeparam>
    /// <param name="record">The instance of the record to store. It replaces any existing record of the same type in this scope.</param>
    public LayoutNodeScope Set<T>(T record) where T : class
    {
        if (record is IEnumerable<ILayoutNodeScopeValue> records)
            return Set(records);

        Set(LayoutNodeScopeValueSlot<T>.Index, record);
        return this;
    }

    /// <summary>Applies several independently typed values to this scope.</summary>
    public LayoutNodeScope Set(IEnumerable<ILayoutNodeScopeValue> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        foreach (var record in records) Set(record.Slot, record);
        return this;
    }

    void Set(int slot, object record)
    {
        if (!_ownsRecords)
        {
            _records = (object?[])_records.Clone();
            _ownsRecords = true;
        }

        if (slot >= _records.Length)
            Array.Resize(ref _records, Math.Max(slot + 1, Math.Max(8, _records.Length * 2)));
        if (_localRecords is null || slot >= _localRecords.Length)
            Array.Resize(ref _localRecords, _records.Length);

        _records[slot] = record;
        _localRecords[slot] = true;
    }

    /// <summary>
    /// Retrieves a value from this scope's flattened inherited values.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to retrieve. Must implement <see cref="ILayoutNodeScopeValue{T}"/>.</typeparam>
    /// <returns>
    /// The inherited instance of the requested value if found.
    /// Returns the default value of <typeparamref name="TValue"/> if no value is found.
    /// </returns>
    public TValue Get<TValue>() where TValue : class, ILayoutNodeScopeValue<TValue>
    {
        var slot = LayoutNodeScopeValueSlot<TValue>.Index;
        if (slot < _records.Length && _records[slot] is { } val)
            return (TValue)val;

        return TValue.Default;
    }

    /// <summary>
    /// Returns <c>true</c> when a value of type <typeparamref name="T"/> was set directly on this
    /// scope, as opposed to inherited from a parent scope by <see cref="Get{TValue}"/>.
    /// </summary>
    public bool HasLocal<T>() where T : class
    {
        var slot = LayoutNodeScopeValueSlot<T>.Index;
        return _localRecords is not null && slot < _localRecords.Length && _localRecords[slot];
    }
}
