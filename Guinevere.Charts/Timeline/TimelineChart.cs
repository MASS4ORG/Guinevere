using System.Numerics;

namespace Guinevere.Charts;

/// <summary>An interval, its row and its caller-selected fill color.</summary>
/// <param name="Start">Start time in the plot's units.</param>
/// <param name="End">End time in the plot's units.</param>
/// <param name="Row">Zero-based lane index.</param>
/// <param name="Color">Fill color for the interval.</param>
public readonly record struct TimelineSpan(double Start, double End, int Row, Color Color);

/// <summary>Draws and hit-tests caller-owned timeline intervals.</summary>
public static class TimelineChart
{
    /// <summary>Draws visible intervals in source order without retaining item state.</summary>
    public static void Draw(Gui gui, TimelinePlot plot, ReadOnlySpan<TimelineSpan> spans, float minimumWidth = 1,
        float horizontalGap = 0, float rowGap = 0)
    {
        if (gui.Pass != Pass.Pass2Render || !plot.IsValid)
        {
            return;
        }
        foreach (var span in spans)
        {
            DrawInterval(gui, plot, span, out _, minimumWidth, horizontalGap, rowGap);
        }
    }

    /// <summary>Draws a visible interval and returns its clipped bounds for labels or selection outlines.</summary>
    public static bool DrawInterval(Gui gui, TimelinePlot plot, TimelineSpan span, out Rect bounds,
        float minimumWidth = 1, float horizontalGap = 0, float rowGap = 0, float radius = 0)
    {
        bounds = default;
        if (gui.Pass != Pass.Pass2Render ||
            !plot.TryProject(span.Start, span.End, span.Row, out bounds, minimumWidth, horizontalGap, rowGap))
        {
            return false;
        }
        gui.DrawRect(bounds, span.Color, radius);
        return true;
    }

    /// <summary>Returns the last visible interval under the pointer, or -1. Minimum width may enlarge hits.</summary>
    public static int HitTest(TimelinePlot plot, ReadOnlySpan<TimelineSpan> spans, Vector2 pointer,
        float minimumWidth = 1, float horizontalGap = 0, float rowGap = 0)
    {
        if (!plot.IsValid || !ChartArea.Contains(plot.Area, pointer))
        {
            return -1;
        }
        for (var i = spans.Length - 1; i >= 0; i--)
        {
            var span = spans[i];
            if (plot.TryProject(span.Start, span.End, span.Row, out var bounds, minimumWidth, horizontalGap, rowGap) &&
                ChartArea.Contains(bounds, pointer))
            {
                return i;
            }
        }
        return -1;
    }
}
