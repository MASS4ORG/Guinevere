namespace Guinevere;

[Flags]
enum ResizeEdge
{
    Left = 1,
    Right = 2,
    Top = 4,
    Bottom = 8
}

static class ResizeGeometry
{
    internal static readonly (ResizeEdge Edge, PointerCursor Cursor)[] Handles =
    [
        (ResizeEdge.Left, PointerCursor.ResizeHorizontal),
        (ResizeEdge.Right, PointerCursor.ResizeHorizontal),
        (ResizeEdge.Top, PointerCursor.ResizeVertical),
        (ResizeEdge.Bottom, PointerCursor.ResizeVertical),
        (ResizeEdge.Left | ResizeEdge.Top, PointerCursor.ResizeDiagonalNorthWestSouthEast),
        (ResizeEdge.Right | ResizeEdge.Bottom, PointerCursor.ResizeDiagonalNorthWestSouthEast),
        (ResizeEdge.Right | ResizeEdge.Top, PointerCursor.ResizeDiagonalNorthEastSouthWest),
        (ResizeEdge.Left | ResizeEdge.Bottom, PointerCursor.ResizeDiagonalNorthEastSouthWest)
    ];

    internal static Rect HandleRect(Rect bounds, ResizeEdge edge)
    {
        var corner = MathF.Min(12, MathF.Min(bounds.W, bounds.H) * 0.5f);
        var border = MathF.Min(6, corner);
        var right = bounds.X + bounds.W;
        var bottom = bounds.Y + bounds.H;
        return edge switch
        {
            ResizeEdge.Left => new Rect(bounds.X, bounds.Y + corner, border, bounds.H - 2 * corner),
            ResizeEdge.Right => new Rect(right - border, bounds.Y + corner, border, bounds.H - 2 * corner),
            ResizeEdge.Top => new Rect(bounds.X + corner, bounds.Y, bounds.W - 2 * corner, border),
            ResizeEdge.Bottom => new Rect(bounds.X + corner, bottom - border, bounds.W - 2 * corner, border),
            ResizeEdge.Left | ResizeEdge.Top => new Rect(bounds.X, bounds.Y, corner, corner),
            ResizeEdge.Right | ResizeEdge.Bottom => new Rect(right - corner, bottom - corner, corner, corner),
            ResizeEdge.Right | ResizeEdge.Top => new Rect(right - corner, bounds.Y, corner, corner),
            _ => new Rect(bounds.X, bottom - corner, corner, corner)
        };
    }

    internal static Rect Resize(Rect bounds, Vector2 delta, ResizeEdge edge, Vector2 minimum)
    {
        var horizontal = ResizeAxis(bounds.X, bounds.W, delta.X,
            (edge & ResizeEdge.Left) != 0, (edge & ResizeEdge.Right) != 0, MathF.Ceiling(minimum.X));
        var vertical = ResizeAxis(bounds.Y, bounds.H, delta.Y,
            (edge & ResizeEdge.Top) != 0, (edge & ResizeEdge.Bottom) != 0, MathF.Ceiling(minimum.Y));
        return new Rect(horizontal.Origin, vertical.Origin, horizontal.Size, vertical.Size);
    }

    static (float Origin, float Size) ResizeAxis(float origin, float size, float delta,
        bool leading, bool trailing, float minimum)
    {
        if (!leading && !trailing) return (origin, size);
        var resized = MathF.Max(minimum, MathF.Round(size + (leading ? -delta : delta)));
        return (leading ? origin + size - resized : origin, resized);
    }
}
