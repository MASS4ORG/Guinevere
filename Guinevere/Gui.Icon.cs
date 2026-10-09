namespace Guinevere;

public partial class Gui
{
    /// <summary>
    /// Lays out an icon as a square node and draws it centred in the render pass. Glyphs fill the square as their
    /// font size; images and pictures scale to fit, keeping their aspect ratio.
    /// </summary>
    /// <param name="icon">The icon to draw, or <c>null</c> to only reserve the square, as for a missing icon.</param>
    /// <param name="size">Side of the square, or <c>0</c> for the scope text size.</param>
    /// <param name="tint">Color for glyphs and tintable images; <c>null</c> uses the icon's own color.</param>
    /// <param name="opacity">Overall opacity in [0, 1].</param>
    /// <returns>The layout node the icon occupies.</returns>
    [PublicAPI]
    public LayoutNode Icon(Icon? icon, float size = 0, Color? tint = null, float opacity = 1f)
    {
        var side = size > 0 ? size : CurrentNodeScope.Get<LayoutNodeScopeTextSize>().Value;
        var node = Node(side, side);
        if (icon is not null) DrawIcon(icon, node.Rect, tint, opacity, node);
        return node;
    }

    /// <summary>Draws an icon centred in <paramref name="destination"/>. Only takes effect during the render pass.</summary>
    /// <param name="icon">The icon to draw.</param>
    /// <param name="destination">Destination rectangle in screen space.</param>
    /// <param name="tint">Color for glyphs and tintable images; <c>null</c> uses the icon's own color.</param>
    /// <param name="opacity">Overall opacity in [0, 1].</param>
    [PublicAPI]
    public void DrawIcon(Icon icon, Rect destination, Color? tint = null, float opacity = 1f) =>
        DrawIcon(icon, destination, tint, opacity, null);

    internal void DrawIcon(Icon icon, Rect destination, Color? tint, float opacity, LayoutNode? node)
    {
        ArgumentNullException.ThrowIfNull(icon);
        if (Pass != Pass.Pass2Render || destination.W <= 0f || destination.H <= 0f) return;
        var color = tint ?? icon.Color;
        switch (icon.Kind)
        {
            case IconKind.Glyph:
                DrawGlyphIcon(icon, destination, color, opacity, node);
                break;
            case IconKind.Image:
                var image = icon.Image!;
                var fitted = Fit(destination, image.Width, image.Height);
                AddDraw(new ImageDrawable(image, fitted, null, BuildIconPaint(icon, color, opacity)), node: node);
                break;
            default:
                AddDraw(new PictureDrawable(icon.Picture!, destination, BuildIconPaint(icon, color, opacity)),
                    node: node);
                break;
        }
    }

    void DrawGlyphIcon(Icon icon, Rect destination, Color? color, float opacity, LayoutNode? node)
    {
        var glyph = icon.Glyph!;
        var font = new SKFont(GlyphTypeface(icon.Font, glyph), Math.Min(destination.W, destination.H));
        font.MeasureText(glyph, out var ink);
        var fill = color ?? CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value;
        var paint = new SKPaint { IsAntialias = true, Color = new Color(fill, fill.A / 255f * Opacity(opacity)) };
        var position = new Vector2(destination.X + destination.W * 0.5f - ink.MidX,
            destination.Y + destination.H * 0.5f - ink.MidY);
        AddDraw(new Text(glyph, position, font, paint), node: node);
    }

    /// <summary>The explicit font, else the first scope font (text, widget icons, icons) that has the glyph.</summary>
    SKTypeface GlyphTypeface(Font? font, string glyph)
    {
        if (font is not null)
        {
            var runs = FontTextLayout.Create(glyph, font, font, font);
            return runs.Count > 0 ? runs[0].Font.SkFont.Typeface : font.SkFont.Typeface;
        }
        var codePoint = char.ConvertToUtf32(glyph, 0);
        var text = CurrentNodeScope.Get<LayoutNodeScopeTextFont>().Value.SkFont;
        if (text.GetGlyph(codePoint) != 0) return text.Typeface;
        var widget = CurrentNodeScope.Get<LayoutNodeScopeWidgetIconFont>().Value.SkFont;
        return widget.GetGlyph(codePoint) != 0
            ? widget.Typeface
            : CurrentNodeScope.Get<LayoutNodeScopeIconFont>().Value.SkFont.Typeface;
    }

    /// <summary>A tint recolors the silhouette (<c>SrcIn</c>) only for tintable icons.</summary>
    static SKPaint? BuildIconPaint(Icon icon, Color? tint, float opacity)
    {
        var alpha = (byte)(Opacity(opacity) * 255f + 0.5f);
        var recolor = icon.Tintable && tint is not null;
        if (!recolor && alpha == 255) return null;

        var paint = new SKPaint { IsAntialias = true, Color = new SKColor(255, 255, 255, alpha) };
        if (recolor) paint.ColorFilter = SKColorFilter.CreateBlendMode(tint!.Value, SKBlendMode.SrcIn);
        return paint;
    }

    static float Opacity(float opacity) => Math.Clamp(opacity, 0f, 1f);

    /// <summary>The largest rectangle with the source aspect ratio centred in <paramref name="bounds"/>.</summary>
    internal static Rect Fit(Rect bounds, float width, float height)
    {
        if (width <= 0f || height <= 0f) return bounds;
        var scale = Math.Min(bounds.W / width, bounds.H / height);
        var w = width * scale;
        var h = height * scale;
        return new Rect(bounds.X + (bounds.W - w) * 0.5f, bounds.Y + (bounds.H - h) * 0.5f, w, h);
    }

    /// <summary>Draws a recorded picture scaled to fit a rectangle, keeping its aspect ratio.</summary>
    sealed class PictureDrawable(SKPicture picture, Rect destination, SKPaint? paint) : IDrawable, IInkBounds
    {
        public SKPaint? Paint { get; } = paint;

        public SKRect? InkBounds(LayoutNode node) => Ink.Painted(
            new SKRect(destination.X, destination.Y, destination.X + destination.W, destination.Y + destination.H),
            Paint);

        public void Render(Gui gui, LayoutNode node, SKCanvas canvas)
        {
            var cull = picture.CullRect;
            var fitted = Fit(destination, cull.Width, cull.Height);
            var scale = cull.Width > 0f ? fitted.W / cull.Width : 1f;
            var matrix = SKMatrix.CreateScaleTranslation(scale, scale, fitted.X - cull.Left * scale,
                fitted.Y - cull.Top * scale);
            canvas.DrawPicture(picture, in matrix, Paint);
        }
    }
}
