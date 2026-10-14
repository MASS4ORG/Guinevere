namespace Guinevere;

public partial class FocusManager
{
    readonly Dictionary<string, Dictionary<FocusDirection, string>> _directionalNeighbors = new();
    readonly Dictionary<string, string> _focusHistory = new();

    /// <summary>Overrides a directional neighbor; null restores spatial scoring.</summary>
    public void SetDirectionalNeighbor(string controlId, FocusDirection direction, string? targetId)
    {
        if (!_directionalNeighbors.TryGetValue(controlId, out var neighbors))
        {
            _directionalNeighbors[controlId] = neighbors = new Dictionary<FocusDirection, string>();
        }

        if (targetId is null)
        {
            neighbors.Remove(direction);
        }
        else
        {
            neighbors[direction] = targetId;
        }
    }

    /// <summary>Restores a scope's remembered focus, falling back to its first available control.</summary>
    public void RestoreScopeFocus(string scopeId)
    {
        var candidates = _frameControlOrder.Where(id => _focusableControls[id].CanReceiveFocus
            && _focusableControls[id].ScopeId == scopeId).ToList();
        var target = _focusHistory.TryGetValue(scopeId, out var remembered) && candidates.Contains(remembered)
            ? remembered : candidates.FirstOrDefault();
        if (target is not null)
        {
            RequestFocus(target);
        }
    }

    void RememberFocus()
    {
        if (_currentFocusedId is { } id && _focusableControls.TryGetValue(id, out var control)
            && control.ScopeId is { } scope)
        {
            _focusHistory[scope] = id;
        }
    }

    void RestoreActiveScope()
    {
        string? restore = null;
        while (ActiveScopeId is { } active && !_frameScopeIds.Contains(active))
        {
            var closing = _scopes[active];
            restore = closing.RestoreFocusId;
            ActiveScopeId = closing.ParentId;
        }

        var candidates = GetNavigableIds();
        if (restore is not null && candidates.Contains(restore))
        {
            RequestFocus(restore);
        }
        else if (ActiveScopeId is { } scope && !candidates.Contains(_nextFrameFocusId ?? ""))
        {
            RestoreScopeFocus(scope);
        }
    }
}
