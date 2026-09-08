using System.Runtime.CompilerServices;

namespace Guinevere;

public partial class Gui
{
    /// <summary>
    /// Draws an image into <paramref name="destination"/>, optionally from a sub-region of the
    /// source and with a colour tint and opacity. Only takes effect during the render pass.
    /// </summary>
    /// <param name="image">The image to draw.</param>
    /// <param name="destination">Destination rectangle in screen space.</param>
    /// <param name="source">Source sub-rectangle in image pixels, or <c>null</c> for the whole image.</param>
    /// <param name="tint">Multiplied into the image's colours; <c>null</c> leaves them unchanged.</param>
    /// <param name="opacity">Overall opacity in [0, 1].</param>
    [PublicAPI]
    public void DrawImage(SKImage image, Rect destination, Rect? source = null, Color? tint = null, float opacity = 1f) =>
        DrawImage(image, destination, source, tint, opacity, null);

    /// <summary>
    /// Draws <paramref name="image"/> filling the current layout node's rectangle.
    /// </summary>
    /// <param name="image">The image to draw.</param>
    /// <param name="tint">Multiplied into the image's colours; <c>null</c> leaves them unchanged.</param>
    /// <param name="opacity">Overall opacity in [0, 1].</param>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawImage(SKImage image, Color? tint = null, float opacity = 1f) =>
        DrawImage(image, CurrentNode.Rect, null, tint, opacity, null);

    /// <summary>Draws a <see cref="Bitmap"/> filling the current layout node's rectangle.</summary>
    /// <param name="bitmap">The bitmap to draw.</param>
    /// <param name="tint">Multiplied into the bitmap's colours; <c>null</c> leaves them unchanged.</param>
    /// <param name="opacity">Overall opacity in [0, 1].</param>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawImage(Bitmap bitmap, Color? tint = null, float opacity = 1f) =>
        DrawImage(bitmap.Image, CurrentNode.Rect, null, tint, opacity, null);

    /// <summary>
    /// Draws an image as a nine-patch into <paramref name="destination"/>: corners 1:1, edges and
    /// centre stretched. Only takes effect during the render pass.
    /// </summary>
    /// <param name="image">The image to draw.</param>
    /// <param name="destination">Destination rectangle in screen space.</param>
    /// <param name="border">Corner sizes, in source pixels.</param>
    /// <param name="tint">Multiplied into the image's colours; <c>null</c> leaves them unchanged.</param>
    /// <param name="opacity">Overall opacity in [0, 1].</param>
    [PublicAPI]
    public void DrawImageNineSlice(SKImage image, Rect destination, Insets border, Color? tint = null, float opacity = 1f) =>
        DrawImageNineSlice(image, destination, border, tint, opacity, null);

    internal void DrawImage(SKImage image, Rect destination, Rect? source, Color? tint, float opacity, LayoutNode? node)
    {
        if (Pass != Pass.Pass2Render) return;
        AddDraw(new ImageDrawable(image, destination, source, BuildImagePaint(tint, opacity)), node: node);
    }

    internal void DrawImageNineSlice(
        SKImage image, Rect destination, Insets border, Color? tint, float opacity, LayoutNode? node)
    {
        if (Pass != Pass.Pass2Render) return;
        AddDraw(new NineSliceDrawable(image, destination, border, BuildImagePaint(tint, opacity)), node: node);
    }

    private static SKPaint? BuildImagePaint(Color? tint, float opacity)
    {
        var alpha = (byte)(Math.Clamp(opacity, 0f, 1f) * 255f + 0.5f);
        if (tint is null && alpha == 255) return null;

        var paint = new SKPaint { IsAntialias = true, Color = new SKColor(255, 255, 255, alpha) };
        if (tint is { } tc)
            paint.ColorFilter = SKColorFilter.CreateBlendMode(tc, SKBlendMode.Modulate);
        return paint;
    }
}
