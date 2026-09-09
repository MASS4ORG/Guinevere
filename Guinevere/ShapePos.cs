namespace Guinevere;

/// <summary>
/// Represents a shape with positional and potentially rounded rectangle configurations.
/// This class extends the functionality of the <see cref="Shape"/> class.
/// </summary>
public class ShapePos : Shape
{
    /// <summary>
    /// Gets the position of the shape as a <see cref="Vector2"/> value.
    /// </summary>
    /// <remarks>
    /// The <c>Position</c> property represents the 2D coordinates of the shape within its containing space.
    /// It is used to define the transformation and placement of the shape.
    /// The position is typically set during the instantiation of the <see cref="ShapePos"/> object.
    /// </remarks>
    public Vector2 Position { get; }

    /// <inheritdoc />
    public ShapePos(SKPath path, Vector2 position)
        : this(path, null, position)
    {
        Position = position;
    }

    /// <inheritdoc />
    public ShapePos(SKPath path, SKPaint? paint, Vector2 position)
        : base(path, paint)
    {
        Position = position;
        path.Transform(SKMatrix.CreateTranslation(Position.X, Position.Y));
    }

    /// <summary>
    /// Creates a rectangle shape based on the specified rectangular dimensions.
    /// </summary>
    /// <param name="rect">The dimensions of the rectangle defined by the <see cref="Rect"/> object.</param>
    /// <returns>A new instance of <see cref="ShapePos"/> representing the rectangular shape.</returns>
    public static ShapePos Rectangle(Rect rect)
    {
        var builder = new SKPathBuilder();
        builder.AddRect(new SKRect(rect.X, rect.Y, rect.X + rect.W, rect.Y + rect.H));
        return new ShapePos(builder.Detach(), rect.Position);
    }

    /// <summary>
    /// Creates a rounded rectangle shape with the specified additional height and corner radius.
    /// </summary>
    /// <param name="node">The additional height for the shape. Can be a float, int, or other valid type convertible to height.</param>
    /// <param name="radius">The corner radius to apply to the rectangle.</param>
    /// <returns>A new instance of <see cref="ShapePos"/> representing the rounded rectangle shape.</returns>
    public static ShapePos RectangleRounded(LayoutNode node, float radius)
    {
        var builder = new SKPathBuilder();
        builder.AddRoundRect(node.Rect, radius, radius);
        return new ShapePos(builder.Detach(), node.Rect.Position);
    }
}
