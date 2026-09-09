namespace Guinevere;

/// <summary>
/// Represents the text color value for layout node scopes.
/// </summary>
public class LayoutNodeScopeTextColor : ILayoutNodeScopeValue<LayoutNodeScopeTextColor>
{
    /// <summary>
    /// Gets the default text color value (black).
    /// </summary>
    public static LayoutNodeScopeTextColor Default => new() { Value = Color.Black };

    /// <summary>
    /// Gets the color value for text rendering.
    /// </summary>
    public required Color Value { get; init; }
}

/// <summary>
/// Represents the text size value for layout node scopes.
/// </summary>
public class LayoutNodeScopeTextSize : ILayoutNodeScopeValue<LayoutNodeScopeTextSize>
{
    /// <summary>
    /// Gets the default text size value (20 pixels).
    /// </summary>
    public static LayoutNodeScopeTextSize Default => new() { Value = 12f};

    /// <summary>
    /// Gets the size value for text rendering.
    /// </summary>
    public required float Value { get; init; }
}

/// <summary>
/// Represents the text font value for layout node scopes.
/// </summary>
public class LayoutNodeScopeTextFont : ILayoutNodeScopeValue<LayoutNodeScopeTextFont>
{
    /// <summary>
    /// Gets the default text font value.
    /// </summary>
    public static LayoutNodeScopeTextFont Default => new() { Value = new Font() };

    /// <summary>
    /// Gets the font value for text rendering.
    /// </summary>
    public required Font Value { get; init; }
}

/// <summary>
/// Represents the icon font value for layout node scopes.
/// </summary>
public class LayoutNodeScopeIconFont : ILayoutNodeScopeValue<LayoutNodeScopeIconFont>
{
    /// <summary>
    /// Gets the default icon font value.
    /// </summary>
    public static LayoutNodeScopeIconFont Default => new() { Value = new Font() };

    /// <summary>
    /// Gets the font value for icon rendering.
    /// </summary>
    public required Font Value { get; init; }
}

/// <summary>
/// Represents the Z-index value for layout node scopes.
/// </summary>
public class LayoutNodeScopeZIndex : ILayoutNodeScopeValue<LayoutNodeScopeZIndex>
{
    /// <summary>
    /// Gets the default Z-index value (0).
    /// </summary>
    public static LayoutNodeScopeZIndex Default => new() { Value = 0 };

    /// <summary>
    /// Gets the Z-index value for rendering order.
    /// </summary>
    public required int Value { get; init; }
}

/// <summary>
/// Represents the scroll container ID value for layout node scopes.
/// </summary>
public class LayoutNodeScopeScrollContainerId : ILayoutNodeScopeValue<LayoutNodeScopeScrollContainerId>
{
    /// <summary>
    /// Gets the default scroll container ID value (null).
    /// </summary>
    public static LayoutNodeScopeScrollContainerId Default => new() { Value = null };

    /// <summary>
    /// Gets the scroll container ID value.
    /// </summary>
    [UsedImplicitly]
    public required string? Value { get; init; }
}

/// <summary>
/// Represents the clipping state value for layout node scopes.
/// </summary>
public class LayoutNodeScopeIsClipped : ILayoutNodeScopeValue<LayoutNodeScopeIsClipped>
{
    /// <summary>
    /// Gets the default clipping state value (false).
    /// </summary>
    public static LayoutNodeScopeIsClipped Default => new() { Value = false };

    /// <summary>
    /// Gets the clipping state value.
    /// </summary>
    public required bool Value { get; init; }
}

/// <summary>
/// Represents the cumulative scroll offset value for layout node scopes.
/// </summary>
public class LayoutNodeScopeCumulativeScrollOffset : ILayoutNodeScopeValue<LayoutNodeScopeCumulativeScrollOffset>
{
    /// <summary>
    /// Gets the default cumulative scroll offset value (Vector2.Zero).
    /// </summary>
    public static LayoutNodeScopeCumulativeScrollOffset Default => new() { Value = Vector2.Zero };

    /// <summary>
    /// Gets the cumulative scroll offset value.
    /// </summary>
    [UsedImplicitly]
    public required Vector2 Value { get; init; }
}

/// <summary>
/// Represents the scroll container state value for layout node scopes.
/// </summary>
public class LayoutNodeScopeIsScrollContainer : ILayoutNodeScopeValue<LayoutNodeScopeIsScrollContainer>
{
    /// <summary>
    /// Gets the default scroll container state value (false).
    /// </summary>
    public static LayoutNodeScopeIsScrollContainer Default => new() { Value = false };

    /// <summary>
    /// Gets the scroll container state value.
    /// </summary>
    [UsedImplicitly]
    public required bool Value { get; init; }
}

/// <summary>
/// Represents the local scroll offset value for layout node scopes.
/// </summary>
public class LayoutNodeScopeLocalScrollOffset : ILayoutNodeScopeValue<LayoutNodeScopeLocalScrollOffset>
{
    /// <summary>
    /// Gets the default local scroll offset value (Vector2.Zero).
    /// </summary>
    public static LayoutNodeScopeLocalScrollOffset Default => new() { Value = Vector2.Zero };

    /// <summary>
    /// Gets the local scroll offset value.
    /// </summary>
    public required Vector2 Value { get; init; }
}
