namespace Guinevere;

/// <summary>
/// Gradient and checkerboard painting shared by the color picker parts and by custom <see cref="IColorPickerShape"/>
/// implementations. Call only in the render pass.
/// </summary>
public static class ColorPaint
{
    static readonly SKColor[] HueColors =
    [
        new(255, 0, 0), new(255, 255, 0), new(0, 255, 0), new(0, 255, 255), new(0, 0, 255), new(255, 0, 255),
        new(255, 0, 0)
    ];

    /// <summary>The fully saturated, fully bright colors around the hue circle, red to red, in six equal steps.</summary>
    public static ReadOnlySpan<SKColor> HueStops => HueColors;

    /// <summary>Fills <paramref name="rect"/> with a gradient between two colors along one axis.</summary>
    /// <param name="gui">The GUI drawing into the current node.</param>
    /// <param name="rect">The area to fill.</param>
    /// <param name="start">Color at the left, or at the bottom when <paramref name="vertical"/>.</param>
    /// <param name="end">Color at the right, or at the top when <paramref name="vertical"/>.</param>
    /// <param name="vertical">Whether the gradient runs bottom to top.</param>
    /// <param name="radius">Corner radius.</param>
    public static void FillGradient(Gui gui, Rect rect, Color start, Color end, bool vertical = false,
        float radius = 0f) =>
        FillGradient(gui, rect, [start, end], vertical, radius);

    /// <summary>Fills <paramref name="rect"/> with evenly spaced gradient stops along one axis.</summary>
    /// <param name="gui">The GUI drawing into the current node.</param>
    /// <param name="rect">The area to fill.</param>
    /// <param name="stops">Colors from the left, or from the bottom when <paramref name="vertical"/>.</param>
    /// <param name="vertical">Whether the gradient runs bottom to top.</param>
    /// <param name="radius">Corner radius.</param>
    public static void FillGradient(Gui gui, Rect rect, ReadOnlySpan<SKColor> stops, bool vertical = false,
        float radius = 0f)
    {
        if (gui.Pass != Pass.Pass2Render || rect.W <= 0 || rect.H <= 0) return;
        var from = vertical ? new SKPoint(rect.X, rect.Y + rect.H) : new SKPoint(rect.X, rect.Y);
        var to = vertical ? new SKPoint(rect.X, rect.Y) : new SKPoint(rect.X + rect.W, rect.Y);
        Shade(gui.DrawRect(rect, radius).SolidColor(Color.White),
            SKShader.CreateLinearGradient(from, to, stops.ToArray(), null, SKShaderTileMode.Clamp));
    }

    /// <summary>Fills a circle with the hue circle swept around its center, red at three o'clock, clockwise.</summary>
    /// <param name="gui">The GUI drawing into the current node.</param>
    /// <param name="center">Circle center.</param>
    /// <param name="radius">Circle radius.</param>
    public static void FillHueWheel(Gui gui, Vector2 center, float radius)
    {
        if (gui.Pass != Pass.Pass2Render || radius <= 0) return;
        Shade(gui.DrawCircle(center, radius).SolidColor(Color.White),
            SKShader.CreateSweepGradient(new SKPoint(center.X, center.Y), HueColors, null));
    }

    /// <summary>Fills a circle with a radial gradient from its center to its edge.</summary>
    /// <param name="gui">The GUI drawing into the current node.</param>
    /// <param name="center">Circle center.</param>
    /// <param name="radius">Circle radius.</param>
    /// <param name="inner">Color at the center.</param>
    /// <param name="outer">Color at the edge.</param>
    public static void FillRadial(Gui gui, Vector2 center, float radius, Color inner, Color outer)
    {
        if (gui.Pass != Pass.Pass2Render || radius <= 0) return;
        Shade(gui.DrawCircle(center, radius).SolidColor(Color.White),
            SKShader.CreateRadialGradient(new SKPoint(center.X, center.Y), radius, [inner, outer], null,
                SKShaderTileMode.Clamp));
    }

    /// <summary>
    /// Fills <paramref name="rect"/> with the checkerboard drawn under translucent colors, in the current node's
    /// <c>checker</c> part colors (<c>background-color</c> and <c>color</c>).
    /// </summary>
    /// <param name="gui">The GUI drawing into the current node.</param>
    /// <param name="rect">The area to fill.</param>
    /// <param name="cell">Side of one square.</param>
    public static void FillChecker(Gui gui, Rect rect, float cell = 6f)
    {
        if (gui.Pass != Pass.Pass2Render || rect.W <= 0 || rect.H <= 0) return;
        var style = gui.ResolvePart("checker");
        gui.DrawRect(rect, style.GetColor("background-color") ?? Color.White);
        cell = Math.Max(2f, cell);
        var builder = new SKPathBuilder();
        for (var y = 0; y * cell < rect.H; y++)
            for (var x = y % 2; x * cell < rect.W; x += 2)
                builder.AddRect(new SKRect(rect.X + x * cell, rect.Y + y * cell,
                    rect.X + MathF.Min(rect.W, (x + 1) * cell), rect.Y + MathF.Min(rect.H, (y + 1) * cell)));
        var squares = new PathShape(builder.Detach());
        squares.SolidColor(style.GetColor("color") ?? Color.LightGray);
        gui.DrawShape(Vector2.Zero, squares);
    }

    static void Shade(Shape shape, SKShader shader)
    {
        shape.Paint!.Color = SKColors.White;
        shape.Paint.Shader = shader;
    }

    sealed class PathShape(SKPath path) : Shape(path);
}
