namespace Guinevere;

/// <summary>A contact update routed through capture, target and bubble phases.</summary>
public sealed class TouchEvent : PointerEvent
{
    /// <summary>The original contact, including its id, phase and optional pressure.</summary>
    public TouchContact Contact { get; internal init; }
    /// <inheritdoc />
    public override bool Cancelable => true;
    internal string? CaptureTarget { get; private set; }
    /// <summary>Captures this contact to the current listener's node until it ends or is canceled.</summary>
    public void CapturePointer() => CaptureTarget = CurrentTargetId;
}

/// <summary>A host-provided or recognized gesture routed through the contact's capture owner.</summary>
public sealed class TouchGestureEvent : GuiEvent
{
    /// <summary>The gesture delta in window coordinates.</summary>
    public TouchGesture Gesture { get; internal init; }
    /// <inheritdoc />
    public override bool Cancelable => true;
}
