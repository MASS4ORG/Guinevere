namespace Guinevere;

/// <summary>
/// The <c>DeferShape</c> class extends the <see cref="Shape"/> class and serves as a specialized geometric
/// shape that supports deferred rendering with customizable properties such as corner rounding and selective corner modification.
/// </summary>
public class DeferShape : Shape
{
    /// <summary>
    /// Defines the radius used to round the corners of a shape.
    /// This property determines the degree of curvature applied, with larger values resulting in more pronounced rounding.
    /// </summary>
    public float Radius;

    /// <summary>
    /// Specifies the corners of a shape to which a radius or modification is applied.
    /// This property determines which corners of the shape will be rounded or affected during rendering.
    /// </summary>
    public Corner Corners = Corner.All;

    /// <summary>
    /// Renders the current shape onto the specified canvas using the provided GUI and layout node.
    /// </summary>
    /// <param name="gui">The GUI instance providing context for the rendering operation.</param>
    /// <param name="node">The layout node defining the position and dimensions for rendering.</param>
    /// <param name="canvas">The canvas where the shape will be drawn.</param>
    public override void Render(Gui gui, LayoutNode node, SKCanvas canvas)
    {
        var shape = ImMath.ApproximatelyEquals(Radius, 0.0f)
            ? Shape.Rect(node.Rect.X, node.Rect.Y, node.Rect.X + node.Rect.W, node.Rect.Y + node.Rect.H)
            : RoundRect(node.Rect.X, node.Rect.Y, node.Rect.X + node.Rect.W, node.Rect.Y + node.Rect.H,
                Radius, Corners);
        canvas.DrawPath(shape.Path, Paint);
    }

    private DeferShape(SKPath path) : base(path)
    {
    }

    /// <summary>
    /// Creates a rectangular shape with specified boundaries.
    /// </summary>
    /// <param name="left">The x-coordinate of the left edge of the rectangle.</param>
    /// <param name="top">The y-coordinate of the top edge of the rectangle.</param>
    /// <param name="right">The x-coordinate of the right edge of the rectangle.</param>
    /// <param name="bottom">The y-coordinate of the bottom edge of the rectangle.</param>
    /// <returns>A <see cref="DeferShape"/> instance that represents the rectangular shape.</returns>
    public new static DeferShape Rect(
        float left, float top, float right, float bottom)
    {
        var builder = new SKPathBuilder();
        builder.AddRect(new SKRect(left, top, right, bottom));
        return new DeferShape(builder.Detach());
    }

    /// <summary>
    /// Creates a rectangular shape filled with a specified solid color, with options
    /// for rounded corners and a configurable radius.
    /// </summary>
    /// <param name="color">The color used to fill the rectangle.</param>
    /// <param name="radius">The radius of the rounded corners. Set to 0 for square corners.</param>
    /// <param name="corners">Specifies which corners of the rectangle should be rounded.</param>
    /// <returns>A <see cref="DeferShape"/> representing the filled rectangle with the specified attributes.</returns>
    public static DeferShape DrawRectFilled(
        Color color,
        float radius, Corner corners = Corner.All)
    {
        var path = Shape.Rect(0, 0, 0, 0);
        var shape = new DeferShape(path.Path) { Radius = radius, Corners = corners };


        return shape;
    }
}
