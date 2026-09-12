namespace Guinevere;

/// <summary>
/// A dock tree living in its own window on top of the dock space.
/// </summary>
/// <param name="root">The tree shown in the window.</param>
/// <param name="bounds">The window's screen-space rect.</param>
public sealed class DockFloat(DockNode root, Rect bounds)
{
    /// <summary>
    /// The tree shown in the window.
    /// </summary>
    public DockNode Root { get; set; } = root;

    /// <summary>
    /// The window's screen-space rect, updated as the window is moved or resized.
    /// </summary>
    public Rect Bounds { get; set; } = bounds;
}
