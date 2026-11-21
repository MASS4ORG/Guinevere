namespace Guinevere;

/// <summary>The stable state and events produced by a headless behavior.</summary>
public readonly record struct ControlBehaviorResult(
    ControlVisualState State,
    bool Activated = false,
    bool Repeated = false,
    DragArgs? Drag = null)
{
    /// <summary>Returns whether a visual-state flag is present.</summary>
    public bool Is(ControlVisualState state) => State.HasFlag(state);
}
