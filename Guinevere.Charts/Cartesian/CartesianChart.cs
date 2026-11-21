using System.Numerics;

namespace Guinevere.Charts;

/// <summary>Records Cartesian chart primitives using caller-provided data and colors.</summary>
public static class CartesianChart
{
    /// <summary>Draws a line through all samples or the supplied decimated indices. Invalid samples break the line.</summary>
    public static void DrawLine(Gui gui, CartesianPlot plot, ReadOnlySpan<ChartPoint> points, Color color,
        float thickness = 1, ReadOnlySpan<int> indices = default)
    {
        if (gui.Pass != Pass.Pass2Render || !plot.IsValid || !float.IsFinite(thickness) || thickness <= 0)
        {
            return;
        }
        var previous = new ChartPoint(double.NaN, double.NaN);
        var count = indices.IsEmpty ? points.Length : indices.Length;
        for (var i = 0; i < count; i++)
        {
            var index = indices.IsEmpty ? i : indices[i];
            var current = (uint)index < (uint)points.Length
                ? new ChartPoint(plot.X.Normalize(points[index].X), plot.Y.Normalize(points[index].Y))
                : new ChartPoint(double.NaN, double.NaN);
            DrawSegment(gui, plot, previous, current, color, thickness);
            previous = current;
        }
    }

    /// <summary>Returns the nearest visible line sample within a pixel radius, or -1.</summary>
    public static int HitLineSample(CartesianPlot plot, ReadOnlySpan<ChartPoint> points, Vector2 pointer,
        float radius = 6, ReadOnlySpan<int> indices = default)
    {
        if (!plot.IsValid || !ChartArea.Contains(plot.Area, pointer) || !float.IsFinite(radius) || radius < 0)
        {
            return -1;
        }
        var best = (double)radius * radius;
        var result = -1;
        var count = indices.IsEmpty ? points.Length : indices.Length;
        for (var i = 0; i < count; i++)
        {
            var index = indices.IsEmpty ? i : indices[i];
            if ((uint)index >= (uint)points.Length || !plot.TryProjectVisible(points[index], out var screen))
            {
                continue;
            }
            var distance = Vector2.DistanceSquared(pointer, screen);
            if (distance <= best)
            {
                result = index;
                best = distance;
            }
        }
        return result;
    }

    /// <summary>Draws point markers, optionally using a per-point radius in pixels for bubbles.</summary>
    public static void DrawPoints(Gui gui, CartesianPlot plot, ReadOnlySpan<ChartPoint> points, Color color,
        float radius = 3, ReadOnlySpan<float> radii = default)
    {
        if (gui.Pass != Pass.Pass2Render || !plot.IsValid || !HasRadii(points, radii))
        {
            return;
        }
        for (var i = 0; i < points.Length; i++)
        {
            var size = radii.IsEmpty ? radius : radii[i];
            if (float.IsFinite(size) && size > 0 && plot.TryProjectVisible(points[i], out var center))
            {
                gui.DrawCircle(center, size, color);
            }
        }
    }

    /// <summary>Returns the topmost source index whose marker contains the pointer, or -1.</summary>
    public static int HitPoints(CartesianPlot plot, ReadOnlySpan<ChartPoint> points, Vector2 pointer,
        float radius = 3, ReadOnlySpan<float> radii = default)
    {
        if (!plot.IsValid || !ChartArea.Contains(plot.Area, pointer) || !HasRadii(points, radii))
        {
            return -1;
        }
        for (var i = points.Length - 1; i >= 0; i--)
        {
            var size = radii.IsEmpty ? radius : radii[i];
            if (float.IsFinite(size) && size > 0 && plot.TryProjectVisible(points[i], out var center) &&
                Vector2.DistanceSquared(pointer, center) <= (double)size * size)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Draws vertical bars, centered on each X value and bounded by the visible plot.</summary>
    public static void DrawBars(Gui gui, CartesianPlot plot, ReadOnlySpan<ChartPoint> points, Color color,
        float barWidth, double baseline = 0)
    {
        if (gui.Pass != Pass.Pass2Render || !plot.IsValid)
        {
            return;
        }
        for (var i = 0; i < points.Length; i++)
        {
            if (TryBar(plot, points[i], barWidth, baseline, out var rect))
            {
                gui.DrawRect(rect, color);
            }
        }
    }

    /// <summary>Returns the topmost source index whose visible bar contains the pointer, or -1.</summary>
    public static int HitBars(CartesianPlot plot, ReadOnlySpan<ChartPoint> points, Vector2 pointer,
        float barWidth, double baseline = 0)
    {
        if (!plot.IsValid || !ChartArea.Contains(plot.Area, pointer))
        {
            return -1;
        }
        for (var i = points.Length - 1; i >= 0; i--)
        {
            if (TryBar(plot, points[i], barWidth, baseline, out var rect) && ChartArea.Contains(rect, pointer))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Draws plot bounds and grid lines for caller-supplied ticks.</summary>
    public static void DrawGrid(Gui gui, CartesianPlot plot, ReadOnlySpan<double> xTicks, ReadOnlySpan<double> yTicks,
        Color axisColor, Color gridColor)
    {
        if (gui.Pass != Pass.Pass2Render || !plot.IsValid)
        {
            return;
        }
        foreach (var tick in xTicks)
        {
            var x = plot.X.Normalize(tick);
            if (x is > 0 and < 1)
            {
                gui.DrawLine(plot.Project(x, 0), plot.Project(x, 1), gridColor);
            }
        }
        foreach (var tick in yTicks)
        {
            var y = plot.Y.Normalize(tick);
            if (y is > 0 and < 1)
            {
                gui.DrawLine(plot.Project(0, y), plot.Project(1, y), gridColor);
            }
        }
        gui.DrawRectBorder(plot.Area, axisColor);
    }

    static bool HasRadii(ReadOnlySpan<ChartPoint> points, ReadOnlySpan<float> radii) =>
        radii.IsEmpty || radii.Length == points.Length;

    static bool TryBar(CartesianPlot plot, ChartPoint point, float width, double baseline, out Rect rect)
    {
        rect = default;
        var x = plot.X.Normalize(point.X);
        if (!CartesianPlot.IsUnit(x) || !double.IsFinite(point.Y) || !double.IsFinite(baseline) ||
            !float.IsFinite(width) || width <= 0)
        {
            return false;
        }
        var y = plot.Y.Normalize(Math.Clamp(point.Y, plot.Y.Min, plot.Y.Max));
        var origin = plot.Y.Normalize(Math.Clamp(baseline, plot.Y.Min, plot.Y.Max));
        var top = plot.Project(x, Math.Max(y, origin));
        var bottom = plot.Project(x, Math.Min(y, origin));
        var left = Math.Max(top.X - width / 2, plot.Area.X);
        var right = Math.Min(top.X + width / 2, plot.Area.X + plot.Area.W);
        rect = new Rect(left, top.Y, right - left, bottom.Y - top.Y);
        return rect.W > 0 && rect.H > 0;
    }

    static void DrawSegment(Gui gui, CartesianPlot plot, ChartPoint from, ChartPoint to, Color color, float thickness)
    {
        if (CartesianPlot.IsUnit(from.X) && CartesianPlot.IsUnit(from.Y) &&
            CartesianPlot.IsUnit(to.X) && CartesianPlot.IsUnit(to.Y))
        {
            gui.DrawLine(plot.Project(from.X, from.Y), plot.Project(to.X, to.Y), color, thickness);
            return;
        }
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        if (!double.IsFinite(dx) || !double.IsFinite(dy))
        {
            return;
        }
        var start = 0d;
        var end = 1d;
        if (!Clip(-dx, from.X, ref start, ref end) || !Clip(dx, 1 - from.X, ref start, ref end) ||
            !Clip(-dy, from.Y, ref start, ref end) || !Clip(dy, 1 - from.Y, ref start, ref end))
        {
            return;
        }
        var a = plot.Project(Math.Clamp(from.X + dx * start, 0, 1), Math.Clamp(from.Y + dy * start, 0, 1));
        var b = plot.Project(Math.Clamp(from.X + dx * end, 0, 1), Math.Clamp(from.Y + dy * end, 0, 1));
        gui.DrawLine(a, b, color, thickness);
    }

    static bool Clip(double direction, double distance, ref double start, ref double end)
    {
        if (direction is 0)
        {
            return distance >= 0;
        }
        var crossing = distance / direction;
        if (direction < 0)
        {
            start = Math.Max(start, crossing);
        }
        else
        {
            end = Math.Min(end, crossing);
        }
        return start <= end;
    }
}
