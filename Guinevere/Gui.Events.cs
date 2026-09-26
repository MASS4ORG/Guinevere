namespace Guinevere;

public partial class Gui
{
    static readonly KeyboardKey[] AllKeys = [.. Enum.GetValues<KeyboardKey>().Distinct()];
    static readonly MouseButton[] AllButtons = Enum.GetValues<MouseButton>();

    readonly List<LayoutNode> _eventPath = [];
    readonly HashSet<KeyboardKey> _keysDownLastFrame = [];
    readonly PointerGesture?[] _gestures = new PointerGesture?[AllButtons.Length];
    int _buttonsDownLastFrame;
    string? _lastFocusedId;
    int _listenerCount;
    int _cursorNodeCount;
    PointerCursor? _requestedCursor;
    PointerCursor? _appliedCursor;
    PointerMode? _requestedPointerMode;
    PointerMode _appliedPointerMode;

    /// <summary>How far, in pixels, the pointer must move with a button held before a drag starts.</summary>
    public float DragThreshold { get; set; } = 3f;

    /// <summary>
    /// Listens for <typeparamref name="TEvent"/> on the current node. See <see cref="LayoutNode.On{TEvent}"/>.
    /// </summary>
    public void On<TEvent>(Action<TEvent> handler, bool capture = false) where TEvent : GuiEvent =>
        CurrentNode.On(handler, capture);

    /// <summary>
    /// Shows <paramref name="cursor"/> for this frame, overriding the node cursors. Call it every frame the
    /// cursor applies, as with any immediate-mode state; the node cursors return once calls stop.
    /// </summary>
    public void RequestCursor(PointerCursor cursor) => _requestedCursor = cursor;

    /// <summary>
    /// Puts the pointer in <paramref name="mode"/> for this frame — typically while a scrub or camera drag holds
    /// the pointer. The pointer returns to <see cref="PointerMode.Normal"/> on the first frame nobody asks.
    /// Needs an <see cref="IPointerCapability"/>; without one the request is ignored.
    /// </summary>
    public void RequestPointerMode(PointerMode mode) => _requestedPointerMode = mode;

    /// <summary>The pointer mode applied at the end of the last frame.</summary>
    public PointerMode PointerMode => _appliedPointerMode;

    bool HasInput => _input is not null || Platform.Supports<IInputHandler>();

    internal void NoteEventListener() => _listenerCount++;
    internal void NoteCursorNode() => _cursorNodeCount++;

    /// <summary>
    /// Turns this frame's input edges into events and dispatches them against the previous frame's tree, which
    /// is still laid out and is what the user saw. Runs before the new tree is built, so listeners can change
    /// state without the two passes of this frame disagreeing.
    /// </summary>
    void DispatchInputEvents()
    {
        // Headless layout runs frames without input; there is nothing to dispatch.
        if (!HasInput) return;

        var input = FrameInputView;
        input.BeginFrame();
        var root = RootNode;
        if (root is null || _listenerCount == 0)
        {
            ForgetEventState();
            return;
        }

        var raw = input.Source;
        DispatchFocusChange(root);

        var position = raw.MousePosition;
        var hit = HitTest(root, position);
        var pointerTarget = _pointerCapture is { } held ? FindNode(root, held.Id) ?? hit : hit;

        var frameDelta = PointerFrameDelta;
        if (_hasPointerLastFrame && frameDelta != Vector2.Zero && pointerTarget is not null)
            Dispatch(new PointerMoveEvent { Position = position, Delta = frameDelta }, pointerTarget);

        foreach (var button in AllButtons)
            DispatchButton(input, raw, root, button, position, frameDelta, pointerTarget, hit);

        if (raw.MouseWheelDelta != 0f && hit is not null &&
            Dispatch(new ScrollEvent { Position = position, Delta = raw.MouseWheelDelta }, hit).DefaultPrevented)
            input.SuppressWheel();

        var focusTarget = Focus.CurrentFocusedId is { } focused ? FindNode(root, focused) ?? root : root;
        foreach (var key in AllKeys)
        {
            if (raw.IsKeyPressed(key) && Dispatch(new KeyDownEvent { Key = key }, focusTarget).DefaultPrevented)
                input.SuppressKeyPress(key);

            if (raw.IsKeyDown(key)) _keysDownLastFrame.Add(key);
            else if (_keysDownLastFrame.Remove(key)) Dispatch(new KeyUpEvent { Key = key }, focusTarget);
        }

        var text = input.PeekText();
        if (text.Length > 0 && Dispatch(new TextInputEvent { Text = text }, focusTarget).DefaultPrevented)
            input.SuppressText();
    }

    void DispatchButton(FrameInput input, IInputHandler raw, LayoutNode root, MouseButton button,
        Vector2 position, Vector2 frameDelta, LayoutNode? pointerTarget, LayoutNode? hit)
    {
        var bit = 1 << (int)button;
        var down = raw.IsMouseButtonDown(button);
        var wasDown = (_buttonsDownLastFrame & bit) != 0;
        _buttonsDownLastFrame = down ? _buttonsDownLastFrame | bit : _buttonsDownLastFrame & ~bit;
        ref var gesture = ref _gestures[(int)button];

        if (raw.IsMouseButtonPressed(button) && pointerTarget is not null)
        {
            if (Dispatch(new PointerDownEvent { Position = position, Button = button }, pointerTarget)
                .DefaultPrevented)
                input.SuppressPress(button);
            gesture = new PointerGesture(pointerTarget.Id, PathIds(pointerTarget), position);
            wasDown = true;
        }

        if (gesture is { } active && down)
        {
            var owner = FindNode(root, active.OwnerId);
            if (owner is null)
            {
                gesture = null;
                return;
            }

            // A drag only takes the pointer when something on the path listens for it; otherwise a press that
            // drifts past the threshold is still a click.
            if (!active.Dragging && Vector2.Distance(position, active.Origin) >= DragThreshold &&
                PathListens<DragStartEvent>(owner) && TryCapturePointer(active.OwnerId, button))
            {
                active.Dragging = true;
                Dispatch(DragArgsFor<DragStartEvent>(active, button, position, frameDelta), owner);
            }
            else if (active.Dragging && frameDelta != Vector2.Zero)
                Dispatch(DragArgsFor<DragMoveEvent>(active, button, position, frameDelta), owner);
            return;
        }

        if (!wasDown || down) return;

        var upTarget = pointerTarget ?? hit;
        if (upTarget is not null) Dispatch(new PointerUpEvent { Position = position, Button = button }, upTarget);
        if (gesture is not { } ended) return;

        gesture = null;
        if (ended.Dragging)
        {
            if (FindNode(root, ended.OwnerId) is { } owner)
                Dispatch(DragArgsFor<DragEndEvent>(ended, button, position, frameDelta), owner);
        }
        else if (hit is not null && SharedAncestor(ended.PathIds, hit) is { } clicked)
            Dispatch(new ClickEvent
            {
                Position = position,
                Button = button,
                ClickCount = RegisterClick($"event:{clicked.Id}:{button}")
            }, clicked);
    }

    static TEvent DragArgsFor<TEvent>(PointerGesture gesture, MouseButton button, Vector2 position,
        Vector2 frameDelta) where TEvent : DragEvent, new() =>
        new() { Button = button, Origin = gesture.Origin, Position = position, FrameDelta = frameDelta };

    void DispatchFocusChange(LayoutNode root)
    {
        var current = Focus.CurrentFocusedId;
        var previous = _lastFocusedId;
        if (current == previous) return;

        _lastFocusedId = current;
        if (previous is not null && FindNode(root, previous) is { } lost)
            Dispatch(new FocusOutEvent { RelatedTargetId = current }, lost);
        if (current is not null && FindNode(root, current) is { } gained)
            Dispatch(new FocusInEvent { RelatedTargetId = previous }, gained);
    }

    void ForgetEventState()
    {
        _keysDownLastFrame.Clear();
        Array.Clear(_gestures);
        _buttonsDownLastFrame = 0;
        _lastFocusedId = Focus.CurrentFocusedId;
    }

    /// <summary>Sends <paramref name="e"/> down the capture path, to the target, and back up the bubble path.</summary>
    TEvent Dispatch<TEvent>(TEvent e, LayoutNode target) where TEvent : GuiEvent
    {
        _eventPath.Clear();
        for (var node = target; node is not null; node = node.Parent) _eventPath.Add(node);
        e.TargetId = target.Id;

        for (var i = _eventPath.Count - 1; i > 0; i--)
        {
            _eventPath[i].Dispatch(e, EventPhase.Capture);
            if (e.PropagationStopped) return e;
        }

        target.Dispatch(e, EventPhase.Target);
        for (var i = 1; i < _eventPath.Count && !e.PropagationStopped; i++)
            _eventPath[i].Dispatch(e, EventPhase.Bubble);
        return e;
    }

    /// <summary>
    /// The node the pointer targets: the topmost hit-testable node containing <paramref name="position"/>, by
    /// z-index and then tree order — the order nodes are drawn in. A blocking overlay under the pointer limits
    /// the search to its own subtree, and clipping ancestors hide what they clip.
    /// </summary>
    internal LayoutNode? HitTest(LayoutNode root, Vector2 position)
    {
        LayoutNode? blocker = null;
        var blockerZ = int.MinValue;
        FindBlocker(root);

        LayoutNode? best = null;
        var bestZ = int.MinValue;
        Visit(blocker ?? root);
        return best;

        void FindBlocker(LayoutNode node)
        {
            if (node.Style.BlocksInput && node.Rect.Contains(position))
            {
                var z = node.Scope.Get<LayoutNodeScopeZIndex>().Value;
                if (z >= blockerZ) (blocker, blockerZ) = (node, z);
            }
            foreach (var child in node.ChildNodes) FindBlocker(child);
        }

        void Visit(LayoutNode node)
        {
            if (node.IsHitTestVisible && node.Rect.Contains(position) && !IsOutsideClippedAncestors(node, position))
            {
                var z = node.Scope.Get<LayoutNodeScopeZIndex>().Value;
                if (z >= bestZ) (best, bestZ) = (node, z);
            }
            foreach (var child in node.ChildNodes) Visit(child);
        }
    }

    /// <summary>
    /// Whether a clipping ancestor hides <paramref name="position"/> from <paramref name="node"/>. Mirrors
    /// <see cref="ApplyAncestorClips"/>: an ancestor that escapes clips stops the climb there.
    /// </summary>
    internal static bool IsOutsideClippedAncestors(LayoutNode node, Vector2 position)
    {
        if (node.Scope.HasLocal<LayoutNodeScopeEscapesAncestorClips>()
            && node.Scope.Get<LayoutNodeScopeEscapesAncestorClips>().Value)
            return false;

        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor.Scope.HasLocal<LayoutNodeScopeIsClipped>()
                && ancestor.Scope.Get<LayoutNodeScopeIsClipped>().Value)
            {
                var rect = ancestor.Rect;
                if (rect is { W: > 0, H: > 0 } && !rect.Contains(position)) return true;
            }

            if (ancestor.Scope.HasLocal<LayoutNodeScopeEscapesAncestorClips>()
                && ancestor.Scope.Get<LayoutNodeScopeEscapesAncestorClips>().Value)
                break;
        }

        return false;
    }

    static bool PathListens<TEvent>(LayoutNode target) where TEvent : GuiEvent
    {
        for (var node = target; node is not null; node = node.Parent)
            if (node.Listens<TEvent>())
                return true;
        return false;
    }

    static LayoutNode? FindNode(LayoutNode node, string id)
    {
        if (node.Id == id) return node;
        foreach (var child in node.ChildNodes)
            if (FindNode(child, id) is { } found)
                return found;
        return null;
    }

    static List<string> PathIds(LayoutNode target)
    {
        var ids = new List<string>();
        for (var node = target; node is not null; node = node.Parent) ids.Add(node.Id);
        ids.Reverse();
        return ids;
    }

    /// <summary>The deepest node on both the press path and <paramref name="releaseTarget"/>'s path.</summary>
    static LayoutNode? SharedAncestor(List<string> pressPath, LayoutNode releaseTarget)
    {
        var depth = 0;
        for (var node = releaseTarget.Parent; node is not null; node = node.Parent) depth++;

        for (var node = releaseTarget; node is not null; node = node.Parent, depth--)
            if (depth < pressPath.Count && pressPath[depth] == node.Id)
                return node;
        return null;
    }

    /// <summary>
    /// Applies the cursor shape and pointer mode for the frame that just rendered, then clears the requests.
    /// The shape comes from a request, else the node holding the pointer, else the topmost node under it,
    /// walking up to the nearest node that set one.
    /// </summary>
    void ApplyCursorAndPointerMode()
    {
        if (Platform.TryGet<ICursorCapability>(out var cursorCapability) && cursorCapability is not null)
        {
            var cursor = _requestedCursor ?? ResolveNodeCursor();
            if (cursor != _appliedCursor)
            {
                cursorCapability.Cursor = cursor;
                _appliedCursor = cursor;
            }
        }

        var mode = _requestedPointerMode ?? PointerMode.Normal;
        if (Platform.TryGet<IPointerCapability>(out var pointer) && pointer is not null)
        {
            if (mode != _appliedPointerMode)
            {
                pointer.Locked = mode == PointerMode.Relative;
                pointer.Visible = mode is PointerMode.Normal or PointerMode.Wrapped;
                _appliedPointerMode = mode;
            }

            if (mode == PointerMode.Wrapped) WrapPointer(pointer);
        }

        _requestedCursor = null;
        _requestedPointerMode = null;
    }

    PointerCursor ResolveNodeCursor()
    {
        if (_cursorNodeCount == 0 || RootNode is null || !HasInput) return PointerCursor.Default;

        var node = _pointerCapture is { } held
            ? FindNode(RootNode, held.Id)
            : HitTest(RootNode, RawInput.MousePosition);
        for (; node is not null; node = node.Parent)
            if (node.CursorShape is { } shape)
                return shape;
        return PointerCursor.Default;
    }

    /// <summary>
    /// Warps a pointer that reached a window edge to the opposite one, moving every gesture anchor by the same
    /// jump so drag totals continue as if the pointer had kept going.
    /// </summary>
    void WrapPointer(IPointerCapability pointer)
    {
        var size = Platform.TryGet<IDisplayCapability>(out var display) && display is not null
            ? display.LogicalSize
            : new Vector2(ScreenRect.W, ScreenRect.H);
        if (size.X < 4f || size.Y < 4f) return;

        if (!HasInput) return;
        var position = RawInput.MousePosition;
        var wrapped = new Vector2(Wrap(position.X, size.X), Wrap(position.Y, size.Y));
        if (wrapped == position) return;

        pointer.Warp(wrapped);
        var jump = wrapped - position;
        foreach (var id in _pressAnchors.Keys.ToArray()) _pressAnchors[id] += jump;
        for (var i = 0; i < _gestures.Length; i++)
            if (_gestures[i] is { } gesture)
                gesture.Origin += jump;

        static float Wrap(float value, float extent) =>
            value <= 0f ? extent - 2f : value >= extent - 1f ? 1f : value;
    }

    /// <summary>One button's press-to-release gesture, owned by the node that received the press.</summary>
    sealed class PointerGesture(string ownerId, List<string> pathIds, Vector2 origin)
    {
        public string OwnerId { get; } = ownerId;
        public List<string> PathIds { get; } = pathIds;
        public Vector2 Origin { get; set; } = origin;
        public bool Dragging { get; set; }
    }
}
