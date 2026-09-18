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
    ///
    /// </summary>
    /// <param name="amount"></param>
    /// <returns></returns>
    public Shape Expand(float amount)
    {
        var expandedPath = new SKPath();

        if (amount > 0)
        {
            // Expand outward by creating a stroke and then filling it
            var strokePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = amount * 2,
                StrokeJoin = SKStrokeJoin.Round,
                StrokeCap = SKStrokeCap.Round
            };

            var strokePath = strokePaint.GetFillPath(Path);

            // Union the original path with the stroke to create a filled expanded shape
            Path.Op(strokePath, SKPathOp.Union, expandedPath);
        }
        else
        {
            // Contract inward by scaling down
            var scaleFactor = 1 + amount / Math.Max(Path.Bounds.Width, Path.Bounds.Height);
            scaleFactor = Math.Max(0.1f, scaleFactor); // Prevent negative scaling

            var bounds = Path.Bounds;
            var centerX = bounds.MidX;
            var centerY = bounds.MidY;

            var transform = SKMatrix.CreateScale(scaleFactor, scaleFactor, centerX, centerY);
            Path.Transform(transform, expandedPath);
        }

        return new Shape(expandedPath) { Paint = Paint };
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
            newPaint = new SKPaint
            {
                Color = Paint.Color,
                Style = Paint.Style,
                IsAntialias = Paint.IsAntialias,
                StrokeWidth = Paint.StrokeWidth,
                Shader = Paint.Shader,
                ImageFilter = Paint.ImageFilter
            };

        var shape = new Shape(newPath, newPaint ?? new SKPaint());

        // Copy all layers
        foreach (var (zIndex, layerList) in Layers)
            foreach (var (layerPath, layerPaint) in layerList)
            {
                if (zIndex == 0) continue; // Skip main layer as it's already added in constructor

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

        return shape;
    }

    /// <summary>
    /// Combines two shapes into a single shape using a union operation, allowing customization
    /// of the result's smoothness.
    /// </summary>
    /// <param name="shape1">The first shape to include in the union.</param>
    /// <param name="shape2">The second shape to include in the union.</param>
    /// <param name="smoothness">The level of smoothness for the union operation. A higher value results in smoother transitions.</param>
    /// <returns>A new <see cref="Shape"/> representing the union of the two input shapes.</returns>
    public static Shape Union(Shape shape1, Shape shape2, int smoothness)
    {
        if (smoothness <= 0) return shape1 + shape2;

        // First create a basic union
        var unionPath = new SKPath();
        shape1.Path.Op(shape2.Path, SKPathOp.Union, unionPath);

        // Apply morphological operations to smooth the union
        var smoothedPath = SmoothUnion(unionPath, smoothness);

        var result = new Shape(smoothedPath);
        result.Paint = shape1.Paint ?? shape2.Paint;

        return result;
    }

    /// <summary>
    /// Smooths a union by applying morphological operations to create organic blending.
    /// </summary>
    /// <param name="unionPath">The path representing the union of two shapes.</param>
    /// <param name="smoothness">The amount of smoothing to apply.</param>
    /// <returns>A smoothed path with organic transitions.</returns>
    static SKPath SmoothUnion(SKPath unionPath, float smoothness)
    {
        // Step 1: Dilate (expand) the union to fill gaps and smooth concave areas
        var dilatePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = smoothness,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round
        };
        var dilatedPath = dilatePaint.GetFillPath(unionPath);

        // Step 2: Unite the original with the dilated version
        var expandedPath = new SKPath();
        unionPath.Op(dilatedPath, SKPathOp.Union, expandedPath);

        // Step 3: Erode (contract) back to approximate original size but with smoothed transitions
        var erodedPath = new SKPath();

        // Create an inward stroke by scaling down
        var bounds = expandedPath.Bounds;
        var centerX = bounds.MidX;
        var centerY = bounds.MidY;
        var maxDim = Math.Max(bounds.Width, bounds.Height);
        var scaleFactor = (maxDim - smoothness * 0.8f) / maxDim;
        scaleFactor = Math.Max(0.1f, scaleFactor);

        var scaleMatrix = SKMatrix.CreateScale(scaleFactor, scaleFactor, centerX, centerY);
        expandedPath.Transform(scaleMatrix, erodedPath);

        return erodedPath;
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

        // Transform all layers
        foreach (var (_, layerList) in shape.Layers)
            for (var i = 0; i < layerList.Count; i++)
            {
                var (layerPath, layerPaint) = layerList[i];
                layerPath.Transform(scaleMatrix);
                layerList[i] = (layerPath, layerPaint);
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
    /// Creates a morphed shape by blending this shape with another shape based on the specified amount.
    /// This method performs shape interpolation by creating a weighted union of the two shapes.
    /// At amount=0, returns this shape; at amount=1, returns the other shape;
    /// values in between create a morphed blend that gradually transitions from one shape to the other.
    /// </summary>
    /// <param name="other">The other shape to blend with this shape.</param>
    /// <param name="amount">The blending amount. 0.0 returns this shape, 1.0 returns the other shape, values in between create a blend.</param>
    /// <returns>A new <see cref="Shape"/> representing the blended result.</returns>
    public Shape Mix(Shape other, float amount)
    {
        amount = Math.Clamp(amount, 0.0f, 1.0f);

        if (amount == 0.0f) return Copy();
        if (Math.Abs(amount - 1.0f) < float.Epsilon) return other.Copy();

        // Create a smooth interpolation between the two shapes
        // We use a combination of scaling and union operations to create the morph effect
        var thisContribution = Expand(-amount * 5); // Contract this shape as amount increases
        var otherContribution = other.Expand(-(1 - amount) * 5); // Contract other shape as amount decreases

        // Create the morphed shape by blending the contributions
        var morphedPath = new SKPath();
        thisContribution.Path.Op(otherContribution.Path, SKPathOp.Union, morphedPath);

        var result = new Shape(morphedPath);

        // Blend paint properties if both shapes have paint
        if (Paint != null && other.Paint != null)
        {
            var blendedPaint = new SKPaint
            {
                Color = BlendColors(Paint.Color, other.Paint.Color, amount),
                Style = amount < 0.5f ? Paint.Style : other.Paint.Style,
                IsAntialias = Paint.IsAntialias || other.Paint.IsAntialias,
                StrokeWidth = Paint.StrokeWidth * (1 - amount) + other.Paint.StrokeWidth * amount
            };
            result.Paint = blendedPaint;
        }
        else
        {
            result.Paint = amount < 0.5f ? Paint : other.Paint;
        }

        return result;
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
            if (zIndex == 0) continue; // Skip main layer as it's already handled

            foreach (var (layerPath, layerPaint) in layerList)
            {
                var transformedLayerPath = new SKPath(layerPath);
                transformedLayerPath.Transform(SKMatrix.CreateTranslation(deltaX, 0));
                result.AddToLayer(zIndex, transformedLayerPath, layerPaint);
            }
        }

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
            if (zIndex == 0) continue; // Skip main layer as it's already handled

            foreach (var (layerPath, layerPaint) in layerList)
            {
                var transformedLayerPath = new SKPath(layerPath);
                transformedLayerPath.Transform(SKMatrix.CreateTranslation(0, deltaY));
                result.AddToLayer(zIndex, transformedLayerPath, layerPaint);
            }
        }

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
            if (zIndex == 0) continue; // Skip main layer as it's already handled

            foreach (var (layerPath, layerPaint) in layerList)
            {
                var transformedLayerPath = new SKPath(layerPath);
                transformedLayerPath.Transform(SKMatrix.CreateTranslation(deltaX, deltaY));
                result.AddToLayer(zIndex, transformedLayerPath, layerPaint);
            }
        }

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
