namespace Guinevere.Graphs;

/// <summary>A view-only camera using logical screen units; display density is applied by the host viewport.</summary>
public sealed class GraphCamera
{
    /// <summary>Creates a camera with fixed positive zoom limits.</summary>
    public GraphCamera(double minimumZoom = 0.01, double maximumZoom = 16)
    {
        GraphBounds.RequireFinite(minimumZoom, maximumZoom);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(minimumZoom, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumZoom, minimumZoom);
        MinimumZoom = minimumZoom;
        MaximumZoom = maximumZoom;
        Zoom = Math.Clamp(1, minimumZoom, maximumZoom);
    }

    /// <summary>The graph point at the top-left of the viewport.</summary>
    public GraphPoint Origin { get; private set; }
    /// <summary>Logical screen units per graph unit.</summary>
    public double Zoom { get; private set; }
    /// <summary>The smallest permitted zoom.</summary>
    public double MinimumZoom { get; }
    /// <summary>The largest permitted zoom.</summary>
    public double MaximumZoom { get; }

    /// <summary>Maps graph coordinates to viewport-local logical screen coordinates.</summary>
    public Vector2 WorldToView(GraphPoint point) =>
        new((float)((point.X - Origin.X) * Zoom), (float)((point.Y - Origin.Y) * Zoom));

    /// <summary>Maps viewport-local logical screen coordinates to graph coordinates.</summary>
    public GraphPoint ViewToWorld(Vector2 point) => new(Origin.X + point.X / Zoom, Origin.Y + point.Y / Zoom);

    /// <summary>Returns visible graph bounds for a logical viewport size.</summary>
    public GraphBounds VisibleBounds(Vector2 size)
    {
        GraphBounds.RequireFinite(size.X, size.Y);
        return new GraphBounds(Origin.X, Origin.Y, Math.Max(0, size.X) / Zoom, Math.Max(0, size.Y) / Zoom);
    }

    /// <summary>Moves the drawn graph by a logical screen delta.</summary>
    public void Pan(Vector2 delta) =>
        SetView(new GraphPoint(Origin.X - delta.X / Zoom, Origin.Y - delta.Y / Zoom), Zoom);

    /// <summary>Sets the camera without applying any semantic graph edit.</summary>
    public void SetView(GraphPoint origin, double zoom)
    {
        GraphBounds.RequireFinite(origin.X, origin.Y, zoom);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(zoom, 0);
        Origin = origin;
        Zoom = Math.Clamp(zoom, MinimumZoom, MaximumZoom);
    }

    /// <summary>Changes zoom while preserving the graph point under a viewport-local cursor.</summary>
    public void ZoomAt(Vector2 anchor, double factor)
    {
        GraphBounds.RequireFinite(anchor.X, anchor.Y, factor);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(factor, 0);
        var point = ViewToWorld(anchor);
        var zoom = Math.Clamp(Zoom * factor, MinimumZoom, MaximumZoom);
        SetView(new GraphPoint(point.X - anchor.X / zoom, point.Y - anchor.Y / zoom), zoom);
    }

    /// <summary>Centers a world point without changing zoom.</summary>
    public void CenterOn(GraphPoint point, Vector2 viewportSize) =>
        SetView(new GraphPoint(point.X - viewportSize.X / (2 * Zoom),
            point.Y - viewportSize.Y / (2 * Zoom)), Zoom);

    /// <summary>Frames world bounds with logical padding; an unlaid-out viewport leaves the camera unchanged.</summary>
    public void Fit(GraphBounds bounds, Vector2 viewportSize, double padding = 32)
    {
        GraphBounds.RequireFinite(viewportSize.X, viewportSize.Y, padding);
        ArgumentOutOfRangeException.ThrowIfNegative(padding);
        var width = viewportSize.X - 2 * padding;
        var height = viewportSize.Y - 2 * padding;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var zoom = Math.Min(width / Math.Max(bounds.Width, 1), height / Math.Max(bounds.Height, 1));
        SetView(Origin, Math.Clamp(zoom, MinimumZoom, MaximumZoom));
        CenterOn(bounds.Center, viewportSize);
    }

    /// <summary>Restores the graph origin and a zoom of one, clamped to the configured limits.</summary>
    public void Reset() => SetView(default, 1);

    internal GraphCamera Snapshot()
    {
        var result = new GraphCamera(MinimumZoom, MaximumZoom);
        result.SetView(Origin, Zoom);
        return result;
    }
}
