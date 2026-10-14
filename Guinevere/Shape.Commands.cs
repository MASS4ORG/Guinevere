using System.Runtime.CompilerServices;

namespace Guinevere;

/// <summary>
/// Represents a base abstract class for creating and manipulating 2D geometric shapes
/// with customizable rendering behaviors, transformations, and visual effects.
/// </summary>
public partial class Shape
{
    /// <summary>
    /// Combines the paths of two shapes into a single shape, resulting in a new shape representing their union.
    /// </summary>
    /// <param name="a">The first shape.</param>
    /// <param name="b">The second shape to combine with the first.</param>
    /// <returns>A new <see cref="Shape"/> representing the union of the two shapes.</returns>
    public static Shape operator +(Shape a, Shape b)
    {
        var op = new SKPath();
        a.Path.Op(b.Path, SKPathOp.Union, op);
        return new Shape(op);
    }

    /// <summary>
    /// Subtracts the path of one shape from another, resulting in a new shape representing the difference between the two.
    /// </summary>
    /// <param name="a">The first shape.</param>
    /// <param name="b">The second shape whose path will be subtracted from the first.</param>
    /// <returns>A new <see cref="Shape"/> representing the difference of the paths of the two shapes.</returns>
    public static Shape operator -(Shape a, Shape b)
    {
        var op = new SKPath();
        a.Path.Op(b.Path, SKPathOp.Difference, op);
        return new Shape(op) { Paint = a.Paint };
    }

    /// <summary>
    /// Computes the intersection of two shapes, resulting in a new shape representing their common area.
    /// </summary>
    /// <param name="a">The first shape.</param>
    /// <param name="b">The second shape to intersect with the first.</param>
    /// <returns>A new <see cref="Shape"/> representing the intersection of the two shapes.</returns>
    public static Shape operator *(Shape a, Shape b)
    {
        var op = new SKPath();
        a.Path.Op(b.Path, SKPathOp.Intersect, op);
        return new Shape(op) { Paint = a.Paint };
    }

    /// <summary>
    /// Expands a shape by the specified amount.
    /// </summary>
    /// <param name="shape">The shape to expand.</param>
    /// <param name="amount">The amount to expand the shape by.</param>
    /// <returns>A new <see cref="Shape"/> expanded by the specified amount.</returns>
    public static Shape operator +(Shape shape, float amount) => shape.Expand(amount);

    /// <summary>
    /// Contracts a shape by the specified amount.
    /// </summary>
    /// <param name="shape">The shape to contract.</param>
    /// <param name="amount">The amount to contract the shape by.</param>
    /// <returns>A new <see cref="Shape"/> contracted by the specified amount.</returns>
    public static Shape operator -(Shape shape, float amount) => shape.Expand(-amount);

    /// <summary>
    /// Expands a shape by the specified amount.
    /// </summary>
    /// <param name="shape">The shape to expand.</param>
    /// <param name="amount">The amount to expand the shape by.</param>
    /// <returns>A new <see cref="Shape"/> expanded by the specified amount.</returns>
    public static Shape operator +(Shape shape, int amount) => shape.Expand(amount);

    /// <summary>
    /// Contracts a shape by the specified amount.
    /// </summary>
    /// <param name="shape">The shape to contract.</param>
    /// <param name="amount">The amount to contract the shape by.</param>
    /// <returns>A new <see cref="Shape"/> contracted by the specified amount.</returns>
    public static Shape operator -(Shape shape, int amount) => shape.Expand(-amount);

    /// <summary>
    /// Offsets the filled boundary by the specified distance using rounded joins and caps.
    /// </summary>
    /// <param name="amount">The outward distance; a negative value contracts the filled shape.</param>
    /// <returns>A new shape with the offset fill and the current paint.</returns>
    public Shape Expand(float amount)
    {
        if (amount == 0)
        {
            return Copy();
        }

        var expandedPath = new SKPath();
        if (amount < 0 && -amount * 2 >= Math.Min(Path.Bounds.Width, Path.Bounds.Height))
        {
            return new Shape(expandedPath, Paint);
        }

        using var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Abs(amount) * 2,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round
        };
        using var strokePath = strokePaint.GetFillPath(Path);
        Path.Op(strokePath, amount > 0 ? SKPathOp.Union : SKPathOp.Difference, expandedPath);
        return new Shape(expandedPath, Paint);
    }

    /// <summary>
    /// Creates a deep copy of the current shape, including all its layers and properties.
    /// </summary>
    /// <returns>A new <see cref="Shape"/> instance that is a copy of the current shape.</returns>
    public Shape Copy()
    {
        var newPath = new SKPath(Path);
        SKPaint? newPaint = null;
        if (Paint != null)
        {
            newPaint = new SKPaint
            {
                Color = Paint.Color,
                Style = Paint.Style,
                IsAntialias = Paint.IsAntialias,
                StrokeWidth = Paint.StrokeWidth,
                Shader = Paint.Shader,
                ImageFilter = Paint.ImageFilter
            };
        }

        var shape = new Shape(newPath, newPaint ?? new SKPaint());

        // Copy all layers
        foreach (var (zIndex, layerList) in Layers)
        {
            foreach (var (layerPath, layerPaint) in layerList)
            {
                if (zIndex == 0)
                {
                    continue; // Skip main layer as it's already added in constructor
                }

                var copiedPath = new SKPath(layerPath);
                var copiedPaint = new SKPaint
                {
                    Color = layerPaint.Color,
                    Style = layerPaint.Style,
                    IsAntialias = layerPaint.IsAntialias,
                    StrokeWidth = layerPaint.StrokeWidth,
                    Shader = layerPaint.Shader,
                    ImageFilter = layerPaint.ImageFilter
                };
                shape.AddToLayer(zIndex, copiedPath, copiedPaint);
            }
        }

        shape.CopyShadows(this);
        return shape;
    }

    /// <summary>
    /// Combines filled shapes using a polynomial smooth minimum of their signed distances.
    /// Positive smoothness samples a bounded grid and can lose details smaller than one grid cell.
    /// </summary>
    /// <param name="shape1">The first shape to include in the union.</param>
    /// <param name="shape2">The second shape to include in the union.</param>
    /// <param name="smoothness">The blending radius in shape units; zero preserves the exact path union.</param>
    /// <returns>A new <see cref="Shape"/> representing the union of the two input shapes.</returns>
    public static Shape Union(Shape shape1, Shape shape2, int smoothness)
    {
        if (shape1.Path.IsEmpty)
        {
            return shape2.Copy();
        }

        if (shape2.Path.IsEmpty)
        {
            return shape1.Copy();
        }

        if (smoothness <= 0)
        {
            var union = new SKPath();
            shape1.Path.Op(shape2.Path, SKPathOp.Union, union);
            return new Shape(union, shape1.Paint);
        }

        var path = ShapeDistanceField.Blend(shape1.Path, shape2.Path, 0, smoothness);
        return new Shape(path, shape1.Paint);
    }

    /// <summary>
    /// Combines this shape with another shape using a union operation with smoothness.
    /// </summary>
    /// <param name="other">The other shape to union with this shape.</param>
    /// <param name="smoothness">The level of smoothness for the union operation.</param>
    /// <returns>A new <see cref="Shape"/> representing the union of the two shapes.</returns>
    public Shape Union(Shape other, int smoothness = 0)
    {
        return Union(this, other, smoothness);
    }

    /// <summary>
    /// Rotates the shape by a specified angle.
    /// </summary>
    /// <param name="angle">The angle by which to rotate the shape, specified in degrees.</param>
    /// <returns>The updated <see cref="Shape"/> after applying the rotation.</returns>
    public Shape Rotate(Angle angle)
    {
        var rotationMatrix = SKMatrix.CreateRotationDegrees(angle.Degree);
        Path.Transform(rotationMatrix);
        return this;
    }

    /// <summary>
    /// Scales the shape by the specified factors along the X and Y axes.
    /// </summary>
    /// <param name="scaleX">The scaling factor for the X-axis.</param>
    /// <param name="scaleY">The scaling factor for the Y-axis.</param>
    /// <returns>The updated <see cref="Shape"/> after applying the scaling.</returns>
    public Shape Scale(float scaleX, float scaleY)
    {
        var shape = Copy();
        var bounds = shape.Path.Bounds;
        var centerX = bounds.MidX;
        var centerY = bounds.MidY;

        var scaleMatrix = SKMatrix.CreateScale(scaleX, scaleY, centerX, centerY);
        shape.Path.Transform(scaleMatrix);

        foreach (var (_, layerList) in shape.Layers)
        {
            for (var i = 0; i < layerList.Count; i++)
            {
                var (layerPath, layerPaint) = layerList[i];
                if (ReferenceEquals(layerPath, shape.Path))
                {
                    continue;
                }

                layerPath.Transform(scaleMatrix);
                layerList[i] = (layerPath, layerPaint);
            }
        }

        return shape;
    }

    /// <summary>
    /// Scales the shape uniformly by the specified factor.
    /// </summary>
    /// <param name="scale">The uniform scaling factor.</param>
    /// <returns>The updated <see cref="Shape"/> after applying the scaling.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Shape Scale(float scale)
    {
        return Scale(scale, scale);
    }

    /// <summary>
    /// Interpolates the filled shapes' signed distances and paint, returning a copy at each endpoint.
    /// Geometry is sampled on a bounded grid, so details smaller than one grid cell may disappear.
    /// </summary>
    /// <param name="other">The shape to blend toward.</param>
    /// <param name="amount">The blending amount, clamped to the range zero through one.</param>
    /// <returns>A new shape representing the blend.</returns>
    public Shape Mix(Shape other, float amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        if (amount == 0)
        {
            return Copy();
        }

        if (amount == 1)
        {
            return other.Copy();
        }

        var path = Path.IsEmpty || other.Path.IsEmpty
            ? new SKPath()
            : ShapeDistanceField.Blend(Path, other.Path, amount);
        var paint = MixPaint(other, amount);
        return new Shape(path, paint);
    }

    SKPaint MixPaint(Shape other, float amount)
    {
        var paint = (amount < 0.5f ? Paint : other.Paint)?.Clone() ?? new SKPaint();
        if (Paint == null || other.Paint == null)
        {
            return paint;
        }

        paint.Color = BlendColors(Paint.Color, other.Paint.Color, amount);
        paint.IsAntialias = Paint.IsAntialias || other.Paint.IsAntialias;
        paint.StrokeWidth = Paint.StrokeWidth * (1 - amount) + other.Paint.StrokeWidth * amount;
        return paint;
    }

    /// <summary>
    /// Blends two colors based on the specified amount.
    /// </summary>
    /// <param name="color1">The first color.</param>
    /// <param name="color2">The second color.</param>
    /// <param name="amount">The blending amount between 0.0 and 1.0.</param>
    /// <returns>The blended color.</returns>
    static SKColor BlendColors(SKColor color1, SKColor color2, float amount)
    {
        var r = (byte)(color1.Red * (1 - amount) + color2.Red * amount);
        var g = (byte)(color1.Green * (1 - amount) + color2.Green * amount);
        var b = (byte)(color1.Blue * (1 - amount) + color2.Blue * amount);
        var a = (byte)(color1.Alpha * (1 - amount) + color2.Alpha * amount);

        return new SKColor(r, g, b, a);
    }

    /// <summary>
    /// Transforms a filled shape into a thick outline (onion ring) with the specified thickness.
    /// </summary>
    /// <param name="thickness">The thickness of the outline.</param>
    /// <returns>A new <see cref="Shape"/> representing the thick outline.</returns>
    public Shape Onion(float thickness)
    {
        // Create the outer boundary by expanding the shape
        var outerShape = Expand(thickness);

        // Create the inner boundary by contracting the shape
        var innerShape = Expand(-thickness);

        // Subtract the inner from the outer to create the ring
        var onionPath = new SKPath();
        outerShape.Path.Op(innerShape.Path, SKPathOp.Difference, onionPath);

        var result = new Shape(onionPath, Paint ?? new SKPaint());
        return result;
    }

    /// <summary>
    /// Moves the shape horizontally by the specified amount.
    /// </summary>
    /// <param name="deltaX">The amount to move the shape along the X-axis.</param>
    /// <returns>A new <see cref="Shape"/> with the translated position.</returns>
    public Shape MoveX(float deltaX)
    {
        var newPath = new SKPath(Path);
        newPath.Transform(SKMatrix.CreateTranslation(deltaX, 0));
        var result = new Shape(newPath, Paint ?? new SKPaint());

        // Transform all layers
        foreach (var (zIndex, layerList) in Layers)
        {
            if (zIndex == 0)
            {
                continue; // Skip main layer as it's already handled
            }

            foreach (var (layerPath, layerPaint) in layerList)
            {
                var transformedLayerPath = new SKPath(layerPath);
                transformedLayerPath.Transform(SKMatrix.CreateTranslation(deltaX, 0));
                result.AddToLayer(zIndex, transformedLayerPath, layerPaint);
            }
        }

        result.CopyShadows(this);
        return result;
    }

    /// <summary>
    /// Moves the shape vertically by the specified amount.
    /// </summary>
    /// <param name="deltaY">The amount to move the shape along the Y-axis.</param>
    /// <returns>A new <see cref="Shape"/> with the translated position.</returns>
    public Shape MoveY(float deltaY)
    {
        var newPath = new SKPath(Path);
        newPath.Transform(SKMatrix.CreateTranslation(0, deltaY));
        var result = new Shape(newPath, Paint ?? new SKPaint());

        // Transform all layers
        foreach (var (zIndex, layerList) in Layers)
        {
            if (zIndex == 0)
            {
                continue; // Skip main layer as it's already handled
            }

            foreach (var (layerPath, layerPaint) in layerList)
            {
                var transformedLayerPath = new SKPath(layerPath);
                transformedLayerPath.Transform(SKMatrix.CreateTranslation(0, deltaY));
                result.AddToLayer(zIndex, transformedLayerPath, layerPaint);
            }
        }

        result.CopyShadows(this);
        return result;
    }

    /// <summary>
    /// Moves the shape both horizontally and vertically by the specified amounts.
    /// </summary>
    /// <param name="deltaX">The amount to move the shape along the X-axis.</param>
    /// <param name="deltaY">The amount to move the shape along the Y-axis.</param>
    /// <returns>A new <see cref="Shape"/> with the translated position.</returns>
    public Shape Move(float deltaX, float deltaY)
    {
        var newPath = new SKPath(Path);
        newPath.Transform(SKMatrix.CreateTranslation(deltaX, deltaY));
        var result = new Shape(newPath, Paint ?? new SKPaint());

        // Transform all layers
        foreach (var (zIndex, layerList) in Layers)
        {
            if (zIndex == 0)
            {
                continue; // Skip main layer as it's already handled
            }

            foreach (var (layerPath, layerPaint) in layerList)
            {
                var transformedLayerPath = new SKPath(layerPath);
                transformedLayerPath.Transform(SKMatrix.CreateTranslation(deltaX, deltaY));
                result.AddToLayer(zIndex, transformedLayerPath, layerPaint);
            }
        }

        result.CopyShadows(this);
        return result;
    }

    /// <summary>
    /// Alias for MoveY - adds the specified amount to the Y position of the shape.
    /// </summary>
    /// <param name="deltaY">The amount to add to the Y position.</param>
    /// <returns>A new <see cref="Shape"/> with the translated position.</returns>
    public Shape AddY(float deltaY)
    {
        return MoveY(deltaY);
    }
}
