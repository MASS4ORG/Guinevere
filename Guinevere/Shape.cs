using System.Runtime.CompilerServices;

namespace Guinevere;

/// <summary>
/// Represents a base abstract class for creating and manipulating 2D geometric shapes
/// with customizable rendering behaviors, transformations, and visual effects.
/// </summary>
public partial class Shape : IDrawable
{
    /// <summary>
    /// Gets the <see cref="SKPath"/> defining the geometric representation of the shape.
    /// This property represents the core structure of the shape, storing its path data,
    /// such as lines, curves, and other geometric elements.
    /// </summary>
    public SKPath Path { get; }

    /// <summary>
    /// Gets or sets the associated <see cref="LayoutNode"/> for the shape.
    /// This property represents the layout node that provides context and
    /// layout information for the shape during rendering.
    /// </summary>
    public LayoutNode? Node { get; set; }

    /// <summary>
    /// Represents a collection of layered path and paint elements associated with the shape.
    /// These layers allow for advanced rendering effects such as shadows, fills, and other
    /// visual modifications. Each layer is defined by a geometric path and its corresponding
    /// painting parameters with a Z-index for ordering.
    /// </summary>
    public readonly SortedDictionary<int, List<(SKPath path, SKPaint paint)>> Layers = new();

    /// <summary>
    /// Renders the shape, including its layers and main content, onto the specified canvas.
    /// This method draws layers in Z-order: negative layers first (outer shadows),
    /// then the main shape at layer 0, then positive layers (inner shadows).
    /// Applies cumulative scroll offset from parent scrollable containers.
    /// </summary>
    /// <param name="gui">The GUI context that facilitates rendering operations and state management.</param>
    /// <param name="node">The layout node associated with this shape, typically defining position and layout properties.</param>
    /// <param name="canvas">The canvas where the shape is rendered.</param>
    public virtual void Render(Gui gui, LayoutNode node, SKCanvas canvas)
    {
        // Render all layers in Z-order
        // Scroll offsets are now handled during layout calculation
        foreach (var (_, layerList) in Layers)
        foreach (var (path, paint) in layerList)
            canvas.DrawPath(path, paint);
    }

    /// <summary>
    /// Represents a two-dimensional geometric shape that can be created, transformed, and rendered.
    /// Provides methods for constructing basic shapes such as rectangles, rounded rectangles, circles, and arcs.
    /// </summary>
    protected Shape(SKPath path) :
        this(path, new SKPaint())
    {
    }

    /// <summary>
    /// Represents a 2D shape that can be rendered, transformed, and manipulated with various geometric operations.
    /// </summary>
    protected Shape(SKPath path, SKPaint? paint)
    {
        Path = path;
        Paint = paint ?? new SKPaint();
        // Add main shape at layer 0
        AddToLayer(0, Path, Paint);
    }

    /// <summary>
    /// Adds a path and paint to the specified layer.
    /// </summary>
    /// <param name="zIndex">The Z-index of the layer (negative for outer shadows, 0 for main shape, positive for inner shadows).</param>
    /// <param name="path">The path to add.</param>
    /// <param name="paint">The paint to use for the path.</param>
    internal void AddToLayer(int zIndex, SKPath path, SKPaint paint)
    {
        if (!Layers.ContainsKey(zIndex)) Layers[zIndex] = new List<(SKPath, SKPaint)>();

        Layers[zIndex].Add((path, paint));
    }

    /// <summary>
    /// Creates a straight line of the given thickness between two points.
    /// </summary>
    /// <param name="start">Where the line begins.</param>
    /// <param name="end">Where the line ends.</param>
    /// <param name="thickness">How thick the line is, in pixels.</param>
    /// <returns>A new <see cref="Shape"/> covering the line.</returns>
    public static Shape Line(Vector2 start, Vector2 end, float thickness = 1f)
    {
        var direction = end - start;
        var length = direction.Length();
        var half = Math.Max(0.5f, thickness / 2f);

        var normal = length > 0.0001f
            ? new Vector2(-direction.Y, direction.X) / length * half
            : new Vector2(half, 0);

        var builder = new SKPathBuilder();
        builder.MoveTo(start.X + normal.X, start.Y + normal.Y);
        builder.LineTo(end.X + normal.X, end.Y + normal.Y);
        builder.LineTo(end.X - normal.X, end.Y - normal.Y);
        builder.LineTo(start.X - normal.X, start.Y - normal.Y);
        builder.Close();

        return new Shape(builder.Detach());
    }

    /// <summary>
    /// Creates a rectangular shape from its edges.
    /// </summary>
    /// <param name="left">Left edge.</param>
    /// <param name="top">Top edge.</param>
    /// <param name="right">Right edge.</param>
    /// <param name="bottom">Bottom edge.</param>
    /// <returns>A new <see cref="Shape"/> for the rectangle.</returns>
    public static Shape Rect(
        float left, float top, float right, float bottom)
    {
        var builder = new SKPathBuilder();
        builder.AddRect(new SKRect(left, top, right, bottom));
        return new Shape(builder.Detach());
    }

    /// <summary>
    /// Creates a rectangular shape with rounded corners, defined by the specified bounds, corner radius, and optional rounded corner configuration.
    /// </summary>
    /// <param name="topLeftX">The X-coordinate of the top-left corner of the rectangle.</param>
    /// <param name="topLeftY">The Y-coordinate of the top-left corner of the rectangle.</param>
    /// <param name="bottomRightX">The X-coordinate of the bottom-right corner of the rectangle.</param>
    /// <param name="bottomRightY">The Y-coordinate of the bottom-right corner of the rectangle.</param>
    /// <param name="radius">The radius of the rounded corners.</param>
    /// <param name="corners">A bitwise combination of <see cref="Corner"/> values to specify which corners are rounded. Defaults to <see cref="Corner.All"/>.</param>
    /// <returns>A new <see cref="Shape"/> representing the specified rounded rectangle.</returns>
    public static Shape RoundRect(
        float topLeftX, float topLeftY, float bottomRightX, float bottomRightY,
        float radius, Corner corners = Corner.All)
    {
        var builder = new SKPathBuilder();
        var rect = new SKRect(topLeftX, topLeftY, bottomRightX, bottomRightY);
        var roundRect = new SKRoundRect();
        var radii = new[]
        {
            (corners & Corner.TopLeft) == Corner.TopLeft ? new SKPoint(radius, radius) : SKPoint.Empty,
            (corners & Corner.TopRight) == Corner.TopRight ? new SKPoint(radius, radius) : SKPoint.Empty,
            (corners & Corner.BottomRight) == Corner.BottomRight ? new SKPoint(radius, radius) : SKPoint.Empty,
            (corners & Corner.BottomLeft) == Corner.BottomLeft ? new SKPoint(radius, radius) : SKPoint.Empty
        };
        roundRect.SetRectRadii(rect, radii);

        builder.AddRoundRect(roundRect);
        return new Shape(builder.Detach());
    }

    /// <summary>
    /// Creates a circular shape defined by a radius and an optional center position.
    /// </summary>
    /// <param name="radius">The radius of the circle.</param>
    /// <param name="center">The center position of the circle. If not specified, the default center is at (0, 0).</param>
    /// <returns>A new <see cref="Shape"/> representing the specified circle.</returns>
    public static Shape Circle(float radius, Vector2? center = null)
    {
        var builder = new SKPathBuilder();
        var pos = center ?? new Vector2(0, 0);
        builder.AddCircle(pos.X, pos.Y, radius);
        return new Shape(builder.Detach());
    }

    /// <summary>
    /// Creates an arc-shaped <see cref="Shape"/> object based on the given parameters, including its position, thickness, and angular span.
    /// The arc includes rounded caps at its start and end points based on the specified thickness.
    /// </summary>
    /// <param name="centerRadius">The radius from the center of the arc to the middle of its thickness.</param>
    /// <param name="halfThickness">Half the thickness of the arc. This value determines how thick the arc will be.</param>
    /// <param name="start">The starting angle of the arc, represented in angular units such as degrees or radians.</param>
    /// <param name="turn">The angular span or sweep of the arc, beginning at the starting angle and extending clockwise.</param>
    /// <returns>A new <see cref="Shape"/> object representing the constructed arc.</returns>
    public static Shape Arc(float centerRadius, float halfThickness, Angle? start, Angle? turn = null)
    {
        var startFinal = start ?? 0;
        var turnFinal = turn ?? Angle.FullCircle;
        var outerRadius = centerRadius + halfThickness;
        var innerRadius = centerRadius - halfThickness;

        // Create outer and inner rectangles
        var outerRect = new SKRect(-outerRadius, -outerRadius, outerRadius, outerRadius);
        var innerRect = new SKRect(-innerRadius, -innerRadius, innerRadius, innerRadius);

        // Calculate the sweep angle, handling wrap-around
        var startDeg = startFinal;
        var endDeg = turnFinal + startFinal;
        var sweepAngle = turnFinal;

        // Normalize angles to handle wrap-around cases
        if (sweepAngle.Degree <= 0) sweepAngle += Angle.FullCircle;
        if (sweepAngle.Degree > 360) sweepAngle -= Angle.FullCircle;

        // Create the main arc body
        var arc = new SKPathBuilder();
        var outerStart = new SKPoint(
            MathF.Cos(startFinal.Radian) * outerRadius,
            MathF.Sin(startFinal.Radian) * outerRadius);
        var innerEnd = new SKPoint(
            MathF.Cos(endDeg.Radian) * innerRadius,
            MathF.Sin(endDeg.Radian) * innerRadius);

        arc.MoveTo(outerStart);
        arc.AddArc(outerRect, startDeg.Degree, sweepAngle.Degree);
        arc.LineTo(innerEnd);
        arc.AddArc(innerRect, endDeg.Degree, -sweepAngle.Degree);
        arc.LineTo(outerStart);
        arc.Close();

        // Calculate arc endpoints for rounded caps
        var startCenterX = MathF.Cos(startFinal.Radian) * centerRadius;
        var startCenterY = MathF.Sin(startFinal.Radian) * centerRadius;
        var endCenterX = MathF.Cos(endDeg.Radian) * centerRadius;
        var endCenterY = MathF.Sin(endDeg.Radian) * centerRadius;

        arc.AddCircle(startCenterX, startCenterY, halfThickness);
        arc.AddCircle(endCenterX, endCenterY, halfThickness);
        return new Shape(arc.Detach());
    }

    /// <summary>
    /// Creates a new pie-shaped shape with the specified center radius, half thickness, start angle, and turn angle.
    /// The shape is defined as a pie slice bounded by the outer and inner arcs based on the provided parameters.
    /// </summary>
    /// <param name="centerRadius">The radius of the center point of the pie shape, halfway between the inner and outer edges.</param>
    /// <param name="halfThickness">The thickness of the pie wedge, representing half the distance between the outer and inner radii.</param>
    /// <param name="start">The starting angle of the pie slice, where the arc begins in degrees or radians.</param>
    /// <param name="turn">The turn angle defining the extent of the pie slice, i.e., the angle between the start and end of the arc. Defaults to a full circle if not specified.</param>
    /// <returns>A new Shape instance configured as a pie wedge with the specified dimensions and angles.</returns>
    public static Shape Pie(float centerRadius, float halfThickness, Angle? start, Angle? turn = null)
    {
        var startFinal = start ?? 0;
        var turnFinal = turn ?? Angle.FullCircle;
        var outerRadius = centerRadius + halfThickness;
        var innerRadius = centerRadius - halfThickness;

        // Create outer and inner rectangles
        var outerRect = new SKRect(-outerRadius, -outerRadius, outerRadius, outerRadius);
        var innerRect = new SKRect(-innerRadius, -innerRadius, innerRadius, innerRadius);

        // Calculate the sweep angle, handling wrap-around
        var startDeg = startFinal;
        var endDeg = turnFinal + startFinal;
        var sweepAngle = turnFinal;

        // Normalize angles to handle wrap-around cases
        if (sweepAngle.Degree <= 0) sweepAngle += Angle.FullCircle;
        if (sweepAngle.Degree > 360) sweepAngle -= Angle.FullCircle;

        // Create the main arc body
        var arc = new SKPathBuilder();
        var outerStart = new SKPoint(
            MathF.Cos(startFinal.Radian) * outerRadius,
            MathF.Sin(startFinal.Radian) * outerRadius);
        var innerEnd = new SKPoint(
            MathF.Cos(endDeg.Radian) * innerRadius,
            MathF.Sin(endDeg.Radian) * innerRadius);

        arc.MoveTo(outerStart);
        arc.AddArc(outerRect, startDeg.Degree, sweepAngle.Degree);
        arc.LineTo(innerEnd);
        arc.AddArc(innerRect, endDeg.Degree, -sweepAngle.Degree);
        arc.LineTo(outerStart);
        arc.Close();

        return new Shape(arc.Detach());
    }

    /// <summary>
    /// Creates a triangular shape defined by three vertices.
    /// </summary>
    /// <param name="a">The first vertex of the triangle.</param>
    /// <param name="b">The second vertex of the triangle.</param>
    /// <param name="c">The third vertex of the triangle.</param>
    /// <returns>A new <see cref="Shape"/> representing the specified triangle.</returns>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Shape Triangle(Vector2 a, Vector2 b, Vector2 c)
    {
        return Triangle(a.X, a.Y, b.X, b.Y, c.X, c.Y);
    }

    /// <summary>
    /// Creates a triangular shape defined by three distinct vertices specified by their X and Y coordinates.
    /// </summary>
    /// <param name="aX">The X-coordinate of the first vertex of the triangle.</param>
    /// <param name="aY">The Y-coordinate of the first vertex of the triangle.</param>
    /// <param name="bX">The X-coordinate of the second vertex of the triangle.</param>
    /// <param name="bY">The Y-coordinate of the second vertex of the triangle.</param>
    /// <param name="cX">The X-coordinate of the third vertex of the triangle.</param>
    /// <param name="cY">The Y-coordinate of the third vertex of the triangle.</param>
    /// <returns>A new instance of <see cref="Shape"/> representing the triangle defined by the provided vertices.</returns>
    public static Shape Triangle(float aX, float aY, float bX, float bY, float cX, float cY)
    {
        var builder = new SKPathBuilder();
        builder.MoveTo(aX, aY);
        builder.LineTo(bX, bY);
        builder.LineTo(cX, cY);
        builder.Close();
        return new Shape(builder.Detach());
    }

    /// <summary>
    /// Creates an equilateral triangle shape with a specified knob radius. The triangle
    /// will have equal sides and can be used in rendering operations within the shape
    /// composition system.
    /// </summary>
    /// <param name="knobRadius">The radius of the knob, which determines the size of the equilateral triangle.</param>
    /// <returns>A Shape representing the equilateral triangle.</returns>
    public static Shape EquilateralTriangle(float knobRadius)
    {
        var height = knobRadius * MathF.Sqrt(3) / 2;
        var halfBase = knobRadius / 2;

        var builder = new SKPathBuilder();
        builder.MoveTo(0, -height * 2 / 3); // Top vertex
        builder.LineTo(-halfBase, height / 3); // Bottom left
        builder.LineTo(halfBase, height / 3); // Bottom right
        builder.Close();

        return new Shape(builder.Detach());
    }

    /// <summary>
    /// Creates a rectangular shape with specified width and height.
    /// </summary>
    /// <param name="width">The width of the rectangle.</param>
    /// <param name="height">The height of the rectangle.</param>
    /// <returns>A new <see cref="Shape"/> representing the rectangle.</returns>
    public static Shape Rectangle(float width, float height)
    {
        var builder = new SKPathBuilder();
        builder.AddRect(new SKRect(0, 0, width, height));
        return new Shape(builder.Detach());
    }

    /// <summary>
    /// Creates a rectangular shape with specified dimensions and corner radius.
    /// </summary>
    /// <param name="width">The width of the rectangle.</param>
    /// <param name="height">The height of the rectangle.</param>
    /// <param name="radius">The radius of the rounded corners.</param>
    /// <returns>A new <see cref="Shape"/> representing the rounded rectangle.</returns>
    public static Shape RectangleRounded(float width, float height, float radius)
    {
        var builder = new SKPathBuilder();
        var rect = new SKRect(0, 0, width, height);
        builder.AddRoundRect(rect, radius, radius);
        return new Shape(builder.Detach());
    }
}
