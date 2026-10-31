namespace Guinevere.Graphs;

/// <summary>Logical keyboard focus, selection, and edit intents, independent of rendering and model types.</summary>
public sealed class GraphNavigation
{
    readonly Dictionary<string, GraphElementView> _items = new(StringComparer.Ordinal);
    readonly List<GraphElementView> _ordered = [];
    readonly HashSet<string> _selected = new(StringComparer.Ordinal);

    /// <summary>The active logical element, which may be outside the viewport.</summary>
    public string? FocusedKey { get; private set; }
    /// <summary>The active logical descriptor, if it is still in the view catalog.</summary>
    public GraphElementView? FocusedElement => FocusedKey is not null ? _items.GetValueOrDefault(FocusedKey) : null;
    /// <summary>The source port of an in-progress keyboard connection.</summary>
    public string? ConnectionSourceKey { get; private set; }
    /// <summary>View selection, independent of graph document state.</summary>
    public IReadOnlySet<string> SelectedKeys => _selected;
    /// <summary>Incremented whenever logical focus moves, so a canvas can reveal it.</summary>
    public long FocusRevision { get; private set; }
    /// <summary>Executes edits through the consumer's reversible graph editor.</summary>
    public Func<GraphEditRequest, GraphEditResult>? EditRequested { get; set; }
    /// <summary>Activates an authored frame, usually toggling saved collapse state.</summary>
    public Action<string>? GroupActivated { get; set; }
    /// <summary>Opens a consumer-owned searchable command or registry-backed node palette.</summary>
    public Action<GraphSearchKind>? SearchRequested { get; set; }
    /// <summary>Emits focus, selection, connection, and validation announcements.</summary>
    public event Action<GraphSemanticEvent>? Changed;

    /// <summary>Replaces the logical view catalog; descriptors are copied and duplicate keys are rejected.</summary>
    public void Update(IEnumerable<GraphElementView> items)
    {
        var next = items.ToArray();
        ValidateCatalog(next);
        _items.Clear();
        _ordered.Clear();
        foreach (var item in next)
        {
            _items.Add(item.Key, item);
            _ordered.Add(item);
        }
        PruneViewState();
    }

    static void ValidateCatalog(GraphElementView[] items)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            ArgumentException.ThrowIfNullOrEmpty(item.Key);
            if (!keys.Add(item.Key))
            {
                throw new ArgumentException("View keys must be unique.", nameof(items));
            }
        }

    }

    void PruneViewState()
    {
        foreach (var key in _selected.Where(key => !_items.ContainsKey(key)).ToArray())
        {
            _selected.Remove(key);
            Emit(GraphSemanticEventKind.SelectionChanged, key, null, "Removed from selection.");
        }
        if (FocusedKey is not null && !IsEnabled(FocusedKey))
        {
            FocusedKey = null;
            FocusRevision++;
            Emit(GraphSemanticEventKind.FocusChanged, null, null, "Graph focus cleared.");
        }
        if (ConnectionSourceKey is not null && !IsEnabled(ConnectionSourceKey))
        {
            CancelConnection();
        }
    }

    /// <summary>Focuses an enabled logical element without requiring a layout node.</summary>
    public bool Focus(string key)
    {
        if (!IsEnabled(key))
        {
            return false;
        }
        if (FocusedKey != key)
        {
            FocusedKey = key;
            FocusRevision++;
            Emit(GraphSemanticEventKind.FocusChanged, key, null, _items[key].Name);
        }
        return true;
    }

    /// <summary>Moves between nodes in world space, with stable ordinal ties independent of zoom.</summary>
    public bool Move(FocusDirection direction)
    {
        if (!IsSpatialDirection(direction))
        {
            return false;
        }
        var current = CurrentNode();
        if (current is null)
        {
            var first = _ordered.FirstOrDefault(static item => item.Role is GraphElementRole.Node or GraphElementRole.Group && item.Enabled);
            return first is not null && Focus(first.Key);
        }

        var best = FindSpatialNode(current, direction);
        return best is not null && Focus(best.Key);
    }

    GraphElementView? FindSpatialNode(GraphElementView current, FocusDirection direction)
    {
        GraphElementView? best = null;
        var score = double.PositiveInfinity;
        foreach (var candidate in _ordered)
        {
            if (!IsSpatialCandidate(candidate, current.Key))
            {
                continue;
            }
            var distance = DirectionScore(current.Bounds.Center, candidate.Bounds.Center, direction);
            if (IsBetterCandidate(candidate, best, distance, score))
            {
                score = distance;
                best = candidate;
            }
        }
        return best;
    }

    static bool IsSpatialDirection(FocusDirection direction) =>
        direction is FocusDirection.Left or FocusDirection.Right or FocusDirection.Up or FocusDirection.Down;

    static bool IsSpatialCandidate(GraphElementView candidate, string currentKey) =>
        candidate.Role is GraphElementRole.Node or GraphElementRole.Group && candidate.Enabled && candidate.Key != currentKey;

    static bool IsBetterCandidate(GraphElementView candidate, GraphElementView? best, double distance, double score) =>
        double.IsFinite(distance) && (distance < score || distance == score && best is not null
            && string.CompareOrdinal(candidate.Key, best.Key) < 0);

    /// <summary>Moves through an owning node's ports in adapter order, wrapping within that node.</summary>
    public bool MovePort(bool backwards = false)
    {
        var node = CurrentNode();
        if (node is null)
        {
            return false;
        }
        var ports = _ordered.Where(item => item.Role == GraphElementRole.Port && item.Enabled
            && item.ParentKey == node.Key).ToArray();
        if (ports.Length == 0)
        {
            return false;
        }
        var index = Array.FindIndex(ports, item => item.Key == FocusedKey);
        var next = index < 0 ? (backwards ? ports.Length - 1 : 0)
            : (index + (backwards ? ports.Length - 1 : 1)) % ports.Length;
        return Focus(ports[next].Key);
    }

    /// <summary>Cycles edges in adapter order so links can be inspected and deleted without a pointer.</summary>
    public bool MoveEdge(bool backwards = false)
    {
        var edges = _ordered.Where(static item => item.Role == GraphElementRole.Edge && item.Enabled).ToArray();
        if (edges.Length == 0)
        {
            return false;
        }
        var index = Array.FindIndex(edges, item => item.Key == FocusedKey);
        var next = index < 0 ? (backwards ? edges.Length - 1 : 0)
            : (index + (backwards ? edges.Length - 1 : 1)) % edges.Length;
        return Focus(edges[next].Key);
    }

    /// <summary>Cancels a connection, returns a port to its node, or returns an edge to one of its endpoints.</summary>
    public bool Back()
    {
        if (ConnectionSourceKey is not null)
        {
            CancelConnection();
            return true;
        }
        var item = FocusedElement;
        return item is not null && ReturnToOwner(item);
    }

    bool ReturnToOwner(GraphElementView item)
    {
        if (item.ParentKey is not null)
        {
            return Focus(item.ParentKey);
        }
        return item.Role == GraphElementRole.Edge && FocusEndpoint(item);
    }

    bool FocusEndpoint(GraphElementView item) => item.SourceKey is not null && Focus(item.SourceKey)
        || item.TargetKey is not null && Focus(item.TargetKey);

    /// <summary>Toggles the focused element's view selection and emits its new state.</summary>
    public bool ToggleSelection()
    {
        if (FocusedKey is null || !IsEnabled(FocusedKey))
        {
            return false;
        }
        var selected = _selected.Add(FocusedKey);
        if (!selected)
        {
            _selected.Remove(FocusedKey);
        }
        Emit(GraphSemanticEventKind.SelectionChanged, FocusedKey, null,
            selected ? "Selected." : "Deselected.");
        return true;
    }

    /// <summary>Selects an element exclusively, or toggles membership in a multi-selection.</summary>
    public bool Select(string key, bool toggle = false)
    {
        if (!Focus(key))
        {
            return false;
        }
        if (toggle)
        {
            return ToggleSelection();
        }
        foreach (var previous in _selected.Where(previous => previous != key).ToArray())
        {
            _selected.Remove(previous);
            Emit(GraphSemanticEventKind.SelectionChanged, previous, null, "Deselected.");
        }
        if (_selected.Add(key))
        {
            Emit(GraphSemanticEventKind.SelectionChanged, key, null, "Selected.");
        }
        return true;
    }

    /// <summary>Clears view selection without changing logical focus or the graph document.</summary>
    public void ClearSelection()
    {
        foreach (var key in _selected.ToArray())
        {
            _selected.Remove(key);
            Emit(GraphSemanticEventKind.SelectionChanged, key, null, "Deselected.");
        }
    }

    /// <summary>Replaces selection with enabled catalog keys while retaining focus unless a key is supplied.</summary>
    public void SetSelection(IEnumerable<string> keys, string? focus = null)
    {
        var next = keys.Where(IsEnabled).ToHashSet(StringComparer.Ordinal);
        foreach (var key in _selected.Except(next).ToArray())
        {
            _selected.Remove(key);
            Emit(GraphSemanticEventKind.SelectionChanged, key, null, "Deselected.");
        }
        foreach (var key in next)
        {
            if (_selected.Add(key))
            {
                Emit(GraphSemanticEventKind.SelectionChanged, key, null, "Selected.");
            }
        }
        if (focus is not null)
        {
            Focus(focus);
        }
    }

    /// <summary>Enters a node's ports, starts a connection, or asks the adapter to complete one.</summary>
    public bool Activate()
    {
        if (FocusedKey is null || !_items.TryGetValue(FocusedKey, out var item) || !item.Enabled)
        {
            return Move(FocusDirection.Right);
        }
        if (item.Role != GraphElementRole.Port)
        {
            return ActivateContainer(item);
        }
        if (ConnectionSourceKey is null)
        {
            ConnectionSourceKey = item.Key;
            Emit(GraphSemanticEventKind.ConnectionStarted, item.Key, null, $"Connecting from {item.Name}.");
            return true;
        }

        var result = RequestEdit(new GraphEditRequest(GraphEditKind.Connect, ConnectionSourceKey, item.Key));
        if (result)
        {
            ConnectionSourceKey = null;
        }
        return true;
    }

    bool ActivateContainer(GraphElementView item)
    {
        if (item.Role == GraphElementRole.Node)
        {
            return MovePort();
        }
        if (item.Role != GraphElementRole.Group)
        {
            return false;
        }
        GroupActivated?.Invoke(item.Key);
        return true;
    }

    /// <summary>Requests a direct connection between enabled logical ports through the attached editor.</summary>
    public bool Connect(string? source, string? target)
    {
        if (source is null || target is null || !IsPort(source) || !IsPort(target))
        {
            Emit(GraphSemanticEventKind.ValidationFailed, source, target, "Two available ports are required.");
            return false;
        }
        var succeeded = RequestEdit(new GraphEditRequest(GraphEditKind.Connect, source, target));
        if (succeeded)
        {
            ConnectionSourceKey = null;
        }
        return succeeded;
    }

    bool IsPort(string key) => _items.TryGetValue(key, out var item) && item.Enabled
        && item.Role == GraphElementRole.Port;

    /// <summary>Requests deletion of the focused element; the adapter owns validation, undo, and rollback.</summary>
    public bool Delete()
    {
        if (FocusedKey is null || !IsEnabled(FocusedKey)
            || !RequestEdit(new GraphEditRequest(GraphEditKind.Delete, FocusedKey)))
        {
            return false;
        }
        if (ConnectionSourceKey is not null)
        {
            CancelConnection();
        }
        return true;
    }

    /// <summary>Requests a searchable surface without inventing node registrations or a command model.</summary>
    public void Search(GraphSearchKind kind)
    {
        SearchRequested?.Invoke(kind);
        Emit(GraphSemanticEventKind.SearchRequested, FocusedKey, null,
            kind == GraphSearchKind.Commands ? "Search graph commands." : "Search node types.");
    }

    bool RequestEdit(GraphEditRequest request)
    {
        var result = EditRequested?.Invoke(request) ?? new GraphEditResult(false, "No graph editor is attached.");
        var successKind = request.Kind == GraphEditKind.Connect
            ? GraphSemanticEventKind.ConnectionCreated : GraphSemanticEventKind.ElementDeleted;
        if (!result.Succeeded || result.Announce)
        {
            Emit(result.Succeeded ? successKind : GraphSemanticEventKind.ValidationFailed,
                request.Key, request.TargetKey, result.Message);
        }
        return result.Succeeded;
    }

    void CancelConnection()
    {
        var source = ConnectionSourceKey;
        ConnectionSourceKey = null;
        Emit(GraphSemanticEventKind.ConnectionCancelled, source, null, "Connection cancelled.");
    }

    GraphElementView? CurrentNode()
    {
        if (FocusedKey is null || !_items.TryGetValue(FocusedKey, out var item))
        {
            return null;
        }
        return item.Role is GraphElementRole.Node or GraphElementRole.Group ? item
            : item.ParentKey is not null && _items.TryGetValue(item.ParentKey, out var parent) ? parent : null;
    }

    bool IsEnabled(string key) => _items.TryGetValue(key, out var item) && item.Enabled;

    void Emit(GraphSemanticEventKind kind, string? key, string? relatedKey, string message) =>
        Changed?.Invoke(new GraphSemanticEvent(kind, key, relatedKey, message));

    static double DirectionScore(GraphPoint origin, GraphPoint target, FocusDirection direction)
    {
        var x = target.X - origin.X;
        var y = target.Y - origin.Y;
        var (forward, sideways) = direction switch
        {
            FocusDirection.Left => (-x, y),
            FocusDirection.Right => (x, y),
            FocusDirection.Up => (-y, x),
            _ => (y, x)
        };
        return forward > 0 ? forward + 2 * Math.Abs(sideways) : double.PositiveInfinity;
    }
}
