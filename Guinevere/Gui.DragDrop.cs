namespace Guinevere;

/// <summary>A channel identifier that keeps otherwise identical drag payloads separate.</summary>
public readonly record struct DragDropTag(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>The relationship between the active drag and a drop target this frame.</summary>
public enum DropTargetState
{
    /// <summary>No payload is previewing the target.</summary>
    None,
    /// <summary>The previewed payload can be accepted.</summary>
    HoverAccepted,
    /// <summary>The previewed payload cannot be accepted.</summary>
    HoverRejected,
    /// <summary>The target received the payload at the end of the preceding frame.</summary>
    Dropped
}

/// <summary>The typed, non-consuming result of offering a drop target.</summary>
public readonly record struct DropTargetResult<T>(DropTargetState State, T? Payload)
{
    /// <summary>Whether the previewed payload can be accepted.</summary>
    public bool IsAccepted => State == DropTargetState.HoverAccepted;
    /// <summary>Whether the target received the payload after the preceding frame.</summary>
    public bool IsDropped => State == DropTargetState.Dropped;
}

/// <summary>Themeable rendering options for <see cref="Gui.DrawDropIndicator"/>.</summary>
public readonly record struct DropIndicatorStyle(Color? Accepted = null, Color? Rejected = null,
    float Thickness = 2f, float Radius = 3f, byte FillAlpha = 32);

public partial class Gui
{
    DragSession? _drag;
    DragSession? _pendingDrag;
    PendingDrop? _pendingDrop;
    CompletedDrop? _completedDrop;

    /// <summary>The active payload, or null when nothing is being dragged.</summary>
    public object? CurrentDragPayload => _drag?.Payload;
    /// <summary>The active payload's channel identifier.</summary>
    public DragDropTag CurrentDragTag => _drag?.Tag ?? default;
    /// <summary>The source that owns the active drag.</summary>
    public string? CurrentDragSourceId => _drag?.SourceId;
    /// <summary>Whether a drag is in progress.</summary>
    public bool IsDragging => _drag is not null;

    /// <summary>Starts an untagged drag using the payload's runtime type.</summary>
    public bool BeginDrag(string sourceId, object payload, Action<Gui>? ghost = null) =>
        BeginDragCore(sourceId, payload, payload.GetType(), default, ghost, false);

    /// <summary>Starts a type-safe drag on an optional application-defined channel.</summary>
    public bool BeginDrag<T>(string sourceId, T payload, DragDropTag tag = default, Action<Gui>? ghost = null)
        where T : notnull => BeginDragCore(sourceId, payload, typeof(T), tag, ghost, false);

    /// <summary>Starts a keyboard-owned drag. Enter or Space on a focused target drops it.</summary>
    public bool BeginKeyboardDrag<T>(string sourceId, T payload, DragDropTag tag = default,
        Action<Gui>? ghost = null) where T : notnull =>
        BeginDragCore(sourceId, payload, typeof(T), tag, ghost, true);

    bool BeginDragCore(string sourceId, object payload, Type payloadType, DragDropTag tag,
        Action<Gui>? ghost, bool keyboard)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        ArgumentNullException.ThrowIfNull(payload);
        if (_drag is not null) return _drag.SourceId == sourceId;

        // Activation waits for the frame boundary so layout and render see the same drag state.
        _pendingDrag ??= new DragSession(sourceId, payload, payloadType, tag, ghost, keyboard);
        return _pendingDrag.SourceId == sourceId;
    }

    /// <summary>Turns the current node into an untagged pointer drag source.</summary>
    public bool DragSource(string sourceId, object payload, Action<Gui>? ghost = null, float threshold = 4f) =>
        DragSourceCore(sourceId, payload, payload.GetType(), default, ghost, threshold, false);

    /// <summary>
    /// Turns the current node into a typed source. A focused source starts a keyboard drag with
    /// Space or Enter; focus navigation selects a target, Enter drops, and Escape cancels.
    /// </summary>
    public bool DragSource<T>(string sourceId, T payload, Action<Gui>? ghost = null,
        float threshold = 4f, DragDropTag tag = default, bool keyboard = true) where T : notnull =>
        DragSourceCore(sourceId, payload, typeof(T), tag, ghost, threshold, keyboard);

    bool DragSourceCore(string sourceId, object payload, Type payloadType, DragDropTag tag,
        Action<Gui>? ghost, float threshold, bool keyboard)
    {
        if (Pass != Pass.Pass2Render) return false;

        if (keyboard)
        {
            Focus.RegisterFocusableControl(sourceId,
                new Vector2(CurrentNode.Rect.X + CurrentNode.Rect.W * 0.5f,
                    CurrentNode.Rect.Y + CurrentNode.Rect.H * 0.5f));
        }

        if (_drag is not null) return _drag.SourceId == sourceId;

        if (keyboard)
        {
            if (HasFocus(sourceId) && (Input.IsKeyPressed(KeyboardKey.Space) || Input.IsKeyPressed(KeyboardKey.Enter)))
                return BeginDragCore(sourceId, payload, payloadType, tag, ghost, true);
        }

        if (!GetInteractable().OnDrag(out var args) || args.TotalDelta.Length() < threshold) return false;
        return BeginDragCore(sourceId, payload, payloadType, tag, ghost, false);
    }

    /// <summary>Peeks at the active payload without consuming it.</summary>
    public bool TryPeekPayload<T>(out T? payload, DragDropTag tag = default)
    {
        if (_drag is { } drag && drag.Tag == tag && typeof(T).IsAssignableFrom(drag.PayloadType)
            && drag.Payload is T value)
        {
            payload = value;
            return true;
        }

        payload = default;
        return false;
    }

    /// <summary>Whether the active payload has the requested type/tag and passes a predicate.</summary>
    public bool CanAccept<T>(DragDropTag tag = default, Func<T, bool>? predicate = null) =>
        TryPeekPayload<T>(out var payload, tag) && (predicate?.Invoke(payload!) ?? true);

    /// <summary>Offers the current node as a typed drop target.</summary>
    public DropTargetResult<T> DropTarget<T>(string id, DragDropTag tag = default,
        Func<T, bool>? canAccept = null, Action<T>? onDrop = null, bool keyboard = true) =>
        DropTarget(CurrentNode.Rect, id, tag, canAccept, onDrop, keyboard);

    /// <summary>Offers an explicit screen-space rectangle as a typed drop target.</summary>
    public DropTargetResult<T> DropTarget<T>(Rect rect, string id, DragDropTag tag = default,
        Func<T, bool>? canAccept = null, Action<T>? onDrop = null, bool keyboard = true)
    {
        if (Pass != Pass.Pass2Render) return default;

        if (_completedDrop is { TargetId: var targetId } completed && targetId == id
            && completed.Tag == tag && completed.Payload is T dropped)
        {
            _completedDrop = null;
            return new(DropTargetState.Dropped, dropped);
        }

        if (_drag is null) return default;
        if (keyboard)
            Focus.RegisterFocusableControl(id,
                new Vector2(rect.X + rect.W * 0.5f, rect.Y + rect.H * 0.5f));

        var preview = !_drag.Keyboard
            ? rect.Contains(Input.MousePosition)
            : keyboard && HasFocus(id);
        if (!preview) return default;

        if (!TryPeekPayload<T>(out var payload, tag) || !(canAccept?.Invoke(payload!) ?? true))
            return new(DropTargetState.HoverRejected, payload);

        var released = !_drag.Keyboard && !Input.IsMouseButtonDown(MouseButton.Left);
        var confirmed = _drag.Keyboard && (Input.IsKeyPressed(KeyboardKey.Enter)
                                           || Input.IsKeyPressed(KeyboardKey.Space));
        if (released || confirmed)
            _pendingDrop = new(id, _drag.Payload, _drag.Tag, value => onDrop?.Invoke((T)value));

        return new(DropTargetState.HoverAccepted, payload);
    }

    /// <summary>Marks the current node as a legacy object drop target.</summary>
    public bool DropTarget(string id, Func<object, bool>? accept = null, Action<object>? onDrop = null) =>
        DropTarget(CurrentNode.Rect, id, accept, onDrop);

    /// <summary>Marks an explicit rectangle as a legacy object drop target.</summary>
    public bool DropTarget(Rect rect, string id, Func<object, bool>? accept = null, Action<object>? onDrop = null)
    {
        if (Pass != Pass.Pass2Render || _drag is null || !rect.Contains(Input.MousePosition)) return false;
        if (accept is not null && !accept(_drag.Payload)) return false;
        if (onDrop is not null && !Input.IsMouseButtonDown(MouseButton.Left))
            _pendingDrop = new(id, _drag.Payload, _drag.Tag, onDrop);
        return true;
    }

    /// <summary>Draws standard accepted or rejected feedback over a drop target.</summary>
    public void DrawDropIndicator(DropTargetState state, Rect? rect = null, DropIndicatorStyle style = default)
    {
        if (Pass != Pass.Pass2Render || state is DropTargetState.None or DropTargetState.Dropped) return;
        var color = state == DropTargetState.HoverAccepted
            ? style.Accepted ?? Controls.Positive
            : style.Rejected ?? Controls.Negative;
        var area = rect ?? CurrentNode.Rect;
        if (style.FillAlpha > 0)
            DrawRect(area, Color.FromArgb(style.FillAlpha, color.R, color.G, color.B), style.Radius);
        DrawRectBorder(area, color, style.Thickness, style.Radius);
    }

    /// <summary>Draws the active drag's ghost under the cursor.</summary>
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

    /// <summary>Abandons the drag without notifying a target.</summary>
    public void CancelDrag()
    {
        _drag = null;
        _pendingDrag = null;
        _pendingDrop = null;
    }

    void ResolveDrag()
    {
        if (_drag is null)
        {
            _completedDrop = null;
            _drag = _pendingDrag;
            _pendingDrag = null;
            return;
        }

        if (Input.IsKeyPressed(KeyboardKey.Escape))
        {
            CancelDrag();
            return;
        }

        if (_drag.Keyboard)
        {
            if (_pendingDrop is null) return;
        }
        else if (Input.IsMouseButtonDown(MouseButton.Left)) return;

        var drop = _pendingDrop;
        _drag = null;
        _pendingDrop = null;
        if (drop is null) return;

        drop.OnDrop(drop.Payload);
        _completedDrop = new(drop.Id, drop.Payload, drop.Tag);
    }

    /// <summary>The z-index used by drag ghosts.</summary>
    public const int DragGhostZIndex = 10_000;

    sealed record DragSession(string SourceId, object Payload, Type PayloadType, DragDropTag Tag,
        Action<Gui>? Ghost, bool Keyboard);
    sealed record PendingDrop(string Id, object Payload, DragDropTag Tag, Action<object> OnDrop);
    sealed record CompletedDrop(string TargetId, object Payload, DragDropTag Tag);
}
