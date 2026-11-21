namespace Guinevere.Charts;

/// <summary>Allocation-free transforms for Cartesian data.</summary>
public static class CartesianTransforms
{
    /// <summary>Writes increasing 1, 2, or 5 decade ticks within the range, returning the number written.</summary>
    /// <remarks>Invalid ranges, target counts below two, and unrepresentable steps produce no ticks. Output is truncated to capacity.</remarks>
    public static int GenerateTicks(ChartRange range, int targetCount, Span<double> destination)
    {
        if (!range.IsValid || targetCount < 2 || destination.IsEmpty)
        {
            return 0;
        }
        var step = TickStep(range, targetCount - 1);
        if (!(step > 0) || !double.IsFinite(step))
        {
            return 0;
        }
        var tick = Math.Ceiling(range.Min / step) * step;
        var count = 0;
        while (double.IsFinite(tick) && tick <= range.Max && count < destination.Length)
        {
            if (tick >= range.Min)
            {
                destination[count++] = tick;
            }
            var next = tick + step;
            if (!(next > tick))
            {
                break;
            }
            tick = next;
        }
        return count;
    }

    static double TickStep(ChartRange range, int intervals)
    {
        var width = range.Max - range.Min;
        var raw = double.IsFinite(width) ? width / intervals : range.Max / intervals - range.Min / intervals;
        var decade = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var fraction = raw / decade;
        return decade * (fraction switch { <= 1 => 1, <= 2 => 2, <= 5 => 5, _ => 10 });
    }

    /// <summary>Writes ordered source indices retaining endpoints and each bucket's Y extrema.</summary>
    /// <remarks>Input must be finite and nondecreasing in X. Invalid input or insufficient capacity returns zero. The output may be shorter than capacity.</remarks>
    public static int Decimate(ReadOnlySpan<ChartPoint> source, Span<int> destination)
    {
        if (source.IsEmpty || destination.IsEmpty || (source.Length > 1 && destination.Length < 2))
        {
            return 0;
        }
        if (!IsOrderedFinite(source))
        {
            return 0;
        }
        if (source.Length <= destination.Length)
        {
            for (var i = 0; i < source.Length; i++)
            {
                destination[i] = i;
            }
            return source.Length;
        }
        destination[0] = 0;
        var count = 1;
        var buckets = (destination.Length - 2) / 2;
        var interior = source.Length - 2;
        for (var b = 0; b < buckets; b++)
        {
            var start = 1 + (int)((long)b * interior / buckets);
            var end = 1 + (int)((long)(b + 1) * interior / buckets);
            var min = start;
            var max = start;
            for (var i = start + 1; i < end; i++)
            {
                if (source[i].Y < source[min].Y)
                {
                    min = i;
                }
                if (source[i].Y > source[max].Y)
                {
                    max = i;
                }
            }
            destination[count++] = Math.Min(min, max);
            if (min != max)
            {
                destination[count++] = Math.Max(min, max);
            }
        }
        destination[count++] = source.Length - 1;
        return count;
    }

    static bool IsOrderedFinite(ReadOnlySpan<ChartPoint> source)
    {
        var previous = double.NegativeInfinity;
        foreach (var point in source)
        {
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || point.X < previous)
            {
                return false;
            }
            previous = point.X;
        }
        return true;
    }

    /// <summary>Counts finite samples into equal-width bins and returns the number of bins.</summary>
    /// <remarks>Values outside the range are ignored. The upper endpoint belongs to the last bin. Invalid ranges leave bins cleared and return zero.</remarks>
    public static int Histogram(ReadOnlySpan<double> source, ChartRange range, Span<int> bins)
    {
        bins.Clear();
        if (!range.IsValid || bins.IsEmpty)
        {
            return 0;
        }
        foreach (var value in source)
        {
            if (!double.IsFinite(value) || value < range.Min || value > range.Max)
            {
                continue;
            }
            var index = Math.Min((int)(range.Normalize(value) * bins.Length), bins.Length - 1);
            bins[index]++;
        }
        return bins.Length;
    }
}
