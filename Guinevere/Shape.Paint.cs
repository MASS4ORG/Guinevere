namespace Guinevere;

/// <summary>
/// Represents a base abstract class for creating and manipulating 2D geometric shapes
/// with customizable rendering behaviors, transformations, and visual effects.
/// </summary>
public partial class Shape
{
    /// <summary>
    /// Gets the <see cref="SKPaint"/> used to render the shape.
    /// This property defines the specific paint settings, such as color, style, and effects,
    /// that are applied during the rendering of the shape.
    /// </summary>
    public SKPaint? Paint { get; private set; }

    /// <summary>
    /// Applies an inner shadow effect to the shape using the specified color, offset, blur radius, and spread value.
    /// </summary>
    /// <param name="color">The color of the shadow.</param>
    /// <param name="offset">The offset of the shadow relative to the shape.</param>
    /// <param name="blurRadius">The radius of the blur applied to the shadow.</param>
    /// <param name="spread">The spread of the shadow, which adjusts its size.</param>
    /// <returns>The <see cref="Shape"/> instance with the applied inner shadow effect.</returns>
    public Shape InnerShadow(Color color, Vector2 offset, float blurRadius = 0, int spread = 0)
    {
        // For inner shadow, the shadow is the same shape as the original but offset
        var shadowPath = new SKPath(Path);

        // Apply spread by expanding the shadow if needed
        if (spread != 0)
        {
            var strokePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke, StrokeWidth = Math.Abs(spread) * 2, StrokeJoin = SKStrokeJoin.Round
            };
            shadowPath = strokePaint.GetFillPath(shadowPath);
        }

        // Create shadow paint with blur
        var shadowPaint = new SKPaint
        {
            Color = color,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            ImageFilter = blurRadius > 0 ? SKImageFilter.CreateBlur(blurRadius, blurRadius) : null
        };

        // Apply offset to the shadow path
        if (offset.X != 0 || offset.Y != 0) shadowPath.Transform(SKMatrix.CreateTranslation(offset.X, offset.Y));

        // For inner shadow, we need to clip the shadow to only show inside the original shape
        // This is achieved by intersecting the offset shadow with the original shape
        var clippedShadowPath = new SKPath();
        shadowPath.Op(Path, SKPathOp.Intersect, clippedShadowPath);

        // Add to positive layer (inner shadows go above main shape)
        var innerShadowLayer = GetNextInnerShadowLayer();
        AddToLayer(innerShadowLayer, clippedShadowPath, shadowPaint);

        return this;
    }

    /// <summary>
    /// Applies an inner shadow effect to the shape using the specified color, offset, blur radius, and spread value.
    /// </summary>
    /// <param name="color">The color of the shadow.</param>
    /// <param name="blurRadius">The radius of the blur applied to the shadow.</param>
    /// <param name="spread">The spread of the shadow, which adjusts its size.</param>
    /// <returns>The <see cref="Shape"/> instance with the applied inner shadow effect.</returns>
    public Shape InnerShadow(Color color, float blurRadius, int spread = 0)
    {
        return InnerShadow(color, Vector2.Zero, blurRadius, spread);
    }

    /// <summary>
    /// Applies an outer shadow effect to the shape using the specified color, offset, blur radius, and spread value.
    /// </summary>
    /// <param name="color">The color of the shadow.</param>
    /// <param name="offset">The offset of the shadow relative to the shape.</param>
    /// <param name="blurRadius">The radius of the blur applied to the shadow.</param>
    /// <param name="spread">The spread of the shadow, which adjusts its size.</param>
    /// <returns>The <see cref="Shape"/> instance with the applied outer shadow effect.</returns>
    public Shape OuterShadow(Color color, Vector2 offset, float blurRadius = 0, float spread = 0)
    {
        // Create a shadow path with offset
        var shadowPath = new SKPath(Path);
        if (offset.X != 0 || offset.Y != 0) shadowPath.Transform(SKMatrix.CreateTranslation(offset.X, offset.Y));

        // Expand shadow if spread is specified
        if (spread != 0)
        {
            var strokePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke, StrokeWidth = Math.Abs(spread) * 2, StrokeJoin = SKStrokeJoin.Round
            };
            shadowPath = strokePaint.GetFillPath(shadowPath);
        }

        // For outer shadow, subtract the original shape from the shadow to prevent overlap
        var finalShadowPath = new SKPath();
        shadowPath.Op(Path, SKPathOp.Difference, finalShadowPath);

        // Create shadow paint
        var shadowPaint = new SKPaint
        {
            Color = color,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            ImageFilter = blurRadius > 0 ? SKImageFilter.CreateBlur(blurRadius, blurRadius) : null
        };

        // Add to negative layer (outer shadows go below main shape)
        var outerShadowLayer = GetNextOuterShadowLayer();
        AddToLayer(outerShadowLayer, finalShadowPath, shadowPaint);

        return this;
    }

    /// <summary>
    /// Applies an outer shadow effect to the shape using the specified color, blur radius, offset, and spread value.
    /// </summary>
    /// <param name="color">The color of the shadow.</param>
    /// <param name="blurRadius">The radius of the blur applied to the shadow.</param>
    /// <param name="spread">The spread of the shadow, which adjusts its size. Defaults to 0.</param>
    /// <returns>The <see cref="Shape"/> instance with the applied outer shadow effect.</returns>
    public Shape OuterShadow(Color color, float blurRadius, float spread = 0)
    {
        return OuterShadow(color, Vector2.Zero, blurRadius, spread);
    }

    /// <summary>
    /// Applies a radial gradient color effect to the shape using the specified colors, radii, and offsets.
    /// </summary>
    /// <param name="colorA">The color at the inner radius of the gradient.</param>
    /// <param name="colorB">The color at the outer radius of the gradient.</param>
    /// <param name="innerRadius">The radius where the gradient starts with the inner color.</param>
    /// <param name="outerRadius">The outermost radius of the gradient where the outer color is applied.</param>
    /// <param name="offsetX">Horizontal offset for the gradient center. Defaults to 0.</param>
    /// <param name="offsetY">Vertical offset for the gradient center. Defaults to 0.</param>
    /// <returns>The <see cref="Shape"/> instance with the applied radial gradient effect.</returns>
    public Shape RadialGradientColor(Color colorA, Color colorB,
        float innerRadius, float outerRadius,
        float offsetX = 0, float offsetY = 0)
    {
        var centerX = Node is not null ? Node.Rect.Center.X : Path.Bounds.MidX;
        var centerY = Node is not null ? Node.Rect.Center.Y : Path.Bounds.MidY;

        var center = new SKPoint(
            centerX + offsetX,
            centerY + offsetY);

        var radialShader = SKShader.CreateRadialGradient(
            center,
            outerRadius,
            [colorA, colorB],
            null,
            SKShaderTileMode.Clamp
        );

        // Preserve existing paint properties
        Paint ??= new SKPaint();

        // Create a new paint if one doesn't exist, or copy existing properties
        if (Paint.Shader != null)
        {
            // For multiple gradients, we need to create a layered effect
            // Create a copy of the current paint for layering
            var existingShader = Paint.Shader;
            Paint.Shader = SKShader.CreateCompose(existingShader, radialShader);
        }
        else
        {
            Paint.Shader = radialShader;
        }

        Paint.Style = SKPaintStyle.Fill;
        Paint.IsAntialias = true;

        return this;
    }

    /// <summary>
    /// Applies a linear gradient color effect to the shape using the specified colors, angle, and scale.
    /// </summary>
    /// <param name="color1">The starting color of the gradient.</param>
    /// <param name="color2">The ending color of the gradient.</param>
    /// <param name="angleDeg">The angle of the gradient in degrees, with 0 being horizontal. Defaults to 0.</param>
    /// <param name="scale">A scaling factor for the gradient's size. Defaults to 1.</param>
    /// <returns>The <see cref="Shape"/> instance with the applied gradient effect.</returns>
    public Shape LinearGradientColor(Color color1, Color color2, float angleDeg = 0, float scale = 1)
    {
        var radians = angleDeg * MathF.PI / 180f;
        var bounds = Path.Bounds;
        var centerX = bounds.MidX;
        var centerY = bounds.MidY;
        var maxDimension = Math.Max(bounds.Width, bounds.Height) * scale;

        var dx = MathF.Cos(radians) * maxDimension * 0.5f;
        var dy = MathF.Sin(radians) * maxDimension * 0.5f;

        var linearShader = SKShader.CreateLinearGradient(
            new SKPoint(centerX - dx, centerY - dy),
            new SKPoint(centerX + dx, centerY + dy),
            [color1, color2],
            null,
            SKShaderTileMode.Clamp
        );

        // Preserve existing paint properties
        Paint ??= new SKPaint();

        // For multiple gradients, create a blended effect
        if (Paint.Shader != null)
        {
            // Blend with the existing shader using multiply blend mode for better composition
            var existingShader = Paint.Shader;
            Paint.Shader = SKShader.CreateCompose(existingShader, linearShader);
        }
        else
        {
            Paint.Shader = linearShader;
        }

        Paint.Style = SKPaintStyle.Fill;
        Paint.IsAntialias = true;

        return this;
    }

    /// <summary>
    /// Sets the shape's color to a solid color using the specified color.
    /// </summary>
    /// <param name="color">The color to apply to the shape as a solid fill.</param>
    /// <returns>Returns the updated shape with the applied solid color.</returns>
    public Shape SolidColor(Color? color)
    {
        var colorFinal = color ?? Color.White;
        Paint ??= new SKPaint();
        Paint.Color = colorFinal;
        Paint.IsAntialias = true;
        return this;
    }

    /// <summary>
    /// Configures the shape to use a border with the specified color and thickness.
    /// </summary>
    /// <param name="color">The color of the border.</param>
    /// <param name="thickness">The thickness of the border.</param>
    /// <returns>Returns the updated shape with the applied border settings.</returns>
    public Shape Stroke(Color color, float thickness)
    {
        Paint ??= new SKPaint();
        Paint.Color = color;
        Paint.Style = SKPaintStyle.Stroke;
        Paint.StrokeWidth = thickness;
        return this;
    }

    /// <summary>
    /// Gets the next available layer index for outer shadows (negative values).
    /// Each new outer shadow goes below the previous ones.
    /// </summary>
    /// <returns>A negative layer index for the new outer shadow.</returns>
    private int GetNextOuterShadowLayer()
    {
        var lowestLayer = Layers.Keys.Where(k => k < 0).DefaultIfEmpty(0).Min();
        return lowestLayer - 1;
    }

    /// <summary>
    /// Gets the next available layer index for inner shadows (positive values).
    /// Each new inner shadow goes above the previous ones.
    /// </summary>
    /// <returns>A positive layer index for the new inner shadow.</returns>
    private int GetNextInnerShadowLayer()
    {
        var highestLayer = Layers.Keys.Where(k => k > 0).DefaultIfEmpty(0).Max();
        return highestLayer + 1;
    }
}
