namespace Guinevere;

/// <summary>
/// Describes an in-progress drag: where the press started, where the pointer is now, and how far
/// it has travelled. Unlike <see cref="HoldArgs"/>, <see cref="Origin"/> is the real press point
/// rather than the previous frame's pointer position, so a drag threshold can be measured against it.
/// </summary>
public readonly struct DragArgs
{
    /// <summary>
    /// Gets the pointer position at the moment the button went down on this element.
    /// </summary>
    public Vector2 Origin { get; init; }

    /// <summary>
    /// Gets the current pointer position.
    /// </summary>
    public Vector2 CurrentPosition { get; init; }

    /// <summary>
    /// Gets the pointer movement since the previous frame.
    /// </summary>
    public Vector2 FrameDelta { get; init; }

    /// <summary>
    /// Gets the total movement since the press started.
    /// </summary>
    public Vector2 TotalDelta => CurrentPosition - Origin;
}
