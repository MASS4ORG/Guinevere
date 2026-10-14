namespace Guinevere;

/// <summary>Shared source and destination geometry for nine-slice rendering and authoring guides.</summary>
public readonly record struct NineSliceLayout
{
    /// <summary>Creates a grid with clamped source borders and proportionally reduced destination corners.</summary>
    public NineSliceLayout(float imageWidth, float imageHeight, Rect destination, Insets border)
    {
        Source = new Rect(0, 0, Positive(imageWidth), Positive(imageHeight));
        Destination = new Rect(destination.X, destination.Y, Positive(destination.W), Positive(destination.H));
        var left = Math.Clamp(Positive(border.Left), 0, Source.W);
        var right = Math.Clamp(Positive(border.Right), 0, Source.W - left);
        var top = Math.Clamp(Positive(border.Top), 0, Source.H);
        var bottom = Math.Clamp(Positive(border.Bottom), 0, Source.H - top);
        Border = new Insets(left, top, right, bottom);
        SourceCenter = new Rect(left, top, Source.W - left - right, Source.H - top - bottom);
        var sx = Border.Horizontal > Destination.W ? Destination.W / Border.Horizontal : 1f;
        var sy = Border.Vertical > Destination.H ? Destination.H / Border.Vertical : 1f;
        DestinationCenter = new Rect(Destination.X + left * sx, Destination.Y + top * sy,
            Math.Max(0, Destination.W - Border.Horizontal * sx),
            Math.Max(0, Destination.H - Border.Vertical * sy));
    }

    /// <summary>The whole source image in pixels.</summary>
    public Rect Source { get; }

    /// <summary>The destination with nonnegative dimensions.</summary>
    public Rect Destination { get; }

    /// <summary>The source borders after clamping to the image.</summary>
    public Insets Border { get; }

    /// <summary>The source region that stretches along both axes.</summary>
    public Rect SourceCenter { get; }

    /// <summary>The destination region between the preserved corners.</summary>
    public Rect DestinationCenter { get; }

    /// <summary>Returns corresponding patch rectangles for a zero-based row and column.</summary>
    public (Rect Source, Rect Destination) Patch(int row, int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(row, 2);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(column, 2);
        return (Cell(Source, SourceCenter, row, column), Cell(Destination, DestinationCenter, row, column));
    }

    static Rect Cell(Rect outer, Rect center, int row, int column)
    {
        var x = Boundary(outer.X, outer.W, center.X, center.W, column);
        var right = Boundary(outer.X, outer.W, center.X, center.W, column + 1);
        var y = Boundary(outer.Y, outer.H, center.Y, center.H, row);
        var bottom = Boundary(outer.Y, outer.H, center.Y, center.H, row + 1);
        return new Rect(x, y, right - x, bottom - y);
    }

    static float Boundary(float origin, float size, float center, float centerSize, int index) => index switch
    {
        0 => origin,
        1 => center,
        2 => center + centerSize,
        _ => origin + size,
    };

    static float Positive(float value) => float.IsFinite(value) ? Math.Max(0, value) : 0;
}
