namespace Guinevere;

public partial class Gui
{
    private DragSession? _drag;
    private DragSession? _pendingDrag;
    private PendingDrop? _pendingDrop;

    /// <summary>
    /// The payload of the drag currently in progress, or null when nothing is being dragged.
    /// </summary>
    public object? CurrentDragPayload => _drag?.Payload;

    /// <summary>
    /// Whether a drag is in progress.
    /// </summary>
    public bool IsDragging => _drag is not null;

    /// <summary>
    /// Starts a drag carrying <paramref name="payload"/>, unless one is already running. Prefer
    /// <see cref="DragSource"/>, which applies the movement threshold that separates a drag from a
    /// click; this overload is for callers that decide on their own when a drag begins.
    /// </summary>
    /// <param name="sourceId">Identifies the element the drag started from.</param>
    /// <param name="payload">The value handed to drop targets.</param>
    /// <param name="ghost">Optional content drawn under the cursor by <see cref="DragGhost"/>.</param>
    /// <returns>True if a drag from <paramref name="sourceId"/> is now in progress.</returns>
    public bool BeginDrag(string sourceId, object payload, Action<Gui>? ghost = null)
    {
        if (_drag is not null) return _drag.SourceId == sourceId;

        // A drag is detected mid-frame, in the render pass, but goes live at the frame boundary:
        // both passes of a frame must agree on whether a drag is running or the ghost and the drop
        // zones would exist in one pass and not the other.
        _pendingDrag ??= new DragSession(sourceId, payload, ghost);
        return _pendingDrag.SourceId == sourceId;
    }

    /// <summary>
    /// Turns the current node into a drag source: once the pointer has been held and moved more than
    /// <paramref name="threshold"/> pixels from where it went down, a drag carrying
    /// <paramref name="payload"/> begins.
    /// </summary>
    /// <param name="sourceId">Identifies this source; reuse the node id when there is one.</param>
    /// <param name="payload">The value handed to drop targets.</param>
    /// <param name="ghost">Optional content drawn under the cursor by <see cref="DragGhost"/>.</param>
    /// <param name="threshold">Pointer travel, in pixels, before the hold counts as a drag.</param>
    /// <returns>True while this source owns the drag in progress.</returns>
    public bool DragSource(string sourceId, object payload, Action<Gui>? ghost = null, float threshold = 4f)
    {
        if (Pass != Pass.Pass2Render) return false;
        if (_drag is not null) return _drag.SourceId == sourceId;

        if (!GetInteractable().OnDrag(out var args)) return false;
        if (args.TotalDelta.Length() < threshold) return false;

        return BeginDrag(sourceId, payload, ghost);
    }

    /// <summary>
    /// Marks the current node as a drop target for the drag in progress.
    /// </summary>
    /// <param name="id">Identifies this target.</param>
    /// <param name="accept">Decides whether the payload is welcome here. Null accepts anything.</param>
    /// <param name="onDrop">Invoked with the payload when the pointer is released over this target.</param>
    /// <returns>True while the drag is hovering this target and the payload is accepted.</returns>
    public bool DropTarget(string id, Func<object, bool>? accept = null, Action<object>? onDrop = null)
    {
        return DropTarget(CurrentNode.Rect, id, accept, onDrop);
    }

    /// <summary>
    /// Marks an explicit rect as a drop target for the drag in progress. Overlapping targets are
    /// resolved by declaration order — the last one to report a hover wins, which puts nested and
    /// later-drawn targets on top.
    /// </summary>
    /// <param name="rect">The screen-space region that accepts the drop.</param>
    /// <param name="id">Identifies this target.</param>
    /// <param name="accept">Decides whether the payload is welcome here. Null accepts anything.</param>
    /// <param name="onDrop">Invoked with the payload when the pointer is released over this target.</param>
    /// <returns>True while the drag is hovering this target and the payload is accepted.</returns>
    public bool DropTarget(Rect rect, string id, Func<object, bool>? accept = null, Action<object>? onDrop = null)
    {
        if (Pass != Pass.Pass2Render || _drag is null) return false;
        if (accept is not null && !accept(_drag.Payload)) return false;
        if (!rect.Contains(Input.MousePosition)) return false;

        if (onDrop is not null && !Input.IsMouseButtonDown(MouseButton.Left))
            _pendingDrop = new PendingDrop(id, onDrop);

        return true;
    }

    /// <summary>
    /// Draws the active drag's ghost under the cursor. Call it at the top level of the frame, after
    /// the rest of the UI: the ghost is an absolutely positioned node raised above everything else,
    /// and a clipping ancestor would cut it off.
    /// </summary>
    public void DragGhost()
    {
        if (_drag?.Ghost is null) return;

        var pos = Input.MousePosition + new Vector2(12, 12);
        using (Node(-1, -1, $"__dragGhost_{_drag.SourceId}").AbsoluteScreen(pos.X, pos.Y).BlockInput().Enter())
        {
            SetZIndex(DragGhostZIndex);
            _drag.Ghost(this);
        }
    }

    /// <summary>
    /// Abandons the drag in progress without notifying any drop target.
    /// </summary>
    public void CancelDrag()
    {
        _drag = null;
        _pendingDrag = null;
        _pendingDrop = null;
    }

    /// <summary>
    /// Settles the drag once the pointer is released: the last target that reported a hover gets the
    /// payload, and a release over nothing simply cancels. Called from <see cref="EndFrame"/>.
    /// </summary>
    private void ResolveDrag()
    {
        if (_drag is null)
        {
            _drag = _pendingDrag;
            _pendingDrag = null;
            return;
        }
        if (Input.IsMouseButtonDown(MouseButton.Left) && !Input.IsKeyPressed(KeyboardKey.Escape)) return;

        var payload = _drag.Payload;
        var drop = _pendingDrop;
        _drag = null;
        _pendingDrop = null;

        if (!Input.IsKeyPressed(KeyboardKey.Escape)) drop?.OnDrop(payload);
    }

    /// <summary>
    /// The z-index the drag ghost is drawn at, above any reasonable application layer.
    /// </summary>
    public const int DragGhostZIndex = 10_000;

    private sealed record DragSession(string SourceId, object Payload, Action<Gui>? Ghost);

    private sealed record PendingDrop(string Id, Action<object> OnDrop);
}
