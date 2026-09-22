namespace Guinevere;

/// <summary>Where an event is in its trip along the path from the root to its target.</summary>
public enum EventPhase
{
    /// <summary>Travelling from the root down to the target's parent; only capture listeners run.</summary>
    Capture,

    /// <summary>At the target itself; capture listeners run before bubble listeners.</summary>
    Target,

    /// <summary>Travelling from the target's parent back up to the root; only bubble listeners run.</summary>
    Bubble
}

/// <summary>
/// An input event dispatched DOM-style along the node path from the root to <see cref="TargetId"/>.
/// Events are dispatched at the start of a frame against the previous frame's laid-out tree — the one the
/// user saw when the input happened — so listeners run before the new tree is built.
/// </summary>
/// <remarks>
/// Nodes are rebuilt every frame, so targets are identified by their stable node ids rather than node
/// references. Listeners should change application state, not hold on to the event.
/// </remarks>
public abstract class GuiEvent
{
    /// <summary>The id of the node the event is aimed at.</summary>
    public string TargetId { get; internal set; } = string.Empty;

    /// <summary>The id of the node whose listener is running now.</summary>
    public string CurrentTargetId { get; internal set; } = string.Empty;

    /// <summary>The current propagation phase.</summary>
    public EventPhase Phase { get; internal set; }

    /// <summary>Whether <see cref="PreventDefault"/> has any effect on this event type.</summary>
    public virtual bool Cancelable => false;

    /// <summary>Whether a listener called <see cref="PreventDefault"/> on a cancelable event.</summary>
    public bool DefaultPrevented { get; private set; }

    internal bool PropagationStopped { get; private set; }
    internal bool ImmediatePropagationStopped { get; private set; }

    /// <summary>Stops the event reaching further nodes; the remaining listeners on this node still run.</summary>
    public void StopPropagation() => PropagationStopped = true;

    /// <summary>Stops the event immediately, including the remaining listeners on this node.</summary>
    public void StopImmediatePropagation() => PropagationStopped = ImmediatePropagationStopped = true;

    /// <summary>
    /// Suppresses the built-in reaction to a cancelable event: the matching polling edge disappears from
    /// <see cref="Gui.Input"/> for the rest of the frame, so controls that poll it do not react as well.
    /// </summary>
    public void PreventDefault()
    {
        if (Cancelable) DefaultPrevented = true;
    }
}

/// <summary>A pointer event at a screen position.</summary>
public abstract class PointerEvent : GuiEvent
{
    /// <summary>The pointer position in screen coordinates.</summary>
    public Vector2 Position { get; internal init; }

    /// <summary>The button that changed, or <see cref="MouseButton.Left"/> for movement.</summary>
    public MouseButton Button { get; internal init; }
}

/// <summary>A button went down over the target. Preventing it hides the press from polling controls.</summary>
public sealed class PointerDownEvent : PointerEvent
{
    /// <inheritdoc />
    public override bool Cancelable => true;
}

/// <summary>A button came up. While a gesture holds the pointer, the capture owner is the target.</summary>
public sealed class PointerUpEvent : PointerEvent;

/// <summary>The pointer moved since the previous frame.</summary>
public sealed class PointerMoveEvent : PointerEvent
{
    /// <summary>How far the pointer moved since the previous frame.</summary>
    public Vector2 Delta { get; internal init; }
}

/// <summary>
/// A press and release of the same button. The target is the deepest node on both the press and the release
/// paths, so pressing a child and releasing on its sibling clicks their shared parent.
/// </summary>
public sealed class ClickEvent : PointerEvent
{
    /// <summary>One for a single click, two for a double click, and so on.</summary>
    public int ClickCount { get; internal init; }
}

/// <summary>A drag gesture on the node that received the press.</summary>
public abstract class DragEvent : GuiEvent
{
    /// <summary>The button driving the drag.</summary>
    public MouseButton Button { get; internal init; }

    /// <summary>The pointer position at the press.</summary>
    public Vector2 Origin { get; internal init; }

    /// <summary>The current pointer position.</summary>
    public Vector2 Position { get; internal init; }

    /// <summary>How far the pointer moved since the previous frame.</summary>
    public Vector2 FrameDelta { get; internal init; }

    /// <summary>How far the pointer moved since the press.</summary>
    public Vector2 TotalDelta => Position - Origin;
}

/// <summary>The pointer moved past <see cref="Gui.DragThreshold"/> with a button held.</summary>
public sealed class DragStartEvent : DragEvent;

/// <summary>The pointer moved during a drag.</summary>
public sealed class DragMoveEvent : DragEvent;

/// <summary>The button driving a drag came up.</summary>
public sealed class DragEndEvent : DragEvent;

/// <summary>The wheel turned over the target. Preventing it stops scroll containers scrolling.</summary>
public sealed class ScrollEvent : GuiEvent
{
    /// <inheritdoc />
    public override bool Cancelable => true;

    /// <summary>The pointer position in screen coordinates.</summary>
    public Vector2 Position { get; internal init; }

    /// <summary>The wheel delta, as reported by <see cref="IInputHandler.MouseWheelDelta"/>.</summary>
    public float Delta { get; internal init; }
}

/// <summary>A keyboard event aimed at the focused node, or the root when nothing has focus.</summary>
public abstract class KeyEvent : GuiEvent
{
    /// <summary>The key that changed.</summary>
    public KeyboardKey Key { get; internal init; }
}

/// <summary>A key went down. Preventing it hides the press from polling code, including focus navigation.</summary>
public sealed class KeyDownEvent : KeyEvent
{
    /// <inheritdoc />
    public override bool Cancelable => true;
}

/// <summary>A key came up.</summary>
public sealed class KeyUpEvent : KeyEvent;

/// <summary>Text was typed while the target had focus. Preventing it keeps the text from text inputs.</summary>
public sealed class TextInputEvent : GuiEvent
{
    /// <inheritdoc />
    public override bool Cancelable => true;

    /// <summary>The characters typed this frame.</summary>
    public string Text { get; internal init; } = string.Empty;
}

/// <summary>Focus moved. Both focus events bubble, so an ancestor learns when focus enters or leaves it.</summary>
public abstract class FocusEvent : GuiEvent
{
    /// <summary>The other side of the move: the node losing focus, or the one gaining it.</summary>
    public string? RelatedTargetId { get; internal init; }
}

/// <summary>The target gained focus.</summary>
public sealed class FocusInEvent : FocusEvent;

/// <summary>The target lost focus.</summary>
public sealed class FocusOutEvent : FocusEvent;
