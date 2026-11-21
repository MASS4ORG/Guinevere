using System.Numerics;

namespace Guinevere.Charts;

/// <summary>A Cartesian viewport with data ranges and a screen-space drawing area.</summary>
/// <param name="Area">Screen-space plot bounds.</param>
/// <param name="X">Visible horizontal data range.</param>
/// <param name="Y">Visible vertical data range, increasing upward.</param>
public readonly record struct CartesianPlot(Rect Area, ChartRange X, ChartRange Y)
{
    /// <summary>Whether the bounds and both data ranges can be projected.</summary>
    public bool IsValid => ChartArea.IsValid(Area) && X.IsValid && Y.IsValid;

    /// <summary>Projects a finite point inside the visible ranges into screen coordinates.</summary>
    public bool TryProject(ChartPoint point, out Vector2 screen)
    {
        screen = default;
        return IsValid && TryProjectVisible(point, out screen);
    }

    internal bool TryProjectVisible(ChartPoint point, out Vector2 screen)
    {
        var x = X.Normalize(point.X);
        var y = Y.Normalize(point.Y);
        if (!IsUnit(x) || !IsUnit(y))
        {
            screen = default;
            return false;
        }
        screen = Project(x, y);
        return true;
    }

    /// <summary>Maps a screen position inside the plot back to its data coordinates.</summary>
    public bool TryUnproject(Vector2 screen, out ChartPoint point)
    {
        point = default;
        if (!IsValid || !ChartArea.Contains(Area, screen))
        {
            return false;
        }
        point = new ChartPoint(X.Denormalize((screen.X - Area.X) / Area.W),
            Y.Denormalize(1 - (screen.Y - Area.Y) / Area.H));
        return true;
    }

    /// <summary>Returns a viewport translated by the given screen-space content drag. Invalid moves leave it unchanged.</summary>
    public CartesianPlot Pan(Vector2 delta)
    {
        if (!IsValid || !float.IsFinite(delta.X) || !float.IsFinite(delta.Y))
        {
            return this;
        }
        var xOffset = -(double)delta.X / Area.W;
        var yOffset = (double)delta.Y / Area.H;
        return ChangeRanges(Remap(X, xOffset, xOffset + 1), Remap(Y, yOffset, yOffset + 1));
    }

    /// <summary>Scales the visible ranges around a screen point without moving the data under that point.</summary>
    /// <remarks>Factors above one zoom in. Use one to leave an axis fixed. Invalid moves leave the viewport unchanged.</remarks>
    public CartesianPlot Zoom(Vector2 anchor, double xFactor, double yFactor)
    {
        if (!IsValid || !ChartArea.Contains(Area, anchor) || !IsZoomFactor(xFactor) || !IsZoomFactor(yFactor))
        {
            return this;
        }
        var x = ((double)anchor.X - Area.X) / Area.W;
        var y = 1 - ((double)anchor.Y - Area.Y) / Area.H;
        return ChangeRanges(ZoomAxis(X, x, xFactor), ZoomAxis(Y, y, yFactor));
    }

    CartesianPlot ChangeRanges(ChartRange x, ChartRange y) =>
        x.IsValid && y.IsValid ? this with { X = x, Y = y } : this;

    static bool IsZoomFactor(double value) => double.IsFinite(value) && value > 0;

    static ChartRange ZoomAxis(ChartRange range, double anchor, double factor) =>
        Remap(range, anchor - anchor / factor, anchor + (1 - anchor) / factor);

    static ChartRange Remap(ChartRange range, double from, double to) =>
        new(range.Denormalize(from), range.Denormalize(to));

    internal Vector2 Project(double x, double y) =>
        new(Area.X + (float)x * Area.W, Area.Y + (float)(1 - y) * Area.H);

    internal static bool IsUnit(double value) => value is >= 0 and <= 1;
}
