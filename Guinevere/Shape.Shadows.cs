namespace Guinevere;

/// <summary>One CSS-style shadow on a <see cref="Shape"/>, computed from the shape's path when drawn.</summary>
readonly record struct ShapeShadow(bool Inset, Vector2 Offset, float Blur, float Spread, Color Color);

public partial class Shape
{
    List<ShapeShadow>? _shadows;

    /// <summary>A shape over an existing path, filled with <paramref name="paint"/>.</summary>
    internal static Shape FromPath(SKPath path, SKPaint paint) => new(path, paint);

    void AddShadow(ShapeShadow shadow) => (_shadows ??= []).Add(shadow);

    /// <summary>Copies another shape's shadows, which follow this shape's own path.</summary>
    internal void CopyShadows(Shape source)
    {
        if (source._shadows is not null) (_shadows ??= []).AddRange(source._shadows);
    }

    /// <inheritdoc cref="IInkBounds.InkBounds"/>
    internal virtual SKRect? InkBounds(LayoutNode node)
    {
        var ink = SKRect.Empty;
        foreach (var (_, layerList) in Layers)
            foreach (var (path, paint) in layerList)
            {
                if (Guinevere.Ink.Painted(path.Bounds, paint) is not { } painted) return null;
                ink = Guinevere.Ink.Join(ink, painted);
            }
        return ShadowBounds(ink);
    }

    SKRect? IInkBounds.InkBounds(LayoutNode node) => InkBounds(node);

    /// <summary>How far outer shadows reach beyond the path bounds; a blur fades out within about 1.5 radii.</summary>
    internal SKRect ShadowBounds(SKRect bounds)
    {
        if (_shadows is null) return bounds;
        var ink = bounds;
        foreach (var shadow in _shadows)
        {
            if (shadow.Inset) continue;
            var reach = Math.Max(0f, shadow.Spread) + shadow.Blur * 1.5f;
            var cast = SKRect.Inflate(bounds, reach, reach);
            cast.Offset(shadow.Offset.X, shadow.Offset.Y);
            ink = SKRect.Union(ink, cast);
        }
        return ink;
    }

    /// <summary>Draws the outer or inset shadows bottom-up, so the first one added ends on top like CSS.</summary>
    void DrawShadows(SKCanvas canvas, bool inset)
    {
        if (_shadows is null) return;
        var box = AsRoundRect(Path);
        var covered = CoversShadows();
        for (var i = _shadows.Count - 1; i >= 0; i--)
            if (_shadows[i].Inset == inset) DrawShadow(canvas, box, _shadows[i], covered);
    }

    void DrawShadow(SKCanvas canvas, SKRoundRect? box, ShapeShadow shadow, bool covered)
    {
        if (box is not null) DrawBoxShadow(canvas, box, shadow, covered);
        else if (shadow.Inset) DrawInsetShadow(canvas, Path, shadow);
        else DrawOuterShadow(canvas, Path, shadow);
    }

    bool CoversShadows() => OpaqueFill ?? Paint is { Shader: null, Style: SKPaintStyle.Fill, Color.Alpha: 255 };

    /// <summary>
    /// Whether the fill hides everything under the shape, so outer shadows need no clip; <c>null</c> infers it from a
    /// solid, opaque <see cref="Paint"/>. Set it for an opaque gradient.
    /// </summary>
    internal bool? OpaqueFill { get; set; }

    /// <summary>The path as a rounded rectangle (plain rectangles and ovals included), or <c>null</c>.</summary>
    static SKRoundRect? AsRoundRect(SKPath path)
    {
        if (path.IsRoundRect) return path.GetRoundRect();
        if (path.IsRect) return new SKRoundRect(path.GetRect(), 0f, 0f);
        if (!path.IsOval) return null;
        var oval = path.GetOvalBounds();
        return new SKRoundRect(oval, oval.Width * 0.5f, oval.Height * 0.5f);
    }

    /// <summary>
    /// The rounded-rectangle fast path: the same shadows as the general one, built by offsetting and inflating the
    /// box instead of path operations, so Skia can use its cached blurred round-rect masks.
    /// </summary>
    static void DrawBoxShadow(SKCanvas canvas, SKRoundRect box, ShapeShadow shadow, bool covered)
    {
        var cast = new SKRoundRect(box);
        cast.Offset(shadow.Offset.X, shadow.Offset.Y);
        var grow = shadow.Inset ? -shadow.Spread : shadow.Spread;
        cast.Inflate(grow, grow);

        if (TryDrawUnclipped(canvas, box, cast, shadow, covered)) return;

        canvas.Save();
        canvas.ClipRoundRect(box, shadow.Inset ? SKClipOperation.Intersect : SKClipOperation.Difference, true);
        if (!shadow.Inset) canvas.DrawRoundRect(cast, ShadowPaint(shadow));
        else DrawAround(canvas, box, cast, shadow);
        canvas.Restore();
    }

    /// <summary>
    /// Draws without a clip when that gives the same result: an outer shadow the opaque fill will paint over, or a
    /// sharp inset ring whose hole lies inside the box.
    /// </summary>
    static bool TryDrawUnclipped(SKCanvas canvas, SKRoundRect box, SKRoundRect cast, ShapeShadow shadow, bool covered)
    {
        if (!shadow.Inset && covered) canvas.DrawRoundRect(cast, ShadowPaint(shadow));
        else if (IsSharpRing(box, cast, shadow)) canvas.DrawRoundRectDifference(box, cast, ShadowPaint(shadow));
        else return false;
        return true;
    }

    static bool IsSharpRing(SKRoundRect box, SKRoundRect hole, ShapeShadow shadow) =>
        shadow is { Inset: true, Blur: 0f } && !hole.Rect.IsEmpty && box.Rect.Contains(hole.Rect);

    /// <summary>Paints everything around <paramref name="hole"/> out to beyond the blur's reach.</summary>
    static void DrawAround(SKCanvas canvas, SKRoundRect box, SKRoundRect hole, ShapeShadow shadow)
    {
        var pad = shadow.Blur + Math.Abs(shadow.Offset.X) + Math.Abs(shadow.Offset.Y) + Math.Abs(shadow.Spread) + 1f;
        var frame = new SKRoundRect(SKRect.Inflate(box.Rect, pad, pad), 0f, 0f);
        if (hole.Rect.IsEmpty) canvas.DrawRoundRect(frame, ShadowPaint(shadow));
        else canvas.DrawRoundRectDifference(frame, hole, ShadowPaint(shadow));
    }

    /// <summary>The path shifted by the offset and grown by the spread, blurred and kept outside the path.</summary>
    static void DrawOuterShadow(SKCanvas canvas, SKPath path, ShapeShadow shadow)
    {
        var cast = Spread(Shifted(path, shadow.Offset), shadow.Spread);
        canvas.Save();
        canvas.ClipPath(path, SKClipOperation.Difference, antialias: true);
        canvas.DrawPath(cast, ShadowPaint(shadow));
        canvas.Restore();
    }

    /// <summary>
    /// Everything around the shifted path, shrunk by the spread, blurred and kept inside the path: the edge's
    /// shadow falls on the side the offset moves away from.
    /// </summary>
    static void DrawInsetShadow(SKCanvas canvas, SKPath path, ShapeShadow shadow)
    {
        var hole = Spread(Shifted(path, shadow.Offset), -shadow.Spread);
        var pad = shadow.Blur + Math.Abs(shadow.Offset.X) + Math.Abs(shadow.Offset.Y) + Math.Abs(shadow.Spread) + 1f;
        var builder = new SKPathBuilder();
        builder.AddRect(SKRect.Inflate(path.Bounds, pad, pad));
        using var frame = builder.Detach();
        var ring = new SKPath();
        if (!frame.Op(hole, SKPathOp.Difference, ring)) ring = new SKPath(frame);

        canvas.Save();
        canvas.ClipPath(path, SKClipOperation.Intersect, antialias: true);
        canvas.DrawPath(ring, ShadowPaint(shadow));
        canvas.Restore();
    }

    static SKPath Shifted(SKPath path, Vector2 offset)
    {
        var shifted = new SKPath(path);
        if (offset != Vector2.Zero) shifted.Transform(SKMatrix.CreateTranslation(offset.X, offset.Y));
        return shifted;
    }

    /// <summary>Grows (positive) or shrinks (negative) a path by a round-joined band of <paramref name="amount"/>.</summary>
    static SKPath Spread(SKPath path, float amount)
    {
        if (amount == 0f) return path;
        using var band = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Abs(amount) * 2f,
            StrokeJoin = SKStrokeJoin.Round,
        }.GetFillPath(path);
        var result = new SKPath();
        return path.Op(band, amount > 0f ? SKPathOp.Union : SKPathOp.Difference, result) ? result : path;
    }

    static SKPaint ShadowPaint(ShapeShadow shadow) => new()
    {
        IsAntialias = true,
        Color = shadow.Color,
        MaskFilter = shadow.Blur > 0f ? SKMaskFilter.CreateBlur(SKBlurStyle.Normal, shadow.Blur * 0.5f) : null,
    };
}
