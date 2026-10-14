namespace Guinevere;

/// <summary>Resolved safe rectangle, logical canvas, geometry scale and text scale.</summary>
public readonly record struct ViewportLayout(
    Rect SafeBounds, Rect ContentBounds, float Scale, float TextScale, Matrix3x2 Transform)
{
    /// <summary>Places a fixed size at a normalized anchor, or stretches between two normalized anchors.</summary>
    public Rect Anchor(Vector2 min, Vector2 max, Vector2 size, Vector2 pivot, ViewportInsets offsets = default)
    {
        ValidateAnchor(min);
        ValidateAnchor(max);
        ValidateAnchor(pivot);
        if (max.X < min.X || max.Y < min.Y) throw new ArgumentOutOfRangeException(nameof(max));
        ValidateSize(size);
        var available = offsets.Apply(ContentBounds);
        var span = new Vector2(available.W, available.H);
        var position = new Vector2(available.X, available.Y) + span * min;
        var horizontal = ResolveAxis(min.X, max.X, span.X, size.X, pivot.X);
        var vertical = ResolveAxis(min.Y, max.Y, span.Y, size.Y, pivot.Y);
        return new Rect(position.X - horizontal.Offset, position.Y - vertical.Offset,
            horizontal.Size, vertical.Size);
    }

    static (float Size, float Offset) ResolveAxis(float min, float max, float span, float size, float pivot) =>
        min == max ? (size, size * pivot) : (span * (max - min), 0);

    static void ValidateSize(Vector2 size)
    {
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X < 0 || size.Y < 0)
            throw new ArgumentOutOfRangeException(nameof(size));
    }

    static void ValidateAnchor(Vector2 value)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y)
            || value.X < 0 || value.X > 1 || value.Y < 0 || value.Y > 1)
            throw new ArgumentOutOfRangeException(nameof(value));
    }
}

sealed class LayoutNodeScopeViewport : ILayoutNodeScopeValue<LayoutNodeScopeViewport>
{
    public static LayoutNodeScopeViewport Default { get; } = new();
    public ViewportLayout? Value { get; init; }
}
