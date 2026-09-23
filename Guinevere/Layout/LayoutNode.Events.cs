namespace Guinevere;

public partial class LayoutNode
{
    List<EventListener>? _listeners;

    /// <summary>The cursor shape set on this node, inherited by descendants that set none.</summary>
    internal PointerCursor? CursorShape { get; private set; }

    /// <summary>Whether the pointer can target this node. Descendants are unaffected.</summary>
    internal bool IsHitTestVisible { get; private set; } = true;

    internal bool HasListeners => _listeners is { Count: > 0 };

    /// <summary>
    /// Listens for <typeparamref name="TEvent"/> (or any subtype) aimed at this node or a descendant.
    /// Bubble listeners run on the way back up from the target; capture listeners run on the way down, before
    /// any descendant sees the event. Registered in the build pass, so each call site adds one listener.
    /// </summary>
    /// <param name="handler">Runs at the start of the next frame, before its tree is built.</param>
    /// <param name="capture">Listen in the capture phase instead of the bubble phase.</param>
    /// <returns>The current instance, for chaining.</returns>
    public LayoutNode On<TEvent>(Action<TEvent> handler, bool capture = false) where TEvent : GuiEvent
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (_gui.Pass != Pass.Pass1Build) return this;

        (_listeners ??= []).Add(new EventListener<TEvent>(handler, capture));
        _gui.NoteEventListener();
        return this;
    }

    /// <summary>
    /// Shows <paramref name="cursor"/> while the pointer is over this node or a descendant that sets none,
    /// and while this node holds the pointer for a gesture.
    /// </summary>
    /// <returns>The current instance, for chaining.</returns>
    public LayoutNode Cursor(PointerCursor cursor)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        CursorShape = cursor;
        _gui.NoteCursorNode();
        return this;
    }

    /// <summary>
    /// Lets the pointer pass through this node to whatever lies beneath it, like CSS
    /// <c>pointer-events: none</c> — for decorative or full-window layout nodes drawn above interactive ones.
    /// Descendants can still be targeted, and events targeted at them still bubble through this node.
    /// </summary>
    /// <returns>The current instance, for chaining.</returns>
    public LayoutNode HitTestVisible(bool visible = true)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        IsHitTestVisible = visible;
        return this;
    }

    /// <summary>Runs this node's listeners for <paramref name="phase"/>; capture ones first at the target.</summary>
    internal void Dispatch(GuiEvent e, EventPhase phase)
    {
        if (_listeners is null) return;

        e.CurrentTargetId = Id;
        e.Phase = phase;
        if (phase != EventPhase.Bubble && Run(e, capture: true)) return;
        if (phase != EventPhase.Capture) Run(e, capture: false);
    }

    /// <summary>Runs one phase's listeners, returning whether the event was stopped outright.</summary>
    bool Run(GuiEvent e, bool capture)
    {
        foreach (var listener in _listeners!)
        {
            if (listener.Capture != capture) continue;
            listener.TryInvoke(e);
            if (e.ImmediatePropagationStopped) return true;
        }
        return false;
    }

    /// <summary>Whether a listener on this node would receive <typeparamref name="TEvent"/>.</summary>
    internal bool Listens<TEvent>() where TEvent : GuiEvent
    {
        if (_listeners is null) return false;
        foreach (var listener in _listeners)
            if (listener.Accepts(typeof(TEvent)))
                return true;
        return false;
    }

    void ResetInteraction()
    {
        _listeners?.Clear();
        CursorShape = null;
        IsHitTestVisible = true;
    }

    abstract class EventListener(bool capture)
    {
        public bool Capture { get; } = capture;
        public abstract bool Accepts(Type eventType);
        public abstract void TryInvoke(GuiEvent e);
    }

    sealed class EventListener<TEvent>(Action<TEvent> handler, bool capture) : EventListener(capture)
        where TEvent : GuiEvent
    {
        public override bool Accepts(Type eventType) => typeof(TEvent).IsAssignableFrom(eventType);

        public override void TryInvoke(GuiEvent e)
        {
            if (e is TEvent typed) handler(typed);
        }
    }
}
