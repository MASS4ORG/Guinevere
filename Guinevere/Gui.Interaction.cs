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
    public IInputHandler Input { get; set; } = null!;

    private readonly Dictionary<string, bool> _dragStates = new();
    private readonly Dictionary<string, Vector2> _pressAnchors = new();
    private Vector2 _pointerLastFrame;
    private bool _hasPointerLastFrame;
    private (string Id, MouseButton Button)? _pointerCapture;
    private readonly Dictionary<string, (float Time, int Count)> _clickRuns = new();
    private LayoutNode? _inputBlocker;

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
        var newShape = shape.Copy();
        newShape.Path.Transform(SKMatrix.CreateTranslation(position.X, position.Y));
        return new InteractableElement(newShape, this, ShapeId(position), CurrentNode);
    }

    private string ShapeId(Vector2 position)
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
        var now = Time.Elapsed;
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
    private void TrackPointerForNextFrame()
    {
        if (Input is null) return;

        _pointerLastFrame = Input.MousePosition;
        _hasPointerLastFrame = true;
    }

    internal Vector2 GetPressAnchor(string id)
    {
        return _pressAnchors.TryGetValue(id, out var anchor) ? anchor : Input.MousePosition;
    }

    internal void SetPressAnchor(string id, Vector2 position)
    {
        _pressAnchors[id] = position;
    }

    /// <summary>
    /// Finds the top-most node that has opted into blocking input and currently contains the cursor.
    /// Run once per frame after layout, since it needs resolved rects; z-index decides overlap, and
    /// equal z falls back to tree order so a later sibling wins.
    /// </summary>
    internal void UpdateInputBlocker()
    {
        _inputBlocker = null;
        if (RootNode is null || Input is null) return;

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

    private void ClearCompletedDrags()
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
    }

    /// <summary>
    /// Ends a capture once its button is up, whether or not the element that took it is still being
    /// drawn. A dock tab dropped into another group comes back under a different node id and would
    /// otherwise never run its own release, leaving the pointer captured for good.
    /// </summary>
    private void ReleaseFinishedCapture()
    {
        if (_pointerCapture is not { } held) return;
        if (Input is not null && Input.IsMouseButtonDown(held.Button)) return;

        _dragStates.Remove(held.Id);
        _pressAnchors.Remove(held.Id);
        _pointerCapture = null;
    }
}
