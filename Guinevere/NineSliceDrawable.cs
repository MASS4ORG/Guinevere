namespace Guinevere;

/// <summary>
/// Draws an image as a nine-patch: the four corners are blitted 1:1, the four edges stretch along
/// one axis and the center stretches both, so a small source scales to any destination without
/// distorting its border. When the destination is smaller than the combined corners the corner
/// sizes are scaled down proportionally so nothing overlaps.
/// </summary>
/// <param name="image">The source image.</param>
/// <param name="destination">Destination rectangle in screen space.</param>
/// <param name="border">Corner sizes, in source pixels.</param>
/// <param name="paint">Paint applied to every patch, or <c>null</c> for a plain copy.</param>
public sealed class NineSliceDrawable(SKImage image, Rect destination, Insets border, SKPaint? paint = null)
    : IDrawable, IInkBounds
{
    static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.None);

    /// <inheritdoc/>
    SKRect? IInkBounds.InkBounds(LayoutNode node) => Ink.Painted(
        new SKRect(Destination.X, Destination.Y, Destination.X + Destination.W, Destination.Y + Destination.H), Paint);

    /// <summary>The source image.</summary>
    public SKImage Image { get; } = image;

    /// <summary>Destination rectangle in screen space.</summary>
    public Rect Destination { get; } = destination;

    /// <summary>Corner sizes, in source pixels.</summary>
    public Insets Border { get; } = border;

    /// <inheritdoc />
    public SKPaint? Paint { get; } = paint;

    /// <inheritdoc />
    public void Render(Gui gui, LayoutNode node, SKCanvas canvas) => Draw(canvas, Image, Destination, Border, Paint);

    /// <summary>Draws the shared nine-slice grid directly onto a canvas, preserving the source corner sizes.</summary>
    public static void Draw(SKCanvas canvas, SKImage image, Rect destination, Insets border, SKPaint? paint = null)
    {
        var grid = new NineSliceLayout(image.Width, image.Height, destination, border);
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var (source, target) = grid.Patch(row, column);
                if (source.W <= 0 || source.H <= 0 || target.W <= 0 || target.H <= 0)
                {
                    continue;
                }

                canvas.DrawImage(image,
                    new SKRect(source.X, source.Y, source.X + source.W, source.Y + source.H),
                    new SKRect(target.X, target.Y, target.X + target.W, target.Y + target.H), Sampling, paint);
            }
        }
    }
}
