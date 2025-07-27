namespace Guinevere;

/// <summary>
/// Manages focus state and navigation for GUI controls, providing Tab/Shift+Tab navigation
/// and cascaded focus behavior similar to Dear ImGui and other IM GUI libraries.
/// </summary>
public class FocusManager
{
    private readonly Dictionary<string, FocusableControl> _focusableControls = new();
    private readonly List<string> _frameControlOrder = new();
    private string? _currentFocusedId;
    private string? _nextFrameFocusId;
    private bool _focusChangedThisFrame;

    /// <summary>
    /// Gets the ID of the currently focused control, or null if no control has focus.
    /// </summary>
    public string? CurrentFocusedId => _currentFocusedId;

    /// <summary>
    /// Gets whether any control currently has focus.
    /// </summary>
    public bool HasAnyFocus => _currentFocusedId != null;

    /// <summary>
    /// Gets whether the focus state changed during the current frame.
    /// </summary>
    public bool FocusChangedThisFrame => _focusChangedThisFrame;

    /// <summary>
    /// Registers a control as focusable for the current frame.
    /// </summary>
    /// <param name="controlId">Unique identifier for the control</param>
    /// <param name="parentId">ID of the parent control for cascaded focus, or null if no parent</param>
    /// <param name="canReceiveFocus">Whether this control can receive keyboard focus</param>
    /// <param name="isInteractable">Whether this control responds to mouse interactions</param>
    public void RegisterFocusableControl(string controlId, string? parentId = null,
        bool canReceiveFocus = true, bool isInteractable = true)
    {
        if (!_focusableControls.ContainsKey(controlId))
        {
            _focusableControls[controlId] = new FocusableControl
            {
                Id = controlId,
                ParentId = parentId,
                CanReceiveFocus = canReceiveFocus,
                IsInteractable = isInteractable
            };
        }
        else
        {
            // Update existing control info
            var control = _focusableControls[controlId];
            control.ParentId = parentId;
            control.CanReceiveFocus = canReceiveFocus;
            control.IsInteractable = isInteractable;
        }

        // Track the order controls are registered this frame
        if (!_frameControlOrder.Contains(controlId))
        {
            _frameControlOrder.Add(controlId);
        }
    }

    /// <summary>
    /// Checks if the specified control currently has focus.
    /// </summary>
    /// <param name="controlId">The control ID to check</param>
    /// <returns>True if the control has focus, false otherwise</returns>
    public bool HasFocus(string controlId)
    {
        return _currentFocusedId == controlId;
    }

    /// <summary>
    /// Checks if the specified control or any of its descendants has focus.
    /// </summary>
    /// <param name="controlId">The control ID to check</param>
    /// <returns>True if the control or its descendants have focus, false otherwise</returns>
    public bool HasFocusWithin(string controlId)
    {
        if (_currentFocusedId == null) return false;

        // Check if the control itself has focus
        if (_currentFocusedId == controlId) return true;

        // Check if any descendant has focus by walking up the parent chain
        var current = _currentFocusedId;
        while (current != null)
        {
            if (_focusableControls.TryGetValue(current, out var control))
            {
                if (control.ParentId == controlId) return true;
                current = control.ParentId;
            }
            else
            {
                break;
            }
        }

        return false;
    }

    /// <summary>
    /// Requests focus for the specified control.
    /// </summary>
    /// <param name="controlId">The control ID to focus</param>
    /// <param name="reason">The reason for the focus request</param>
    public void RequestFocus(string controlId, FocusReason reason = FocusReason.Programmatic)
    {
        if (_focusableControls.TryGetValue(controlId, out var control) && control.CanReceiveFocus)
        {
            if (_currentFocusedId != controlId)
            {
                _nextFrameFocusId = controlId;
                _focusChangedThisFrame = true;
            }
        }
    }

    /// <summary>
    /// Clears focus from all controls.
    /// </summary>
    public void ClearFocus()
    {
        if (_currentFocusedId != null)
        {
            _nextFrameFocusId = null;
            _focusChangedThisFrame = true;
        }
    }

    /// <summary>
    /// Handles keyboard navigation (Tab/Shift+Tab) between focusable controls.
    /// </summary>
    /// <param name="input">The input handler to check for key presses</param>
    public void HandleKeyboardNavigation(IInputHandler input)
    {
        if (input.IsKeyPressed(KeyboardKey.Tab))
        {
            var focusableIds = _frameControlOrder
                .Where(id => _focusableControls.TryGetValue(id, out var control) && control.CanReceiveFocus)
                .ToList();

            if (focusableIds.Count == 0) return;

            var isShiftHeld = input.IsKeyDown(KeyboardKey.LeftShift) || input.IsKeyDown(KeyboardKey.RightShift);

            if (_currentFocusedId == null)
            {
                // No current focus, focus the first control
                RequestFocus(focusableIds[0], FocusReason.Keyboard);
            }
            else
            {
                var currentIndex = focusableIds.IndexOf(_currentFocusedId);
                if (currentIndex >= 0)
                {
                    int nextIndex;
                    if (isShiftHeld)
                    {
                        // Shift+Tab: go to previous control
                        nextIndex = currentIndex == 0 ? focusableIds.Count - 1 : currentIndex - 1;
                    }
                    else
                    {
                        // Tab: go to next control
                        nextIndex = (currentIndex + 1) % focusableIds.Count;
                    }

                    RequestFocus(focusableIds[nextIndex], FocusReason.Keyboard);
                }
                else
                {
                    // Current focused control is not in this frame's list, focus first available
                    RequestFocus(focusableIds[0], FocusReason.Keyboard);
                }
            }
        }
    }

    /// <summary>
    /// Should be called at the beginning of each frame to prepare for focus management.
    /// </summary>
    public void BeginFrame()
    {
        _frameControlOrder.Clear();
        _focusChangedThisFrame = false;

        // Apply any pending focus changes from the previous frame
        if (_nextFrameFocusId != _currentFocusedId)
        {
            _currentFocusedId = _nextFrameFocusId;
            _focusChangedThisFrame = true;
        }
    }

    /// <summary>
    /// Should be called at the end of each frame to clean up focus management.
    /// </summary>
    public void EndFrame()
    {
        // Remove controls that weren't registered this frame
        var controlsToRemove = _focusableControls.Keys
            .Where(id => !_frameControlOrder.Contains(id))
            .ToList();

        foreach (var id in controlsToRemove)
        {
            _focusableControls.Remove(id);

            // If the removed control had focus, clear focus
            if (_currentFocusedId == id)
            {
                _currentFocusedId = null;
                _nextFrameFocusId = null;
            }
        }
    }

    /// <summary>
    /// Gets all parent IDs in the hierarchy chain for cascaded focus checking.
    /// </summary>
    /// <param name="controlId">The control to get parents for</param>
    /// <returns>List of parent IDs from immediate parent to root</returns>
    public List<string> GetParentChain(string controlId)
    {
        var parents = new List<string>();
        var current = controlId;

        while (current != null && _focusableControls.TryGetValue(current, out var control))
        {
            if (control.ParentId != null)
            {
                parents.Add(control.ParentId);
                current = control.ParentId;
            }
            else
            {
                break;
            }
        }

        return parents;
    }
}

/// <summary>
/// Represents a control that can participate in the focus system.
/// </summary>
public class FocusableControl
{
    /// <summary>
    /// Unique identifier for this control.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// ID of the parent control, or null if this is a root control.
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// Whether this control can receive keyboard focus.
    /// </summary>
    public bool CanReceiveFocus { get; set; } = true;

    /// <summary>
    /// Whether this control responds to mouse interactions.
    /// </summary>
    public bool IsInteractable { get; set; } = true;
}

/// <summary>
/// Indicates the reason why focus was requested or changed.
/// </summary>
public enum FocusReason
{
    /// <summary>
    /// Focus was set programmatically via code.
    /// </summary>
    Programmatic,

    /// <summary>
    /// Focus was changed via keyboard navigation (Tab/Shift+Tab).
    /// </summary>
    Keyboard,

    /// <summary>
    /// Focus was changed via mouse click.
    /// </summary>
    Mouse
}
