namespace Guinevere;

/// <summary>
/// Represents a clipping operation specifically designed for scrollable containers.
/// This operation clips content to the viewport bounds, ensuring that scrolled content
/// outside the visible area is properly hidden.
/// </summary>
public class ScrollClipOperation : IDrawListEntry
{
    readonly Rect _viewportRect;

    /// <summary>
    /// Initializes a new instance of the ScrollClipOperation class.
    /// </summary>
    /// <param name="viewportRect">The viewport rectangle to clip to (in screen coordinates)</param>
    public ScrollClipOperation(Rect viewportRect)
    {
        _viewportRect = viewportRect;
    }

    /// <summary>
    /// Executes the scroll clip operation on the provided canvas.
    /// This clips rendering to the viewport bounds, preventing scrolled content
    /// from appearing outside the container.
    /// </summary>
    /// <param name="gui">The GUI instance managing the current state</param>
    /// <param name="node">The layout node associated with this clip operation</param>
    /// <param name="canvas">The canvas to apply the clipping to</param>
    public void Execute(Gui gui, LayoutNode node, SKCanvas canvas)
    {
        canvas.Save();

        // Always clip to the viewport rectangle
        // This ensures that content scrolled outside the visible area is hidden
        canvas.ClipRect(_viewportRect);
    }
}
