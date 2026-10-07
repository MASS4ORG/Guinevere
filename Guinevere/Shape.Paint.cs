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
    /// Adds an inset shadow like CSS <c>box-shadow: inset</c>: the shape's edge casts it inward, away from the
    /// <paramref name="offset"/> (an offset of (0, 10) darkens the top), clipped to the shape.
    /// </summary>
    /// <param name="color">The color of the shadow.</param>
    /// <param name="offset">How far the shadow-casting hole is shifted inside the shape.</param>
    /// <param name="blurRadius">The CSS blur radius (twice the Gaussian sigma).</param>
    /// <param name="spread">Grows the shadow inward when positive; negative values shrink it.</param>
    /// <returns>The <see cref="Shape"/> instance with the applied inner shadow effect.</returns>
    public Shape InnerShadow(Color color, Vector2 offset, float blurRadius = 0, float spread = 0)
    {
        AddShadow(new ShapeShadow(true, offset, Math.Max(0f, blurRadius), spread, color));
        return this;
    }

    /// <summary>Adds an unshifted inset shadow; see <see cref="InnerShadow(Color, Vector2, float, float)"/>.</summary>
    /// <param name="color">The color of the shadow.</param>
    /// <param name="blurRadius">The CSS blur radius (twice the Gaussian sigma).</param>
    /// <param name="spread">Grows the shadow inward when positive; negative values shrink it.</param>
    /// <returns>The <see cref="Shape"/> instance with the applied inner shadow effect.</returns>
    public Shape InnerShadow(Color color, float blurRadius, float spread = 0) =>
        InnerShadow(color, Vector2.Zero, blurRadius, spread);

    /// <summary>
    /// Adds a drop shadow like CSS <c>box-shadow</c>: the shape, shifted by <paramref name="offset"/> and grown by
    /// <paramref name="spread"/>, blurred and kept outside the shape. The first shadow added is drawn on top.
    /// </summary>
    /// <param name="color">The color of the shadow.</param>
    /// <param name="offset">The offset of the shadow relative to the shape.</param>
    /// <param name="blurRadius">The CSS blur radius (twice the Gaussian sigma).</param>
    /// <param name="spread">Grows the shadow when positive; negative values shrink it.</param>
    /// <returns>The <see cref="Shape"/> instance with the applied outer shadow effect.</returns>
    public Shape OuterShadow(Color color, Vector2 offset, float blurRadius = 0, float spread = 0)
    {
        AddShadow(new ShapeShadow(false, offset, Math.Max(0f, blurRadius), spread, color));
        return this;
    }

    /// <summary>Adds an unshifted drop shadow; see <see cref="OuterShadow(Color, Vector2, float, float)"/>.</summary>
    /// <param name="color">The color of the shadow.</param>
    /// <param name="blurRadius">The CSS blur radius (twice the Gaussian sigma).</param>
    /// <param name="spread">Grows the shadow when positive; negative values shrink it. Defaults to 0.</param>
    /// <returns>The <see cref="Shape"/> instance with the applied outer shadow effect.</returns>
    public Shape OuterShadow(Color color, float blurRadius, float spread = 0) =>
        OuterShadow(color, Vector2.Zero, blurRadius, spread);

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
}
