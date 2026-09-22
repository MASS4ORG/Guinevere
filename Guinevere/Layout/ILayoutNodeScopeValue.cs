namespace Guinevere;

/// <summary>
/// Represents a value that can be associated with a layout node scope.
/// Implementations of this interface provide a mechanism to define a default
/// value and are used for cascading or overriding specific settings within
/// a layout node hierarchy.
/// </summary>
public interface ILayoutNodeScopeValue
{
    /// <summary>The process-local storage slot assigned to this value type.</summary>
    int Slot { get; }
}

/// <summary>A strongly typed value that can cascade through layout node scopes.</summary>
/// <typeparam name="T">The implementing value type.</typeparam>
public interface ILayoutNodeScopeValue<out T> : ILayoutNodeScopeValue where T : ILayoutNodeScopeValue<T>
{
    int ILayoutNodeScopeValue.Slot => LayoutNodeScopeValueSlot<T>.Index;

    /// <summary>
    /// Gets the default instance of the implementing type. This property is used
    /// to provide a fallback value when a specific instance is not set or found
    /// in a layout node scope hierarchy.
    /// </summary>
    abstract static T Default { get; }
}

static class LayoutNodeScopeValueSlot<T>
{
    public static readonly int Index = LayoutNodeScopeValueSlots.Next();
}

static class LayoutNodeScopeValueSlots
{
    static int _next = -1;

    public static int Count => Volatile.Read(ref _next) + 1;

    public static int Next() => Interlocked.Increment(ref _next);
}
