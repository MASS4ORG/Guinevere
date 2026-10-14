namespace Guinevere;

public partial class Gui
{
    /// <summary>This frame's node transforms, kept off the nodes so untransformed nodes carry no matrix.</summary>
    readonly Dictionary<LayoutNode, Matrix3x2> _transforms = new(ReferenceEqualityComparer.Instance);

    /// <summary>Whether any node has been given a transform, enabling the transformed render and hit-test paths.</summary>
    internal bool TransformsUsed { get; private set; }

    internal Matrix3x2? LocalTransformOf(LayoutNode node) =>
        TransformsUsed && _transforms.TryGetValue(node, out var transform) ? transform : null;

    /// <summary>Forgets the previous tree's transforms once its events have been dispatched.</summary>
    void ClearTransforms() => _transforms.Clear();

    /// <summary>
    /// Transforms how a node and its subtree are drawn and hit-tested, without changing layout. The matrix works in
    /// screen coordinates, so pivots are absolute points such as <c>node.Rect.Center</c>; a descendant's transform is
    /// applied before its ancestors'. Pointer positions, event coordinates and drag deltas stay in screen space; use
    /// <see cref="LayoutNode.ScreenToLocal"/> for layout coordinates. Call from the same site in both passes.
    /// </summary>
    /// <param name="transform">The affine transform; identity clears it.</param>
    /// <param name="scope">The node to transform; defaults to the current node.</param>
    public void SetTransform(Matrix3x2 transform, LayoutNodeScope? scope = null)
    {
        var node = (scope ?? CurrentNodeScope).Node;
        if (transform.IsIdentity)
        {
            _transforms.Remove(node);
            return;
        }
        _transforms[node] = transform;
        TransformsUsed = true;
    }

    /// <summary>
    /// The pointer in the current node's layout coordinates, for turning a position into a value such as a slider's.
    /// Under a transform that cannot be inverted it is the screen position, so derived values stay finite.
    /// </summary>
    public Vector2 LocalMousePosition => LocalPointer(CurrentNode);

    /// <summary>Whether the pointer is inside <paramref name="rect"/>, given in the current node's layout coordinates.</summary>
    /// <param name="rect">The area, as laid out; it is tested where it is drawn.</param>
    public bool IsPointerIn(Rect rect) => rect.Contains(CurrentNode.ScreenToLocal(Input.MousePosition));

    /// <summary>The pointer in <paramref name="node"/>'s layout coordinates, or the screen position when singular.</summary>
    internal Vector2 LocalPointer(LayoutNode node)
    {
        var screen = Input.MousePosition;
        var local = node.ScreenToLocal(screen);
        return float.IsNaN(local.X) ? screen : local;
    }

    /// <summary>
    /// Re-applies every ancestor's transform and, below any ancestor that escapes clips, its clip rect, then the node's
    /// own transform, so clips follow the transformed ancestors that set them.
    /// </summary>
    void ApplyTransformedClips(LayoutNode node, SKCanvas canvas)
    {
        _clipAncestors.Clear();
        for (var a = node.Parent; a is not null; a = a.Parent) _clipAncestors.Add(a);

        var lastClip = LastClippingAncestor(node);
        for (var i = _clipAncestors.Count - 1; i >= 0; i--)
        {
            var ancestor = _clipAncestors[i];
            ConcatTransform(ancestor, canvas);
            if (IsViewport(ancestor)) canvas.ClipRect(ViewportClipBounds(ancestor));
            else if (i <= lastClip) ApplyPanelClip(ancestor, canvas);
        }
        ConcatTransform(node, canvas);
        if (IsViewport(node)) canvas.ClipRect(ViewportClipBounds(node));
    }

    static void ApplyPanelClip(LayoutNode node, SKCanvas canvas)
    {
        if (IsClipped(node) && node.Rect is { W: > 0, H: > 0 } rect) canvas.ClipRect(rect);
    }

    /// <summary>
    /// The index in the ancestor list of the outermost ancestor whose clip applies: the nearest one that escapes
    /// clips, else the root; -1 when the node itself escapes them.
    /// </summary>
    int LastClippingAncestor(LayoutNode node)
    {
        if (EscapesClips(node)) return -1;
        for (var i = 0; i < _clipAncestors.Count; i++)
            if (EscapesClips(_clipAncestors[i])) return i;
        return _clipAncestors.Count - 1;
    }

    static void ConcatTransform(LayoutNode node, SKCanvas canvas)
    {
        if (node.LocalTransform is { } transform) canvas.Concat(ToSkia(transform));
    }

    /// <summary>The ink bounds of a node mapped to where it is drawn, as an axis-aligned box.</summary>
    static SKRect DrawnBounds(LayoutNode node, SKRect bounds) =>
        node.WorldTransform is { IsIdentity: false } world ? ToSkia(world).MapRect(bounds) : bounds;

    static bool EscapesClips(LayoutNode node) =>
        node.Scope.HasLocal<LayoutNodeScopeEscapesAncestorClips>()
        && node.Scope.Get<LayoutNodeScopeEscapesAncestorClips>().Value;

    static bool IsClipped(LayoutNode node) =>
        node.Scope.HasLocal<LayoutNodeScopeIsClipped>() && node.Scope.Get<LayoutNodeScopeIsClipped>().Value;

    static SKMatrix ToSkia(Matrix3x2 m) => new(m.M11, m.M21, m.M31, m.M12, m.M22, m.M32, 0f, 0f, 1f);
}
