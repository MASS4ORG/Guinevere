namespace Guinevere;

/// <summary>
/// Represents an operation that applies or restores clipping to the provided canvas.
/// Allows specifying a shape to clip to or restoring the previous canvas state.
/// </summary>
public class ClipOperation : IDrawListEntry
{
    readonly Rect? _rect;
    readonly Shape? _shape;
    readonly Vector2? _position;

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

        if (_rect is { } clipRect)
        {
            if (ClipRectFor(gui, node, clipRect) is { } rect) canvas.ClipRect(rect);
        }
        else if (_shape is not null && _position is { } position)
        {
            canvas.ClipPath(new ShapePos(_shape.Path, _shape.Paint, position).Path);
        }
    }

    /// <summary>
    /// The rectangle to clip to, or null for none. A scrolling container clips to its own viewport rather than to
    /// the content-sized rect it was given; empty rectangles never clip.
    /// </summary>
    static Rect? ClipRectFor(Gui gui, LayoutNode node, Rect clipRect)
    {
        if (clipRect.W <= 0 || clipRect.H <= 0) return null;

        var scrolling = gui.GetScrollState(node.Id) is { } scroll && (scroll.IsScrollingX || scroll.IsScrollingY);
        if (!scrolling) return clipRect;

        var viewport = node.Rect;
        return viewport.W > 0 && viewport.H > 0 ? viewport : null;
    }
}
