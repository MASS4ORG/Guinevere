namespace Guinevere;

/// <summary>
/// Represents an interactive UI element within the graphical user interface.
/// This structure encapsulates information necessary to enable interactivity for a specific
/// visual region or shape, including rectangle or custom-defined shapes, and associates it
/// with a graphical user interface context to handle various user interactions.
/// </summary>
public readonly struct InteractableElement
{
    private readonly SKPath _shape;
    private readonly Gui _gui;
    private readonly string _id;
    private readonly LayoutNode? _node;

    /// <summary>
    /// Represents an interactive UI element in the graphical user interface,
    /// defining a specific shape or region for user interaction and associating it
    /// with a GUI context for handling input events such as clicks, hovers, and holds.
    /// </summary>
    public InteractableElement(Rect rect, Gui gui, string? id, LayoutNode? node = null)
    {
        var builder = new SKPathBuilder();
        builder.AddRect(rect);
        _shape = builder.Detach();
        _gui = gui;
        _id = id ?? $"rect_{rect.X}_{rect.Y}_{rect.W}_{rect.H}";
        _node = node;
    }

    /// <summary>
    /// Represents an interactive element within the graphical user interface (GUI),
    /// enabling interaction such as clicks, hovers, and holds on specific visual regions defined by shapes.
    /// </summary>
    public InteractableElement(Shape path, Gui gui, string? id, LayoutNode? node = null)
    {
        _shape = path.Path;
        _gui = gui;
        _id = id ?? $"shape_{path.Path.GetHashCode()}";
        _node = node;
    }

    /// <summary>
    /// Determines whether the specified interaction type is occurring on the element.
    /// </summary>
    /// <param name="mask">The type of interaction(s) to check for, specified as a combination of one or more <see cref="Interactions"/> flags.</param>
    /// <returns>
    /// True if the specified interaction(s) are occurring on the element; otherwise, false.
    /// </returns>
    public bool On(Interactions mask)
    {
        var pos = _gui.Input.MousePosition;

        if (!_shape.Contains(pos.X, pos.Y))
            return false;

        if (mask.HasFlag(Interactions.Hover) && OnHover())
            return true;

        if (mask.HasFlag(Interactions.Hold) && IsHeld())
            return true;

        return mask.HasFlag(Interactions.Click) && OnClick();
    }

    /// <summary>
    /// Determines if the mouse cursor is currently hovering over the interactive
    /// element's defined shape or boundary within the associated GUI context.
    /// </summary>
    /// <returns>
    /// True if the cursor is within the interactive region of the element; otherwise, false.
    /// </returns>
    public bool OnHover()
    {
        if (_gui.IsHoverBlocked(_node)) return false;

        return _shape.Contains(_gui.Input.MousePosition.X, _gui.Input.MousePosition.Y);
    }

    /// <summary>
    /// Determines whether the specified mouse button is currently being held down
    /// on the interactive element.
    /// </summary>
    /// <param name="button">The mouse button to check, defaulting to the left button if not specified.</param>
    /// <returns>True if the specified mouse button is being held down; otherwise, false.</returns>
    public bool OnHold(MouseButton button = MouseButton.Left)
    {
        return IsHeld(button);
    }

    /// <summary>
    /// Determines if the interactive element is currently being held by the user,
    /// and provides details about the hold interaction, such as the start and current positions.
    /// </summary>
    /// <param name="args">When the method returns true, contains details about the hold interaction.
    /// The details include the starting position of the hold and the current mouse position.</param>
    /// <returns>True if the interactive element is being held; otherwise, false.</returns>
    public bool OnHold(out HoldArgs args)
    {
        args = default;
        if (!IsHeld()) return false;

        args = new HoldArgs
        {
            StartPosition = _gui.Input.PrevMousePosition, CurrentPosition = _gui.Input.MousePosition
        };
        return true;
    }

    /// <summary>
    /// Determines whether the element is being dragged, reporting the press origin alongside the
    /// current position so callers can apply a movement threshold before treating it as a drag.
    /// </summary>
    /// <param name="args">When the method returns true, describes the drag in progress.</param>
    /// <param name="button">The mouse button to track. Defaults to <see cref="MouseButton.Left"/>.</param>
    /// <returns>True while the element is held; otherwise, false.</returns>
    public bool OnDrag(out DragArgs args, MouseButton button = MouseButton.Left)
    {
        args = default;
        if (!IsHeld(button)) return false;

        args = new DragArgs
        {
            Origin = _gui.GetPressAnchor(_id),
            CurrentPosition = _gui.Input.MousePosition,
            FrameDelta = _gui.PointerFrameDelta
        };
        return true;
    }

    private bool IsHeld(MouseButton button = MouseButton.Left)
    {
        var mouseDown = _gui.Input.IsMouseButtonDown(button);
        var isDragging = _gui.GetDragState(_id);

        // Stage 1: keep going while this element still holds the pointer.
        if (isDragging && mouseDown && _gui.HoldsPointer(_id, button)) return true;

        // Stage 2: release, and hand the pointer back.
        if (isDragging && !mouseDown)
        {
            _gui.SetDragState(_id, false);
            _gui.ReleasePointer(_id);
            return false;
        }

        // Stage 3: begin only on the press itself, and only if nothing else already owns the pointer.
        // Testing the button's level here instead of its edge is what used to let any element the
        // cursor happened to cross mid-drag start a drag of its own.
        if (!_gui.Input.IsMouseButtonPressed(button)) return false;
        if (!OnHover()) return false;
        if (!_gui.TryCapturePointer(_id, button)) return false;

        if (!isDragging) _gui.SetPressAnchor(_id, _gui.Input.MousePosition);
        _gui.SetDragState(_id, true);
        return true;
    }

    /// <summary>
    /// Determines if the specified mouse button was clicked while the interactive element is hovered.
    /// </summary>
    /// <param name="button">The mouse button to check for the click interaction. Defaults to <see cref="MouseButton.Left"/>.</param>
    /// <returns>True if the specified mouse button was clicked and the interactive element is hovered; otherwise, false.</returns>
    public bool OnClick(MouseButton button = MouseButton.Left)
    {
        return OnHover() && _gui.Input.IsMouseButtonPressed(button);
    }
}
