namespace Guinevere;

enum PrimitiveKind : byte
{
    Rect,
    Circle,
    Line,
    Triangle
}

/// <summary>Stores direct geometry and paint values without owning native resources.</summary>
readonly record struct PrimitiveCommand(
    PrimitiveKind Kind, Rect Rect, SKColor Color, float Radius = 0, Corner Corners = Corner.All,
    float Thickness = 0, bool Stroke = false, Vector2 A = default, Vector2 B = default, Vector2 C = default,
    SKColor ColorB = default, SKColor ColorC = default)
{
    internal SKRect InkBounds()
    {
        SKRect bounds = Kind switch
        {
            PrimitiveKind.Circle => new(A.X - Radius, A.Y - Radius, A.X + Radius, A.Y + Radius),
            PrimitiveKind.Line => PointBounds(A, B, A),
            PrimitiveKind.Triangle => PointBounds(A, B, C),
            _ => Rect
        };
        var padding = Stroke ? MathF.Abs(Thickness) * 0.5f + 1f : 0f;
        if (Kind == PrimitiveKind.Line) padding = MathF.Max(1f, Thickness) * 0.5f + 1f;
        bounds.Inflate(padding, padding);
        return bounds;
    }

    static SKRect PointBounds(Vector2 a, Vector2 b, Vector2 c) =>
        new(MathF.Min(a.X, MathF.Min(b.X, c.X)), MathF.Min(a.Y, MathF.Min(b.Y, c.Y)),
            MathF.Max(a.X, MathF.Max(b.X, c.X)), MathF.Max(a.Y, MathF.Max(b.Y, c.Y)));
}
