namespace Guinevere;

/// <summary>Metadata exposed to an accessibility or automation adapter.</summary>
public readonly record struct ControlSemantics(
    string Id,
    ControlRole Role,
    string? Label,
    string? Value,
    Rect Bounds,
    ControlVisualState State);
