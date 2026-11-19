namespace Guinevere;

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
