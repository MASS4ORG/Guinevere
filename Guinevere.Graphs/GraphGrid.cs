namespace Guinevere.Graphs;

/// <summary>One viewport-local adaptive grid line.</summary>
public readonly record struct GraphGridLine(Vector2 Start, Vector2 End, bool Major, bool Axis);

/// <summary>Generates bounded grid geometry without accumulating floating-point world positions.</summary>
public static class GraphGrid
{
    /// <summary>Produces minor and major lines at every zoom, with optional distinct origin axes.</summary>
    public static IEnumerable<GraphGridLine> Lines(GraphCamera camera, Vector2 viewportSize, bool showAxes = true,
        double minimumSpacing = 24, int majorEvery = 4)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumSpacing);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(majorEvery);
        GraphBounds.RequireFinite(minimumSpacing);
        var bounds = camera.VisibleBounds(viewportSize);
        var step = Math.Pow(2, Math.Ceiling(Math.Log2(minimumSpacing / camera.Zoom)));
        foreach (var line in AxisLines(bounds.X, bounds.Width, step, camera.Zoom, viewportSize, true, showAxes, majorEvery))
        {
            yield return line;
        }
        foreach (var line in AxisLines(bounds.Y, bounds.Height, step, camera.Zoom, viewportSize, false, showAxes, majorEvery))
        {
            yield return line;
        }
    }

    static IEnumerable<GraphGridLine> AxisLines(double origin, double extent, double step, double zoom,
        Vector2 size, bool vertical, bool showAxes, int majorEvery)
    {
        var first = Math.Ceiling(origin / step);
        var count = Math.Min(4096, (int)Math.Min(4096, Math.Ceiling(extent / step) + 1));
        for (var i = 0; i < count; i++)
        {
            var index = first + i;
            var position = (float)((index * step - origin) * zoom);
            var start = vertical ? new Vector2(position, 0) : new Vector2(0, position);
            var end = vertical ? new Vector2(position, size.Y) : new Vector2(size.X, position);
            yield return new GraphGridLine(start, end, index % majorEvery == 0, showAxes && index == 0);
        }
    }
}
