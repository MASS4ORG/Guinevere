namespace Guinevere.Charts;

/// <summary>Interpolates chart values in stable source order using caller-owned storage.</summary>
public static class ChartAnimation
{
    /// <summary>Writes an interpolated sample for each matching source index and returns the number written.</summary>
    /// <remarks>Progress is clamped to [0, 1]. Missing values remain missing between endpoints. The destination can replace
    /// either input in place. Invalid progress, lengths or shifted overlaps return zero.</remarks>
    public static int Interpolate(ReadOnlySpan<double> from, ReadOnlySpan<double> to, double progress, Span<double> values)
    {
        if (!double.IsFinite(progress) || from.Length != to.Length || values.Length < from.Length ||
            HasShiftedOverlap(from, values) || HasShiftedOverlap(to, values))
        {
            return 0;
        }
        if (progress <= 0 || progress >= 1)
        {
            (progress <= 0 ? from : to).CopyTo(values);
            return from.Length;
        }
        for (var i = 0; i < from.Length; i++)
        {
            var a = from[i];
            var b = to[i];
            values[i] = double.IsFinite(a) && double.IsFinite(b) ? double.Lerp(a, b, progress) : double.NaN;
        }
        return from.Length;
    }

    static bool HasShiftedOverlap(ReadOnlySpan<double> input, Span<double> output) =>
        input.Overlaps(output, out var offset) && offset != 0;
}
