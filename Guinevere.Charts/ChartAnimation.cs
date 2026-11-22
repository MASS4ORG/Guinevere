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
            values[i] = InterpolateValue(from[i], to[i], progress);
        }
        return from.Length;
    }

    /// <summary>Interpolates both coordinates of matching samples. A coordinate missing at either endpoint stays missing during the transition.</summary>
    public static int InterpolatePoints(ReadOnlySpan<ChartPoint> from, ReadOnlySpan<ChartPoint> to, double progress,
        Span<ChartPoint> points)
    {
        if (!double.IsFinite(progress) || from.Length != to.Length || points.Length < from.Length ||
            HasShiftedOverlap(from, points) || HasShiftedOverlap(to, points))
        {
            return 0;
        }
        if (progress <= 0 || progress >= 1)
        {
            (progress <= 0 ? from : to).CopyTo(points);
            return from.Length;
        }
        for (var i = 0; i < from.Length; i++)
        {
            points[i] = new ChartPoint(InterpolateValue(from[i].X, to[i].X, progress),
                InterpolateValue(from[i].Y, to[i].Y, progress));
        }
        return from.Length;
    }

    static double InterpolateValue(double from, double to, double progress) =>
        double.IsFinite(from) && double.IsFinite(to) ? double.Lerp(from, to, progress) : double.NaN;

    static bool HasShiftedOverlap<T>(ReadOnlySpan<T> input, Span<T> output) =>
        input.Overlaps(output, out var offset) && offset != 0;
}
