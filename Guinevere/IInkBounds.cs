namespace Guinevere;

/// <summary>A drawable that knows the area it paints, so an opacity group can use a tight offscreen layer.</summary>
interface IInkBounds
{
    /// <summary>The painted area in canvas coordinates, or <c>null</c> when it cannot be bounded.</summary>
    /// <param name="node">The node the drawable belongs to.</param>
    SKRect? InkBounds(LayoutNode node);
}

/// <summary>Helpers for combining ink bounds.</summary>
static class Ink
{
    /// <summary>The union of two areas, where an empty area adds nothing.</summary>
    public static SKRect Join(SKRect a, SKRect b) => a.IsEmpty ? b : b.IsEmpty ? a : SKRect.Union(a, b);

    /// <summary>The area a stroke or fill with <paramref name="paint"/> covers around <paramref name="bounds"/>;
    /// <c>null</c> when an image or mask filter makes it unbounded.</summary>
    public static SKRect? Painted(SKRect bounds, SKPaint? paint)
    {
        if (paint is null) return bounds;
        if (paint.ImageFilter is not null || paint.MaskFilter is not null) return null;
        var half = paint.Style == SKPaintStyle.Fill ? 0f : paint.StrokeWidth * 0.5f + 1f;
        return SKRect.Inflate(bounds, half, half);
    }
}
