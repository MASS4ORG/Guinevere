namespace Guinevere.Charts;

/// <summary>Describes one positive pie slice in y-down coordinates.</summary>
/// <param name="SourceIndex">Index of the original weight.</param>
/// <param name="StartAngle">Clockwise start angle in radians from the positive X axis.</param>
/// <param name="SweepAngle">Clockwise angular extent in radians.</param>
public readonly record struct PieSegment(int SourceIndex, double StartAngle, double SweepAngle);

/// <summary>Appearance and radial offset of one selected slice.</summary>
/// <param name="SourceIndex">Index of the selected input weight.</param>
/// <param name="Offset">Radial offset in screen pixels.</param>
/// <param name="Color">Optional replacement for the slice color.</param>
public readonly record struct PieSelection(int SourceIndex, float Offset, Color? Color = null);
