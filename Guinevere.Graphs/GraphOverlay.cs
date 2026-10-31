namespace Guinevere.Graphs;

/// <summary>
/// A visual layer above graph elements and below the minimap. The canvas calls it from the same site in both passes:
/// build the same nodes in both, queue primitives in the render pass, and freeze changing state in the build pass.
/// Overlays receive no input and read state already published to the UI thread rather than waiting for evaluation.
/// </summary>
public interface IGraphOverlay
{
    /// <summary>Draws inside a non-interactive node covering the canvas, for the frame's camera snapshot.</summary>
    void Draw(Gui gui, GraphOverlayContext context);
}

/// <summary>The frame's camera snapshot and mappings from world coordinates into the overlay node.</summary>
/// <param name="Canvas">The canvas being drawn.</param>
/// <param name="Pass">The current Guinevere pass.</param>
/// <param name="Origin">The overlay node's laid-out position; meaningful in the render pass.</param>
/// <param name="Zoom">The snapshot zoom, in logical units per world unit.</param>
/// <param name="VisibleBounds">World bounds visible in this frame, for culling.</param>
/// <param name="Detail">The level of detail for this zoom.</param>
public readonly record struct GraphOverlayContext(
    GraphCanvas Canvas,
    Pass Pass,
    Vector2 Origin,
    double Zoom,
    GraphBounds VisibleBounds,
    GraphLevelOfDetail Detail)
{
    /// <summary>Maps a world point to an offset within the overlay node, for absolutely placed child nodes.</summary>
    public Vector2 Offset(GraphPoint point) =>
        new((float)((point.X - VisibleBounds.X) * Zoom), (float)((point.Y - VisibleBounds.Y) * Zoom));

    /// <summary>Maps a world point into drawing coordinates for render-pass primitives.</summary>
    public Vector2 ToLocal(GraphPoint point) => Origin + Offset(point);
}
