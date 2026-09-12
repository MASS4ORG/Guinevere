namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// Lays out and draws an image as a node. With no explicit size the node is the image's pixel
    /// size; pass <paramref name="width"/> and/or <paramref name="height"/> to fit it into the layout,
    /// or use <c>Expand</c>/<c>Width</c> on the returned node.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="image">The image to draw.</param>
    /// <param name="width">Node width, or <c>-1</c> to use the image width.</param>
    /// <param name="height">Node height, or <c>-1</c> to use the image height.</param>
    /// <param name="tint">Multiplied into the image's colours; <c>null</c> leaves them unchanged.</param>
    /// <param name="opacity">Overall opacity in [0, 1].</param>
    /// <returns>The layout node the image occupies, for further layout chaining.</returns>
    public static LayoutNode Image(
        this Gui gui,
        SKImage image,
        float width = -1,
        float height = -1,
        Color? tint = null,
        float opacity = 1f)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(image);

        var w = width >= 0 ? width : image.Width;
        var h = height >= 0 ? height : image.Height;

        var node = gui.Node(w, h);

        if (gui.Pass == Pass.Pass2Render)
            gui.DrawImage(image, node.Rect, null, tint, opacity, node);

        return node;
    }

    /// <inheritdoc cref="Image(Gui, SKImage, float, float, Color?, float)"/>
    /// <param name="gui">The GUI context.</param>
    /// <param name="bitmap">The bitmap to draw.</param>
    /// <param name="width">Node width, or <c>-1</c> to use the bitmap width.</param>
    /// <param name="height">Node height, or <c>-1</c> to use the bitmap height.</param>
    /// <param name="tint">Multiplied into the bitmap's colours; <c>null</c> leaves them unchanged.</param>
    /// <param name="opacity">Overall opacity in [0, 1].</param>
    public static LayoutNode Image(
        this Gui gui,
        Bitmap bitmap,
        float width = -1,
        float height = -1,
        Color? tint = null,
        float opacity = 1f)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return gui.Image(bitmap.Image, width, height, tint, opacity);
    }
}
