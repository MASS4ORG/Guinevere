namespace Guinevere.Charts;

/// <summary>A finite, increasing interval for Cartesian data.</summary>
/// <param name="Min">Inclusive lower endpoint.</param>
/// <param name="Max">Inclusive upper endpoint.</param>
public readonly record struct ChartRange(double Min, double Max)
{
    /// <summary>Whether both endpoints are finite and increasing.</summary>
    public bool IsValid => double.IsFinite(Min) && double.IsFinite(Max) && Min < Max;

    /// <summary>Maps a value to the interval's unit coordinates, without clamping.</summary>
    public double Normalize(double value)
    {
        if (!IsValid || !double.IsFinite(value))
        {
            return double.NaN;
        }
        var width = Max - Min;
        var offset = value - Min;
        return double.IsFinite(width) && double.IsFinite(offset)
            ? offset / width
            : (value / 2 - Min / 2) / (Max / 2 - Min / 2);
    }

    /// <summary>Maps unit coordinates back to data coordinates, without clamping.</summary>
    public double Denormalize(double value)
    {
        if (!IsValid || !double.IsFinite(value))
        {
            return double.NaN;
        }
        var width = Max - Min;
        return value switch
        {
            0 => Min,
            1 => Max,
            _ when double.IsFinite(width) => Math.FusedMultiplyAdd(value, width, Min),
            _ when value is > 0 and < 1 => (1 - value) * Min + value * Max,
            _ => Math.FusedMultiplyAdd(value, Max / 2 - Min / 2, Min / 2) * 2
        };
    }
}
