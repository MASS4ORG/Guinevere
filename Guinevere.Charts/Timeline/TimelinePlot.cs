namespace Guinevere.Charts;

/// <summary>Projects time intervals into clipped rows of a timeline.</summary>
/// <param name="Area">Screen-space chart bounds.</param>
/// <param name="Time">Visible time range in caller-defined units.</param>
/// <param name="RowHeight">Height of a lane in screen pixels.</param>
public readonly record struct TimelinePlot(Rect Area, ChartRange Time, float RowHeight)
{
    /// <summary>Whether the timeline has finite bounds and a usable time scale.</summary>
    public bool IsValid => Time.IsValid && ChartArea.IsValid(Area) && float.IsFinite(RowHeight) && RowHeight > 0;

    /// <summary>Projects a visible interval, applying gaps and a minimum width within the plot.</summary>
    public bool TryProject(double start, double end, int row, out Rect bounds, float minimumWidth = 1,
        float horizontalGap = 0, float rowGap = 0)
    {
        bounds = default;
        if (!IsValid || !VisibleInterval(start, end) || row < 0 ||
            !ValidSpacing(minimumWidth) || !ValidSpacing(horizontalGap) || !ValidSpacing(rowGap))
        {
            return false;
        }

        var rightEdge = (double)Area.X + Area.W;
        var bottomEdge = (double)Area.Y + Area.H;
        var top = (double)Area.Y + row * (double)RowHeight;
        var bottom = Math.Min(bottomEdge, top + RowHeight - rowGap);
        var left = Area.X + Time.Normalize(Math.Max(start, Time.Min)) * Area.W;
        var right = Area.X + Time.Normalize(Math.Min(end, Time.Max)) * Area.W;
        right = Math.Min(rightEdge, Math.Max(left + minimumWidth, right - horizontalGap));
        if (right <= left || bottom <= top)
        {
            return false;
        }
        bounds = new Rect((float)left, (float)top, (float)(right - left), (float)(bottom - top));
        return bounds.W > 0 && bounds.H > 0;
    }

    bool VisibleInterval(double start, double end) =>
        double.IsFinite(start) && double.IsFinite(end) && end >= start && start < Time.Max &&
        (end > Time.Min || start >= Time.Min);

    static bool ValidSpacing(float value) => float.IsFinite(value) && value >= 0;
}
