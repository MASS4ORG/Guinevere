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

    internal bool GetDragState(string id)
    {
        return _dragStates.TryGetValue(id, out var state) && state;
    }

    internal bool SetDragState(string id, bool state)
    {
        return state ? _dragStates.TryAdd(id, true) : _dragStates.Remove(id);
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
}
