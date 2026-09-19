namespace Guinevere;

/// <summary>
/// Manages focus state and navigation for GUI controls, providing Tab/Shift+Tab navigation
/// and cascaded focus behavior similar to Dear ImGui and other IM GUI libraries.
/// </summary>
public class FocusManager
{
    readonly Dictionary<string, FocusableControl> _focusableControls = new();
    readonly List<string> _frameControlOrder = [];
    readonly HashSet<string> _textInputIds = [];
    readonly Dictionary<string, FocusNavigationScopeState> _scopes = new();
    readonly List<string> _scopeStack = [];
    readonly HashSet<string> _frameScopeIds = [];
    string? _currentFocusedId;
    string? _nextFrameFocusId;
    bool _focusChangedThisFrame;
    FocusReason _nextFrameFocusReason = FocusReason.Programmatic;

    /// <summary>The scope that currently owns keyboard navigation, if any.</summary>
    public string? ActiveScopeId { get; private set; }

    internal string? CurrentRegistrationScopeId => _scopeStack.Count == 0 ? null : _scopeStack[^1];

    /// <summary>Why the focused control got focus, so a control can react to Tab differently to a click.</summary>
    public FocusReason CurrentFocusReason { get; private set; } = FocusReason.Programmatic;

    /// <summary>
    /// Gets the ID of the currently focused control, or null if no control has focus.
    /// </summary>
    public string? CurrentFocusedId => _currentFocusedId;

    /// <summary>
    /// Gets whether any control currently has focus.
    /// </summary>
    public bool HasAnyFocus => _currentFocusedId != null;

    /// <summary>Whether the focused control is an editor that must receive typed keystrokes.</summary>
    public bool IsTextInputFocused => _currentFocusedId is not null && _textInputIds.Contains(_currentFocusedId);

    /// <summary>Marks a focusable control as a text editor.</summary>
    public void RegisterTextInput(string controlId) => _textInputIds.Add(controlId);

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
        bool canReceiveFocus = true, bool isInteractable = true) =>
        RegisterFocusableControl(controlId, parentId, canReceiveFocus, isInteractable, null);

    /// <summary>Registers a focusable control with a center for directional navigation.</summary>
    /// <param name="controlId">Unique identifier for the control.</param>
    /// <param name="navigationPosition">Center used for directional navigation.</param>
    public void RegisterFocusableControl(string controlId, Vector2? navigationPosition) =>
        RegisterFocusableControl(controlId, null, true, true, navigationPosition);

    /// <summary>Registers a focusable control with a center for directional navigation.</summary>
    /// <param name="controlId">Unique identifier for the control.</param>
    /// <param name="parentId">Logical parent control, if any.</param>
    /// <param name="canReceiveFocus">Whether the control can receive keyboard focus.</param>
    /// <param name="isInteractable">Whether the control responds to pointer interaction.</param>
    /// <param name="navigationPosition">Center used for directional navigation.</param>
    public void RegisterFocusableControl(string controlId, string? parentId, bool canReceiveFocus,
        bool isInteractable, Vector2? navigationPosition)
    {
        if (!_focusableControls.ContainsKey(controlId))
        {
            _focusableControls[controlId] = new FocusableControl
            {
                ParentId = parentId,
                CanReceiveFocus = canReceiveFocus,
                ScopeId = CurrentRegistrationScopeId,
                NavigationPosition = navigationPosition
            };
        }
        else
        {
            // Update existing control info
            var control = _focusableControls[controlId];
            control.ParentId = parentId;
            control.CanReceiveFocus = canReceiveFocus;
            control.ScopeId = CurrentRegistrationScopeId;
            control.NavigationPosition = navigationPosition;
        }

        if (!canReceiveFocus && _currentFocusedId == controlId) ClearFocus();

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
                _nextFrameFocusReason = reason;
                _focusChangedThisFrame = true;
            }
        }
    }

    /// <summary>Associates explicit next and previous focus targets with a control.</summary>
    public void SetNavigationLinks(string controlId, string? nextId = null, string? previousId = null)
    {
        if (!_focusableControls.TryGetValue(controlId, out var control)) return;
        control.NextId = nextId;
        control.PreviousId = previousId;
    }

    /// <summary>Moves focus using explicit links first, then the closest control in the requested direction.</summary>
    public void Navigate(FocusDirection direction)
    {
        var candidates = GetNavigableIds();
        if (candidates.Count == 0) return;

        if (_currentFocusedId is null || !_focusableControls.TryGetValue(_currentFocusedId, out var current))
        {
            RequestFocus(candidates[0], FocusReason.Keyboard);
            return;
        }

        var linked = direction switch
        {
            FocusDirection.Next => current.NextId,
            FocusDirection.Previous => current.PreviousId,
            _ => null
        };
        if (linked is not null && candidates.Contains(linked))
        {
            RequestFocus(linked, FocusReason.Keyboard);
            return;
        }

        if (direction is FocusDirection.Next or FocusDirection.Previous)
        {
            var index = candidates.IndexOf(_currentFocusedId);
            RequestFocus(candidates[(index + (direction == FocusDirection.Next ? 1 : candidates.Count - 1)) % candidates.Count],
                FocusReason.Keyboard);
            return;
        }

        if (current.NavigationPosition is not { } origin) return;
        var target = candidates
            .Where(id => id != _currentFocusedId && _focusableControls[id].NavigationPosition is not null)
            .Select(id => (Id: id, Delta: _focusableControls[id].NavigationPosition!.Value - origin))
            .Where(x => IsInDirection(x.Delta, direction))
            .OrderBy(x => NavigationDistance(x.Delta, direction))
            .Select(x => x.Id)
            .FirstOrDefault();
        if (target is not null) RequestFocus(target, FocusReason.Keyboard);
    }

    /// <summary>Begins registering controls in a nestable focus-navigation scope.</summary>
    public FocusNavigationScope EnterScope(string scopeId, string? restoreFocusId = null)
    {
        _frameScopeIds.Add(scopeId);
        if (!_scopes.TryGetValue(scopeId, out var scope))
            _scopes[scopeId] = scope = new FocusNavigationScopeState();
        scope.ParentId = CurrentRegistrationScopeId;
        if (restoreFocusId is not null) scope.RestoreFocusId = restoreFocusId;
        _scopeStack.Add(scopeId);
        return new FocusNavigationScope(this, scopeId);
    }

    /// <summary>Makes a scope the sole owner of Tab and directional navigation.</summary>
    public void ActivateScope(string scopeId, string? restoreFocusId = null)
    {
        if (!_scopes.TryGetValue(scopeId, out var scope)) return;
        if (ActiveScopeId != scopeId)
        {
            scope.RestoreFocusId = restoreFocusId ?? _nextFrameFocusId ?? _currentFocusedId;
            ActiveScopeId = scopeId;
        }
    }

    internal void ExitScope(string scopeId)
    {
        if (_scopeStack.Count > 0 && _scopeStack[^1] == scopeId) _scopeStack.RemoveAt(_scopeStack.Count - 1);
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
            var focusableIds = GetNavigableIds();

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

        if (IsTextInputFocused) return;
        if (input.IsKeyPressed(KeyboardKey.Left)) Navigate(FocusDirection.Left);
        else if (input.IsKeyPressed(KeyboardKey.Right)) Navigate(FocusDirection.Right);
        else if (input.IsKeyPressed(KeyboardKey.Up)) Navigate(FocusDirection.Up);
        else if (input.IsKeyPressed(KeyboardKey.Down)) Navigate(FocusDirection.Down);
    }

    /// <summary>
    /// Should be called at the beginning of each frame to prepare for focus management.
    /// </summary>
    public void BeginFrame()
    {
        _frameControlOrder.Clear();
        _frameScopeIds.Clear();
        _scopeStack.Clear();
        _focusChangedThisFrame = false;

        // Apply any pending focus changes from the previous frame
        if (_nextFrameFocusId != _currentFocusedId)
        {
            _currentFocusedId = _nextFrameFocusId;
            CurrentFocusReason = _nextFrameFocusReason;
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

        if (ActiveScopeId is not null && !_frameScopeIds.Contains(ActiveScopeId))
        {
            var closingScope = _scopes[ActiveScopeId];
            ActiveScopeId = closingScope.ParentId;
            if (closingScope.RestoreFocusId is { } opener && _focusableControls.ContainsKey(opener))
                RequestFocus(opener, FocusReason.Programmatic);
        }

        foreach (var id in _scopes.Keys.Where(id => !_frameScopeIds.Contains(id)).ToList()) _scopes.Remove(id);
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

    List<string> GetNavigableIds() => _frameControlOrder
        .Where(id => _focusableControls.TryGetValue(id, out var control) && control.CanReceiveFocus
            && (ActiveScopeId is null || control.ScopeId == ActiveScopeId))
        .ToList();

    static bool IsInDirection(Vector2 delta, FocusDirection direction) => direction switch
    {
        FocusDirection.Left => delta.X < 0,
        FocusDirection.Right => delta.X > 0,
        FocusDirection.Up => delta.Y < 0,
        FocusDirection.Down => delta.Y > 0,
        _ => false
    };

    static float NavigationDistance(Vector2 delta, FocusDirection direction)
    {
        var primary = direction is FocusDirection.Left or FocusDirection.Right ? MathF.Abs(delta.X) : MathF.Abs(delta.Y);
        var secondary = direction is FocusDirection.Left or FocusDirection.Right ? MathF.Abs(delta.Y) : MathF.Abs(delta.X);
        return primary + secondary * 2;
    }
}

/// <summary>
/// Represents a control that can participate in the focus system.
/// </summary>
public class FocusableControl
{
    /// <summary>
    /// ID of the parent control, or null if this is a root control.
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// Whether this control can receive keyboard focus.
    /// </summary>
    public bool CanReceiveFocus { get; set; } = true;

    /// <summary>The navigation scope containing this control.</summary>
    public string? ScopeId { get; set; }
    /// <summary>Explicit target used by forward navigation.</summary>
    public string? NextId { get; set; }
    /// <summary>Explicit target used by reverse navigation.</summary>
    public string? PreviousId { get; set; }
    /// <summary>Center point used to choose a directional navigation target.</summary>
    public Vector2? NavigationPosition { get; set; }
}

class FocusNavigationScopeState
{
    public string? ParentId { get; set; }
    public string? RestoreFocusId { get; set; }
}

/// <summary>A frame-local registration scope that can constrain keyboard navigation.</summary>
public sealed class FocusNavigationScope : IDisposable
{
    readonly FocusManager _manager;
    readonly string _id;
    internal FocusNavigationScope(FocusManager manager, string id) => (_manager, _id) = (manager, id);
    /// <summary>Activates this scope until it is no longer registered in a frame.</summary>
    public void SetActive(string? restoreFocusId = null) => _manager.ActivateScope(_id, restoreFocusId);
    /// <summary>Leaves the registration scope.</summary>
    public void Dispose() => _manager.ExitScope(_id);
}

/// <summary>Keyboard focus movement directions.</summary>
public enum FocusDirection
{
    /// <summary>Move forward in registration order.</summary>
    Next,
    /// <summary>Move backward in registration order.</summary>
    Previous,
    /// <summary>Move to the closest control on the left.</summary>
    Left,
    /// <summary>Move to the closest control on the right.</summary>
    Right,
    /// <summary>Move to the closest control above.</summary>
    Up,
    /// <summary>Move to the closest control below.</summary>
    Down
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
