namespace Guinevere.Graphs;

/// <summary>An aspect-preserving mapping of graph content and camera bounds into a minimap rectangle.</summary>
public sealed class GraphMinimap
{
    readonly GraphBounds _world;
    readonly Rect _target;
    readonly double _scale;
    readonly Vector2 _offset;

    /// <summary>Creates a mapping which includes both content and the entire visible viewport.</summary>
    public GraphMinimap(GraphBounds content, GraphBounds visible, Rect target)
    {
        GraphBounds.RequireFinite(target.X, target.Y, target.W, target.H);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(target.W, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(target.H, 0);
        _world = content.Union(visible);
        _target = target;
        _scale = Math.Min(target.W / Math.Max(1, _world.Width), target.H / Math.Max(1, _world.Height));
        _offset = target.Center - new Vector2((float)(_world.Width * _scale / 2),
            (float)(_world.Height * _scale / 2));
        Viewport = Map(visible);
    }

    /// <summary>The projected camera viewport indicator.</summary>
    public Rect Viewport { get; }

    /// <summary>Projects a graph rectangle into the minimap.</summary>
    public Rect Map(GraphBounds bounds) => new(
        _offset.X + (float)((bounds.X - _world.X) * _scale),
        _offset.Y + (float)((bounds.Y - _world.Y) * _scale),
        (float)(bounds.Width * _scale), (float)(bounds.Height * _scale));

    /// <summary>Maps a minimap pointer to a graph point, clamping it to the minimap rectangle.</summary>
    public GraphPoint Unmap(Vector2 point) => new(
        _world.X + (Math.Clamp(point.X, _target.X, _target.X + _target.W) - _offset.X) / _scale,
        _world.Y + (Math.Clamp(point.Y, _target.Y, _target.Y + _target.H) - _offset.Y) / _scale);
}
