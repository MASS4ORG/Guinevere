namespace Guinevere;

/// <summary>The semantic kind of a headless control.</summary>
public enum ControlRole
{
    /// <summary>No specialized semantic role.</summary>
    None,
    /// <summary>A command button.</summary>
    Button,
    /// <summary>A binary checkbox.</summary>
    Checkbox,
    /// <summary>One option in a single-selection group.</summary>
    Radio,
    /// <summary>A binary switch.</summary>
    Switch,
    /// <summary>One page in a tab set.</summary>
    Tab,
    /// <summary>A bounded numeric value.</summary>
    Slider
}

/// <summary>Composable visual state produced by a headless control behavior.</summary>
[Flags]
public enum ControlVisualState
{
    /// <summary>No state flags.</summary>
    None = 0,
    /// <summary>The control cannot be operated.</summary>
    Disabled = 1 << 0,
    /// <summary>The control owns keyboard focus.</summary>
    Focused = 1 << 1,
    /// <summary>The pointer is over the control.</summary>
    Hovered = 1 << 2,
    /// <summary>The primary pointer button is held on the control.</summary>
    Pressed = 1 << 3,
    /// <summary>The control has a checked value.</summary>
    Checked = 1 << 4,
    /// <summary>The control is the selected item.</summary>
    Selected = 1 << 5,
    /// <summary>The control owns an active drag.</summary>
    Dragging = 1 << 6
}

/// <summary>Metadata exposed to an accessibility or automation adapter.</summary>
public readonly record struct ControlSemantics(
    string Id,
    ControlRole Role,
    string? Label,
    string? Value,
    Rect Bounds,
    ControlVisualState State);

/// <summary>Optional platform capability that receives the controls present in a rendered frame.</summary>
public interface IControlSemanticsSink : IPlatformCapability
{
    /// <summary>Publishes one control's current metadata.</summary>
    void Publish(ControlSemantics semantics);
}

/// <summary>
/// Optional command source for controller, assistive-technology, or application-defined activation.
/// The focused control passes its stable id to the source during the render pass.
/// </summary>
public interface IControlActivationSource : IPlatformCapability
{
    /// <summary>Returns true when a routed activation command targets this control.</summary>
    bool IsActivated(string controlId);
}

/// <summary>Settings shared by the core headless behaviors.</summary>
public sealed record ControlBehaviorOptions(
    bool Enabled = true,
    bool Focusable = true,
    bool Checked = false,
    bool Selected = false,
    ControlRole Role = ControlRole.None,
    string? Label = null,
    string? Value = null,
    KeyboardKey ActivationKey = KeyboardKey.Space);

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
