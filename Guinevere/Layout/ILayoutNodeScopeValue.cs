namespace Guinevere;

/// <summary>
/// Represents a value that can be associated with a layout node scope.
/// Implementations of this interface provide a mechanism to define a default
/// value and are used for cascading or overriding specific settings within
/// a layout node hierarchy.
/// </summary>
/// <typeparam name="T">
/// The type of the implementing value, which must itself implement <see cref="ILayoutNodeScopeValue{T}"/>.
/// </typeparam>
public interface ILayoutNodeScopeValue<out T> where T : ILayoutNodeScopeValue<T>
{
    /// <summary>
    /// Gets the default instance of the implementing type. This property is used
    /// to provide a fallback value when a specific instance is not set or found
    /// in a layout node scope hierarchy.
    /// </summary>
    abstract static T Default { get; }
}
