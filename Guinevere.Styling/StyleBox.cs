using System.Numerics;
using SkiaSharp;

namespace Guinevere;

/// <summary>
/// The visual part of a styled node: a per-corner rounded <see cref="Shape"/> with its background (color or
/// gradient) and CSS shadows, then the border and the outline.
/// </summary>
sealed class StyleBox : IDrawable, IInkBounds
{
    readonly Shape _shape;
    readonly SKRoundRect _box;
    readonly (Color Color, float Width)? _border;
    readonly BoxOutline? _outline;

    StyleBox(Shape shape, SKRoundRect box, (Color, float)? border, BoxOutline? outline)
    {
        _shape = shape;
        _box = box;
        _border = border;
        _outline = outline;
    }

    /// <inheritdoc/>
    public SKPaint? Paint => null;

    /// <inheritdoc/>
    public SKRect? InkBounds(LayoutNode node)
    {
        var reach = Math.Max(_border?.Width * 0.5f ?? 0f, _outline is { } o ? o.Offset + o.Width : 0f) + 1f;
        return _shape.InkBounds(node) is { } shape ? Ink.Join(shape, SKRect.Inflate(_box.Rect, reach, reach)) : null;
    }

    /// <summary>
    /// Builds the box for a resolved style over <paramref name="rect"/>, or <c>null</c> when the style draws nothing.
    /// </summary>
    public static StyleBox? From(ResolvedStyle style, Rect rect)
    {
        var bounds = new SKRect(rect.X, rect.Y, rect.X + rect.W, rect.Y + rect.H);
        var (fill, opaqueGradient) = Fill(style, bounds);
        var shadows = StyleBoxValues.Shadows(style.Get("box-shadow"));
        var border = Border(style);
        var outline = StyleBoxValues.Outline(style);
        if (fill is null && shadows.Count == 0 && border is null && outline is null) return null;

        var box = new SKRoundRect();
        box.SetRectRadii(bounds, StyleBoxValues.Radii(style.Get("border-radius"), rect));
        var shape = Shaped(box, fill, shadows);
        if (opaqueGradient) shape.OpaqueFill = true;
        return new StyleBox(shape, box, border, outline);
    }

    static (Color, float)? Border(ResolvedStyle style)
    {
        var width = style.GetLength("border-width") ?? 0f;
        return style.GetColor("border-color") is { } color && width > 0f ? (color, width) : null;
    }

    /// <summary>The rounded box as a <see cref="Shape"/> with its fill (transparent when none) and CSS shadows.</summary>
    static Shape Shaped(SKRoundRect box, SKPaint? fill, List<BoxShadow> shadows)
    {
        var builder = new SKPathBuilder();
        builder.AddRoundRect(box);
        var shape = Shape.FromPath(builder.Detach(), fill ?? new SKPaint { Color = SKColors.Transparent });
        foreach (var shadow in shadows)
        {
            var offset = new Vector2(shadow.X, shadow.Y);
            if (shadow.Inset) shape.InnerShadow(shadow.Color, offset, shadow.Blur, shadow.Spread);
            else shape.OuterShadow(shadow.Color, offset, shadow.Blur, shadow.Spread);
        }
        return shape;
    }

    /// <summary>
    /// A <c>linear-gradient</c> from <c>background</c> or <c>background-image</c> over the solid
    /// <c>background-color</c>/<c>bg-color</c>/<c>background</c> color, and whether the gradient is fully opaque.
    /// </summary>
    static (SKPaint? Fill, bool OpaqueGradient) Fill(ResolvedStyle style, SKRect bounds)
    {
        var shader = StyleBoxValues.Gradient(style.Get("background-image"), bounds, out var opaque)
                     ?? StyleBoxValues.Gradient(style.Get("background"), bounds, out opaque);
        if (shader is not null) return (new SKPaint { IsAntialias = true, Shader = shader }, opaque);

        var color = style.GetColor("background-color") ?? style.GetColor("bg-color") ?? style.GetColor("background");
        return (color is { } c ? new SKPaint { IsAntialias = true, Color = c } : null, false);
    }

    /// <inheritdoc/>
    public void Render(Gui gui, LayoutNode node, SKCanvas canvas)
    {
        _shape.Render(gui, node, canvas);
        if (_border is { } border) canvas.DrawRoundRect(_box, Stroke(border.Color, border.Width));
        if (_outline is not { } outline) return;
        var ring = new SKRoundRect(_box);
        ring.Inflate(outline.Offset + outline.Width * 0.5f, outline.Offset + outline.Width * 0.5f);
        canvas.DrawRoundRect(ring, Stroke(outline.Color, outline.Width));
    }

    static SKPaint Stroke(Color color, float width) =>
        new() { IsAntialias = true, Color = color, Style = SKPaintStyle.Stroke, StrokeWidth = width };
}
