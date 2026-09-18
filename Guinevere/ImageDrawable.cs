namespace Guinevere;

/// <summary>
/// A drawable that blits an <see cref="SKImage"/> into a destination rectangle, optionally from a
/// sub-region of the source and through a <see cref="SKPaint"/> (tint, opacity, blend mode).
/// </summary>
/// <param name="image">The source image.</param>
/// <param name="destination">Destination rectangle in screen space.</param>
/// <param name="source">Source sub-rectangle in image pixels, or <c>null</c> for the whole image.</param>
/// <param name="paint">Paint applied to the blit, or <c>null</c> for a plain copy.</param>
public sealed class ImageDrawable(SKImage image, Rect destination, Rect? source = null, SKPaint? paint = null)
    : IDrawable
{
    static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.None);

    /// <summary>The source image.</summary>
    public SKImage Image { get; } = image;

    /// <summary>Destination rectangle in screen space.</summary>
    public Rect Destination { get; } = destination;

    /// <summary>Source sub-rectangle in image pixels, or <c>null</c> for the whole image.</summary>
    public Rect? Source { get; } = source;

    /// <inheritdoc />
    public SKPaint? Paint { get; } = paint;

    /// <inheritdoc />
    public void Render(Gui gui, LayoutNode node, SKCanvas canvas)
    {
        var dst = new SKRect(Destination.X, Destination.Y, Destination.X + Destination.W, Destination.Y + Destination.H);

        if (Source is { } src)
        {
            var srcRect = new SKRect(src.X, src.Y, src.X + src.W, src.Y + src.H);
            canvas.DrawImage(Image, srcRect, dst, Sampling, Paint);
        }
        else
        {
            canvas.DrawImage(Image, dst, Sampling, Paint);
        }
    }
}
