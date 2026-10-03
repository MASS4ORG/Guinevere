namespace Guinevere;

public partial class Gui
{
    /// <summary>
    /// Provides access to the input handling system for the GUI, enabling interaction
    /// through keyboard and mouse events. This property represents a contract to
    /// handle input-related functionalities such as detecting key presses, mouse
    /// movements, and clipboard interactions.
    /// </summary>
    /// <remarks>
    /// The <see cref="IInputHandler"/> implementation supplies methods and properties
    /// for querying input states, including:
    /// - Keyboard events (e.g., key pressed, key down, key up).
    /// - Mouse events (e.g., mouse position, mouse button states, mouse wheel delta).
    /// - Retrieving or setting clipboard text.
    /// Useful for managing user inputs and enabling interactive elements within the GUI.
    /// </remarks>
    IInputHandler? _input;
    FrameInput? _frameInput;

    /// <summary>
    /// Required input capability. Assigning it also publishes input and clipboard services. Reading it returns
    /// this frame's view of that handler: edges an event listener handled with
    /// <see cref="GuiEvent.PreventDefault"/> are hidden from it, and typed text is handed out once per frame.
    /// </summary>
    public IInputHandler Input
    {
        get => FrameInputView;
        set
        {
            _input = value;
            Platform.Register<IInputHandler>(value);
            Platform.Register<IClipboard>(value);
        }
    }

    /// <summary>
    /// The handler the platform supplied, without this frame's filtering. Use it to reach integration-specific
    /// members; poll through <see cref="Input"/> so handled events are respected.
    /// </summary>
    public IInputHandler PlatformInput => RawInput;

    IInputHandler RawInput => _input ?? Platform.Require<IInputHandler>();

    FrameInput FrameInputView
    {
        get
        {
            var source = RawInput;
            if (_frameInput is null || !ReferenceEquals(_frameInput.Source, source))
                _frameInput = new FrameInput(source);
            return _frameInput;
        }
    }

    readonly Dictionary<string, bool> _dragStates = [];
    readonly Dictionary<string, PressAnchor> _pressAnchors = [];
    Vector2 _pointerLastFrame;
    bool _hasPointerLastFrame;
    (string Id, MouseButton Button)? _pointerCapture;
    readonly Dictionary<string, (float Time, int Count)> _clickRuns = [];
    LayoutNode? _inputBlocker;

    /// <summary>
    /// Retrieves an interactable element for the current layout node.
    /// </summary>
    /// <returns>An instance of <see cref="InteractableElement"/> that represents the interactable element associated with the current layout node.</returns>
    public InteractableElement GetInteractable()
    {
        return GetInteractable(CurrentNode);
    }

    /// <summary>
    /// Retrieves an interactable element for the current layout node.
    /// </summary>
    /// <returns>An instance of <see cref="InteractableElement"/> that represents the interactable element for the current node.</returns>
    public InteractableElement GetInteractable(LayoutNode node)
    {
        node.IsPointerInteractive = true;
        return new InteractableElement(node.Rect, this, node.Id, node);
    }

    /// <summary>
    /// Retrieves an interactable element based on the specified position and shape.
    /// </summary>
    /// <param name="position">The position where the interactable element should be located.</param>
    /// <param name="shape">The shape associated with the interactable element.</param>
    /// <returns>An instance of <see cref="InteractableElement"/> representing the interactable element at the specified position and shape.</returns>
    public InteractableElement GetInteractable(Vector2 position, Shape shape)
    {
        CurrentNode.IsPointerInteractive = true;
        var newShape = shape.Copy();
        newShape.Path.Transform(SKMatrix.CreateTranslation(position.X, position.Y));
        return new InteractableElement(newShape, this, ShapeId(position), CurrentNode);
    }

    string ShapeId(Vector2 position)
    {
        return $"{CurrentNode.Id}_{position.X}_{position.Y}";
    }

    /// <summary>
    /// The element currently holding the pointer, or null when nothing is. While a capture is held,
    /// no other element can start a drag — so dragging something across a splitter or another
    /// draggable does not hand the pointer over mid-gesture.
    /// </summary>
    public string? PointerCapture => _pointerCapture?.Id;

    /// <summary>
    /// Whether some element is currently holding the pointer. Controls use this to drop affordances
    /// that would mislead mid-drag, such as a splitter highlighting itself as grabbable.
    /// </summary>
    public bool IsPointerCaptured => _pointerCapture is not null;

    /// <summary>
    /// Claims the pointer for an element, if it is free or already theirs. Keyed by button so a
    /// right-drag cannot take over from a left-drag in progress.
    /// </summary>
    internal bool TryCapturePointer(string id, MouseButton button)
    {
        if (_pointerCapture is { } held) return held.Id == id && held.Button == button;

        _pointerCapture = (id, button);
        return true;
    }

    /// <summary>Whether this element currently holds the pointer for this button.</summary>
    internal bool HoldsPointer(string id, MouseButton button) =>
        _pointerCapture is { } held && held.Id == id && held.Button == button;

    /// <summary>Gives the pointer back, if this element is the one holding it.</summary>
    internal void ReleasePointer(string id)
    {
        if (_pointerCapture?.Id == id) _pointerCapture = null;
    }

    /// <summary>
    /// How long after a click a second one still counts as a double click, in seconds.
    /// </summary>
    public float DoubleClickInterval { get; set; } = 0.4f;

    /// <summary>
    /// Records a click and reports how many landed in a row on this element. Two means a double click.
    /// </summary>
    internal int RegisterClick(string id)
    {
        var now = Clock.Elapsed;
        var run = _clickRuns.TryGetValue(id, out var previous) && now - previous.Time <= DoubleClickInterval
            ? previous.Count + 1
            : 1;

        _clickRuns[id] = (now, run);
        return run;
    }

    internal bool GetDragState(string id)
    {
        return _dragStates.TryGetValue(id, out var state) && state;
    }

    internal bool SetDragState(string id, bool state)
    {
        return state ? _dragStates.TryAdd(id, true) : _dragStates.Remove(id);
    }

    /// <summary>
    /// How far the pointer moved since the previous frame. <see cref="IInputHandler.PrevMousePosition"/>
    /// cannot answer this: most integrations update it from the move event, so it keeps reporting the
    /// last movement once the pointer stops. Tracked per frame here, so it is the same everywhere.
    /// </summary>
    internal Vector2 PointerFrameDelta =>
        _hasPointerLastFrame ? Input.MousePosition - _pointerLastFrame : Vector2.Zero;

    /// <summary>Records the pointer for the next frame's delta. Called from <see cref="EndFrame"/>.</summary>
    void TrackPointerForNextFrame()
    {
        _pointerLastFrame = Input.MousePosition;
        _hasPointerLastFrame = true;
    }

    internal Vector2 GetPressAnchor(string id)
    {
        return _pressAnchors.TryGetValue(id, out var anchor) ? anchor.Origin : Input.MousePosition;
    }

    /// <summary>
    /// Whether a hold began on this element and has not been forgotten. The anchor is only dropped when
    /// the frame ends, so it is still there on the frame the button comes up — after the pointer has
    /// already been handed back.
    /// </summary>
    internal bool HasPressAnchor(string id) => _pressAnchors.ContainsKey(id);

    internal void SetPressAnchor(string id, Vector2 position, MouseButton button)
    {
        _pressAnchors[id] = new PressAnchor(position, button);
    }

    /// <summary>
    /// Shifts every press anchor, so a wrap does not read as the pointer having travelled: a gesture
    /// held across the edge is one that never moved.
    /// </summary>
    internal void ShiftPressAnchors(Vector2 delta)
    {
        foreach (var id in _pressAnchors.Keys.ToArray())
        {
            var press = _pressAnchors[id];
            press.Origin += delta;
            _pressAnchors[id] = press;
        }
    }

    /// <summary>
    /// How far the pointer has strayed from where the press landed, keeping the furthest it reached: a
    /// gesture that travelled and came back is still the drag it was on the way out.
    /// </summary>
    internal float NotePressTravel(string id)
    {
        if (!_pressAnchors.TryGetValue(id, out var press)) return 0f;

        var travel = (Input.MousePosition - press.Origin).Length();
        if (travel <= press.Travel) return press.Travel;

        press.Travel = travel;
        _pressAnchors[id] = press;
        return travel;
    }

    /// <summary>Where a hold began, which button began it, and how far the pointer has got since.</summary>
    struct PressAnchor
    {
        public PressAnchor(Vector2 origin, MouseButton button)
        {
            Origin = origin;
            Button = button;
        }

        public Vector2 Origin { get; set; }
        public MouseButton? Button { get; set; }
        public float Travel { get; set; }
    }

    /// <summary>
    /// Finds the top-most node that has opted into blocking input and currently contains the cursor.
    /// Run once per frame after layout, since it needs "resolved" rects; z-index decides overlap, and
    /// equal z falls back to tree order so a later sibling wins.
    /// </summary>
    void UpdateInputBlocker()
    {
        _inputBlocker = null;
        if (RootNode is null) return;

        var pos = Input.MousePosition;
        var bestZ = int.MinValue;
        Visit(RootNode);

        void Visit(LayoutNode node)
        {
            if (node.Style.BlocksInput && node.Rect.Contains(pos))
            {
                var z = node.Scope.Get<LayoutNodeScopeZIndex>().Value;
                if (z >= bestZ)
                {
                    bestZ = z;
                    _inputBlocker = node;
                }
            }

            foreach (var child in node.ChildNodes) Visit(child);
        }
    }

    /// <summary>
    /// Whether a blocking overlay is swallowing the pointer for this node. The blocker's own subtree
    /// stays interactive, so the check is an ancestor walk rather than a rect test.
    /// </summary>
    /// <summary>Whether the pointer is inside some element that blocks input.</summary>
    public bool IsPointerOverBlocker => _inputBlocker is not null;

    internal bool IsHoverBlocked(LayoutNode? node)
    {
        if (_inputBlocker is null) return false;

        for (var n = node; n is not null; n = n.Parent)
            if (ReferenceEquals(n, _inputBlocker))
                return false;

        return true;
    }

    /// <summary>
    /// Forgets gestures that are over, once the frame that finished them has been drawn. The press anchor
    /// has to outlive the release for <c>OnClickCompleted</c> to see it, and outlive the frame so that
    /// whichever element resolved the hold had its turn first — so it is dropped here, not by the
    /// element that gave the pointer back. Otherwise a leftover anchor reads as a fresh press and the
    /// next click is one frame late, counting as a double click against the one before it.
    /// </summary>
    void ClearCompletedDrags()
    {
        var keysToRemove = new List<string>();
        foreach (var kvp in _dragStates)
            if (!kvp.Value)
                keysToRemove.Add(kvp.Key);

        foreach (var key in keysToRemove)
        {
            _dragStates.Remove(key);
            _pressAnchors.Remove(key);
        }

        // An element that never began a drag leaves no drag state behind, so its anchor can only be
        // matched against the ones that are left.
        foreach (var key in _pressAnchors.Keys)
            if (!IsMouseButtonDownFor(key))
                keysToRemove.Add(key);

        foreach (var key in keysToRemove) _pressAnchors.Remove(key);
    }

    /// <summary>
    /// Whether a press anchor still belongs to a button that is down. A button that is up means the
    /// gesture it belongs to has already reported, however it was resolved.
    /// </summary>
    bool IsMouseButtonDownFor(string id)
    {
        return _pressAnchors[id].Button is { } button && Input.IsMouseButtonDown(button);
    }

    /// <summary>
    /// Ends a capture once its button is up, whether the element that took it is still being
    /// drawn. A dock tab dropped into another group comes back under a different node id and would
    /// otherwise never run its own release, leaving the pointer captured for good.
    /// </summary>
    void ReleaseFinishedCapture()
    {
        if (_pointerCapture is not { } held) return;
        if (Input.IsMouseButtonDown(held.Button)) return;

        _dragStates.Remove(held.Id);
        _pressAnchors.Remove(held.Id);
        _pointerCapture = null;
    }
}
