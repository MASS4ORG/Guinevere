namespace Guinevere;

/// <summary>What the user did to a <c>TabStrip</c> this frame.</summary>
/// <param name="Activated">The tab clicked, or null.</param>
/// <param name="Closed">The tab closed by its close button or a middle-click, or null.</param>
/// <param name="Dragged">The tab a drag started on, or null.</param>
public readonly record struct TabStripResult(
    TabStripItem? Activated,
    TabStripItem? Closed,
    TabStripItem? Dragged)
{
    /// <summary>Nothing happened.</summary>
    public static TabStripResult None { get; } = new(null, null, null);
}
