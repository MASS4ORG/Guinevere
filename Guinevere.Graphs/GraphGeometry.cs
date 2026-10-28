namespace Guinevere.Graphs;

/// <summary>A double-precision point in graph view coordinates, independent of model identity or payload.</summary>
public readonly record struct GraphPoint(double X, double Y);

/// <summary>A finite, nonnegative rectangle in graph view coordinates.</summary>
public readonly record struct GraphBounds
{
    /// <summary>Creates a rectangle; invalid or overflowing coordinates are rejected.</summary>
    public GraphBounds(double x, double y, double width, double height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        RequireFinite(x, y, width, height);
        RequireFinite(x + width, y + height);
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>The left coordinate.</summary>
    public double X { get; }
    /// <summary>The top coordinate.</summary>
    public double Y { get; }
    /// <summary>The horizontal extent.</summary>
    public double Width { get; }
    /// <summary>The vertical extent.</summary>
    public double Height { get; }
    /// <summary>The right coordinate.</summary>
    public double Right => X + Width;
    /// <summary>The bottom coordinate.</summary>
    public double Bottom => Y + Height;
    /// <summary>The center in graph coordinates.</summary>
    public GraphPoint Center => new(X + Width / 2, Y + Height / 2);

    /// <summary>Includes touching borders, so thin links and point markers remain visible.</summary>
    public bool Intersects(GraphBounds other) =>
        X <= other.Right && Right >= other.X && Y <= other.Bottom && Bottom >= other.Y;

    /// <summary>Whether this rectangle completely contains another rectangle, including its borders.</summary>
    public bool Contains(GraphBounds other) =>
        X <= other.X && Y <= other.Y && Right >= other.Right && Bottom >= other.Bottom;

    /// <summary>Returns the smallest rectangle enclosing both rectangles.</summary>
    public GraphBounds Union(GraphBounds other)
    {
        var x = Math.Min(X, other.X);
        var y = Math.Min(Y, other.Y);
        return new GraphBounds(x, y, Math.Max(Right, other.Right) - x, Math.Max(Bottom, other.Bottom) - y);
    }

    internal static void RequireFinite(params ReadOnlySpan<double> values)
    {
        foreach (var value in values)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(values), "Coordinates must be finite.");
            }
        }
    }
}
