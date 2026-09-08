namespace Guinevere;

/// <summary>
/// Draws an image as a nine-patch: the four corners are blitted 1:1, the four edges stretch along
/// one axis and the centre stretches both, so a small source scales to any destination without
/// distorting its border. When the destination is smaller than the combined corners the corner
/// sizes are scaled down proportionally so nothing overlaps.
/// </summary>
/// <param name="image">The source image.</param>
/// <param name="destination">Destination rectangle in screen space.</param>
/// <param name="border">Corner sizes, in source pixels.</param>
/// <param name="paint">Paint applied to every patch, or <c>null</c> for a plain copy.</param>
public sealed class NineSliceDrawable(SKImage image, Rect destination, Insets border, SKPaint? paint = null)
    : IDrawable
{
    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.None);

    /// <summary>The source image.</summary>
    public SKImage Image { get; } = image;

    /// <summary>Destination rectangle in screen space.</summary>
    public Rect Destination { get; } = destination;

    /// <summary>Corner sizes, in source pixels.</summary>
    public Insets Border { get; } = border;

    /// <inheritdoc />
    public SKPaint? Paint { get; } = paint;

    /// <inheritdoc />
    public void Render(Gui gui, LayoutNode node, SKCanvas canvas)
    {
        float sw = Image.Width;
        float sh = Image.Height;

        // Source column/row boundaries.
        var sl = Math.Clamp(Border.Left, 0f, sw);
        var sr = Math.Clamp(Border.Right, 0f, sw - sl);
        var st = Math.Clamp(Border.Top, 0f, sh);
        var sb = Math.Clamp(Border.Bottom, 0f, sh - st);

        // Destination corner sizes, shrunk together if the destination is too small for them.
        var dl = sl;
        var dr = sr;
        var scaleX = dl + dr > Destination.W && dl + dr > 0f ? Destination.W / (dl + dr) : 1f;
        dl *= scaleX;
        dr *= scaleX;

        var dt = st;
        var db = sb;
        var scaleY = dt + db > Destination.H && dt + db > 0f ? Destination.H / (dt + db) : 1f;
        dt *= scaleY;
        db *= scaleY;

        float[] sx = [0f, sl, sw - sr, sw];
        float[] sy = [0f, st, sh - sb, sh];
        float[] dx = [Destination.X, Destination.X + dl, Destination.X + Destination.W - dr, Destination.X + Destination.W];
        float[] dy = [Destination.Y, Destination.Y + dt, Destination.Y + Destination.H - db, Destination.Y + Destination.H];

        for (var row = 0; row < 3; row++)
        for (var col = 0; col < 3; col++)
        {
            var src = new SKRect(sx[col], sy[row], sx[col + 1], sy[row + 1]);
            var dstRect = new SKRect(dx[col], dy[row], dx[col + 1], dy[row + 1]);
            if (src.Width <= 0f || src.Height <= 0f || dstRect.Width <= 0f || dstRect.Height <= 0f)
                continue;

            canvas.DrawImage(Image, src, dstRect, Sampling, Paint);
        }
    }
}
