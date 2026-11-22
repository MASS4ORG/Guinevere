namespace Guinevere.Charts;

/// <summary>A Cartesian data point in double precision.</summary>
/// <param name="X">Horizontal data coordinate.</param>
/// <param name="Y">Vertical data coordinate.</param>
public readonly record struct ChartPoint(double X, double Y);
