using SkiaSharp;

namespace Guinevere;

/// <summary>
/// The visual part of a styled node: outer shadows, the background (color or gradient), inset shadows, the border
/// and the outline, drawn in that order with per-corner radii.
/// </summary>
sealed class StyleBox : IDrawable
{
    readonly SKRoundRect _box;
    readonly SKPaint? _fill;
    readonly List<BoxShadow> _shadows;
    readonly (Color Color, float Width)? _border;
    readonly BoxOutline? _outline;

    StyleBox(SKRoundRect box, SKPaint? fill, List<BoxShadow> shadows, (Color, float)? border, BoxOutline? outline)
    {
        _box = box;
        _fill = fill;
        _shadows = shadows;
        _border = border;
        _outline = outline;
    }

    /// <inheritdoc/>
    public SKPaint? Paint => null;

    /// <summary>
    /// Builds the box for a resolved style over <paramref name="rect"/>, or <c>null</c> when the style draws nothing.
    /// </summary>
    public static StyleBox? From(ResolvedStyle style, Rect rect)
    {
        var bounds = new SKRect(rect.X, rect.Y, rect.X + rect.W, rect.Y + rect.H);
        var fill = Fill(style, bounds);
        var shadows = StyleBoxValues.Shadows(style.Get("box-shadow"));
        var borderColor = style.GetColor("border-color");
        var borderWidth = style.GetLength("border-width") ?? 0f;
        (Color, float)? border = borderColor is { } bc && borderWidth > 0f ? (bc, borderWidth) : null;
        var outline = StyleBoxValues.Outline(style);
        if (fill is null && shadows.Count == 0 && border is null && outline is null) return null;

        var box = new SKRoundRect();
        box.SetRectRadii(bounds, StyleBoxValues.Radii(style.Get("border-radius"), rect));
        return new StyleBox(box, fill, shadows, border, outline);
    }

    /// <summary>
    /// A <c>linear-gradient</c> from <c>background</c> or <c>background-image</c> over the solid
    /// <c>background-color</c>/<c>bg-color</c>/<c>background</c> color.
    /// </summary>
    static SKPaint? Fill(ResolvedStyle style, SKRect bounds)
    {
        var shader = StyleBoxValues.Gradient(style.Get("background-image"), bounds)
                     ?? StyleBoxValues.Gradient(style.Get("background"), bounds);
        if (shader is not null) return new SKPaint { IsAntialias = true, Shader = shader };

        var color = style.GetColor("background-color") ?? style.GetColor("bg-color") ?? style.GetColor("background");
        return color is { } c ? new SKPaint { IsAntialias = true, Color = c } : null;
    }

    /// <inheritdoc/>
    public void Render(Gui gui, LayoutNode node, SKCanvas canvas)
    {
        DrawShadows(canvas, inset: false);
        if (_fill is not null) canvas.DrawRoundRect(_box, _fill);
        DrawShadows(canvas, inset: true);
        if (_border is { } border) canvas.DrawRoundRect(_box, Stroke(border.Color, border.Width));
        if (_outline is { } outline) DrawOutline(canvas, outline);
    }

    /// <summary>Draws the outer or inset layers bottom-up, so the first listed shadow ends on top like CSS.</summary>
    void DrawShadows(SKCanvas canvas, bool inset)
    {
        for (var i = _shadows.Count - 1; i >= 0; i--)
        {
            if (_shadows[i].Inset != inset) continue;
            if (inset) DrawInsetShadow(canvas, _shadows[i]);
            else DrawOuterShadow(canvas, _shadows[i]);
        }
    }

    void DrawOutline(SKCanvas canvas, BoxOutline outline)
    {
        var ring = new SKRoundRect(_box);
        ring.Inflate(outline.Offset + outline.Width * 0.5f, outline.Offset + outline.Width * 0.5f);
        canvas.DrawRoundRect(ring, Stroke(outline.Color, outline.Width));
    }

    /// <summary>The shadow of the offset, spread box, kept outside the box like CSS.</summary>
    void DrawOuterShadow(SKCanvas canvas, BoxShadow shadow)
    {
        var cast = new SKRoundRect(_box);
        cast.Offset(shadow.X, shadow.Y);
        cast.Inflate(shadow.Spread, shadow.Spread);

        canvas.Save();
        canvas.ClipRoundRect(_box, SKClipOperation.Difference, antialias: true);
        canvas.DrawRoundRect(cast, ShadowPaint(shadow));
        canvas.Restore();
    }

    /// <summary>The shadow the box edge casts inward around an offset, shrunk hole, kept inside the box.</summary>
    void DrawInsetShadow(SKCanvas canvas, BoxShadow shadow)
    {
        var hole = new SKRoundRect(_box);
        hole.Offset(shadow.X, shadow.Y);
        hole.Inflate(-shadow.Spread, -shadow.Spread);
        var pad = shadow.Blur + Math.Abs(shadow.X) + Math.Abs(shadow.Y) + Math.Abs(shadow.Spread) + 1f;
        var outer = new SKRoundRect(SKRect.Inflate(_box.Rect, pad, pad));

        canvas.Save();
        canvas.ClipRoundRect(_box, SKClipOperation.Intersect, antialias: true);
        if (hole.Rect.IsEmpty) canvas.DrawRoundRect(outer, ShadowPaint(shadow));
        else canvas.DrawRoundRectDifference(outer, hole, ShadowPaint(shadow));
        canvas.Restore();
    }

    static SKPaint ShadowPaint(BoxShadow shadow) => new()
    {
        IsAntialias = true,
        Color = shadow.Color,
        MaskFilter = shadow.Blur > 0f ? SKMaskFilter.CreateBlur(SKBlurStyle.Normal, shadow.Blur * 0.5f) : null,
    };

    static SKPaint Stroke(Color color, float width) =>
        new() { IsAntialias = true, Color = color, Style = SKPaintStyle.Stroke, StrokeWidth = width };
}
