namespace Guinevere;

/// <summary>Settings shared by the core headless behaviors.</summary>
/// <param name="Enabled">Whether the control can be operated.</param>
/// <param name="Focusable">Whether the control takes keyboard focus.</param>
/// <param name="Checked">Whether the control currently holds a checked value.</param>
/// <param name="Selected">Whether the control is the selected item.</param>
/// <param name="Role">Semantic role published to adapters; null means <see cref="ControlRole.None"/>.</param>
/// <param name="Label">Accessible name.</param>
/// <param name="Value">Accessible value text.</param>
/// <param name="ActivationKey">Key that activates the focused control, besides Enter.</param>
public sealed record ControlBehaviorOptions(
    bool Enabled = true,
    bool Focusable = true,
    bool Checked = false,
    bool Selected = false,
    ControlRole? Role = null,
    string? Label = null,
    string? Value = null,
    KeyboardKey ActivationKey = KeyboardKey.Space);
