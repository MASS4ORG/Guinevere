namespace Guinevere;

/// <summary>
/// Reuse of immediate-mode nodes across frames. The build pass revisits a parent's previous children in the order they
/// were built and resets a match in place instead of allocating; children it does not revisit are dropped and never
/// reused for another identity. A reference kept past its frame therefore observes the same element rebuilt in a later
/// frame, or, once the element is gone, a detached node frozen in its last state.
/// </summary>
public partial class LayoutNode
{
    /// <summary>This node's children from the previous frame, awaiting reuse during the current build pass.</summary>
    List<LayoutNode>? _previousChildren;

    /// <summary>The position of the next previous child the build pass expects to revisit.</summary>
    int _reuseCursor;

    /// <summary>Identity index of the previous children, built when the build pass diverges from their order.</summary>
    Dictionary<int, int>? _reuseIndex;

    /// <summary>Whether <see cref="_reuseIndex"/> describes the current previous children.</summary>
    bool _reuseIndexValid;

    /// <summary>
    /// Returns this node to the state of a newly constructed one with the given size, keeping its buffers. Its current
    /// children become the previous children its own build revisits.
    /// </summary>
    internal void ResetForBuild(float? width, float? height)
    {
        RetainChildren();
        Style = LayoutStyle.Default;
        if (width.HasValue) Style.Width = width.Value;
        if (height.HasValue) Style.Height = height.Value;
        StyleTarget = null;
        ResetLayoutState();
        Pass2NodeCount = 0;
        DrawList.Clear();
        ResetInteraction();
        Scope.ResetForBuild();
        if (width == 0) ExpandWidth();
        if (height == 0) ExpandHeight();
    }

    /// <summary>Starts a root frame: children become reusable, frame-local state is cleared; the root scope persists.</summary>
    internal void BeginRootFrame()
    {
        RetainChildren();
        MatchCursor = 0;
        DrawList.Clear();
        ResetInteraction();
        InvalidateLayout();
    }

    /// <summary>Moves the children into the previous-children buffer and empties the child list.</summary>
    void RetainChildren()
    {
        if (ChildNodes.Count > 0 || _previousChildren is { Count: > 0 })
        {
            _previousChildren ??= new List<LayoutNode>(ChildNodes.Count);
            _previousChildren.Clear();
            _previousChildren.AddRange(ChildNodes);
            ChildNodes.Clear();
        }
        _reuseCursor = 0;
        _reuseIndexValid = false;
        _absoluteChildCount = 0;
        _flowChildrenCache = null;
        MatchCursor = 0;
    }

    void ResetLayoutState()
    {
        _rect = Rect.Zero;
        _layoutDirty = true;
        _hasLayout = false;
        _lastLayoutScreenRect = default;
        _intrinsicWidthValid = false;
        _intrinsicHeightValid = false;
        _intrinsicContentWidth = 0f;
        _intrinsicContentHeight = 0f;
        _ancestorScrollOffset = Vector2.Zero;
        _resolvedWidthForHeightPass = 0f;
    }

    /// <summary>
    /// Takes the previous child with <paramref name="identity"/>, or null when there is none. Children usually come
    /// back in the previous frame's order, so a cursor finds each in constant time; a divergent call resynchronizes
    /// through an identity index. A taken child is removed from the buffer, so it is reused at most once.
    /// </summary>
    internal LayoutNode? TakePreviousChild(int identity)
    {
        if (_previousChildren is not { Count: > 0 } previous) return null;
        var cursor = _reuseCursor;
        if (cursor < previous.Count && previous[cursor] is { } next && next.Identity == identity)
        {
            previous[cursor] = null!;
            _reuseCursor = cursor + 1;
            return next;
        }
        return TakeDivergedChild(previous, identity);
    }

    LayoutNode? TakeDivergedChild(List<LayoutNode> previous, int identity)
    {
        if (!_reuseIndexValid)
        {
            (_reuseIndex ??= new Dictionary<int, int>(previous.Count)).Clear();
            for (var i = 0; i < previous.Count; i++)
                if (previous[i] is { } child) _reuseIndex.TryAdd(child.Identity, i);
            _reuseIndexValid = true;
        }
        if (!_reuseIndex!.Remove(identity, out var position) || previous[position] is not { } match) return null;
        previous[position] = null!;
        _reuseCursor = position + 1;
        return match;
    }
}
