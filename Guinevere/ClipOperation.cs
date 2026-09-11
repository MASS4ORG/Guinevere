namespace Guinevere;

/// <summary>
/// Represents an operation that applies or restores clipping to the provided canvas.
/// Allows specifying a shape to clip to or restoring the previous canvas state.
/// </summary>
public class ClipOperation : IDrawListEntry
{
    private readonly Rect? _rect;
    private readonly Shape? _shape;
    private readonly Vector2? _position;

    /// <summary>
    /// Represents an operation that applies clipping to a specified shape
    /// or restores the previous clipping state on a canvas.
    /// </summary>
    public ClipOperation(Shape shape, Vector2 position)
    {
        _shape = shape;
        _position = position;
    }

    /// <summary>
    /// Represents an operation to apply or restore a clipping region on a canvas.
    /// Provides functionality to limit rendering to a specified rectangular area or reset the clipping state.
    /// </summary>
    public ClipOperation(Rect rect)
    {
        _rect = rect;
    }

    /// <summary>
    /// Executes the clip operation on the provided canvas, applying clipping to the specified node's bounds
    /// or restoring the canvas state if required. For scrollable containers, clips to the viewport bounds.
    /// </summary>
    /// <param name="gui">The GUI instance managing the current state and operations.</param>
    /// <param name="node">The layout node to which the clip operation is applied.</param>
    /// <param name="canvas">The canvas on which the clip operation is performed.</param>
    public void Execute(Gui gui, LayoutNode node, SKCanvas canvas)
    {
        canvas.Save();

        if (_rect != null)
        {
            var clipRect = _rect;

            // Validate that we have proper dimensions before clipping
            if (clipRect.W <= 0 || clipRect.H <= 0)
                // If dimensions are invalid, don't apply clipping
                return;

            // For scrollable containers, ensure we're clipping to the correct viewport
            var scrollState = gui.GetScrollState(node.Id);
            if (scrollState != null && (scrollState.IsScrollingX || scrollState.IsScrollingY))
            {
                // Use the node's current inner rect as the viewport bounds
                // This ensures we clip to the actual container size, not the content size
                var viewportRect = node.Rect;

                // Only apply clipping if the viewport has valid dimensions
                if (viewportRect is { W: > 0, H: > 0 }) canvas.ClipRect(viewportRect);
            }
            else
            {
                // For non-scrollable content, use the provided rect
                canvas.ClipRect(clipRect);
            }
        }
        else if (_shape != null && _position is not null)
        {
            var shape = new ShapePos(_shape.Path, _shape.Paint, _position.Value);
            canvas.ClipPath(shape.Path);
        }
    }
}
