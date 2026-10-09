namespace Guinevere;

public partial class LayoutNode
{
    /// <summary>The affine transform set on this node, or <c>null</c> for identity.</summary>
    internal Matrix3x2? LocalTransform => _gui.LocalTransformOf(this);

    /// <summary>The transform set on this node with <see cref="Gui.SetTransform"/>; identity when none.</summary>
    public Matrix3x2 Transform => LocalTransform ?? Matrix3x2.Identity;

    /// <summary>
    /// This node's transform composed with its ancestors': maps the node's layout coordinates to where it is drawn.
    /// </summary>
    public Matrix3x2 WorldTransform
    {
        get
        {
            var world = Matrix3x2.Identity;
            if (!_gui.TransformsUsed) return world;
            for (var node = this; node is not null; node = node._parent)
                if (node.LocalTransform is { } local) world *= local;
            return world;
        }
    }

    /// <summary>
    /// Maps a screen point, such as the pointer, into this node's layout coordinates. Returns NaN components when a
    /// transform on the way collapses the node to a line or point, so nothing contains it.
    /// </summary>
    /// <param name="screen">The point in screen coordinates.</param>
    public Vector2 ScreenToLocal(Vector2 screen)
    {
        if (!_gui.TransformsUsed) return screen;
        var world = WorldTransform;
        if (world.IsIdentity) return screen;
        return Matrix3x2.Invert(world, out var inverse) ? Vector2.Transform(screen, inverse) : new Vector2(float.NaN);
    }

    /// <summary>Maps a point in this node's layout coordinates to where it is drawn on screen.</summary>
    /// <param name="local">The point in layout coordinates.</param>
    public Vector2 LocalToScreen(Vector2 local) =>
        _gui.TransformsUsed ? Vector2.Transform(local, WorldTransform) : local;

    /// <summary>
    /// The screen-space bounds of a rect in this node's layout coordinates: exact under translation and scale, the
    /// enclosing box under rotation or skew. For areas recorded now and tested against the pointer later.
    /// </summary>
    /// <param name="local">The rect in layout coordinates.</param>
    public Rect LocalToScreen(Rect local)
    {
        if (!_gui.TransformsUsed) return local;
        var world = WorldTransform;
        if (world.IsIdentity) return local;
        var a = Vector2.Transform(new Vector2(local.X, local.Y), world);
        var b = Vector2.Transform(new Vector2(local.X + local.W, local.Y), world);
        var c = Vector2.Transform(new Vector2(local.X, local.Y + local.H), world);
        var d = Vector2.Transform(new Vector2(local.X + local.W, local.Y + local.H), world);
        var min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
        var max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
        return new Rect(min.X, min.Y, max.X - min.X, max.Y - min.Y);
    }

    /// <summary>Whether the screen point lands inside this node's rect as drawn.</summary>
    internal bool ContainsScreenPoint(Vector2 screen) => Rect.Contains(ScreenToLocal(screen));
}
