namespace Guinevere.Charts;

/// <summary>A preprojected interval and its index in the caller's source data.</summary>
/// <param name="Start">Inclusive start time.</param>
/// <param name="End">Exclusive end time.</param>
/// <param name="SourceIndex">Original interval index.</param>
public readonly record struct FlameInterval(double Start, double End, int SourceIndex);

/// <summary>Searches one caller-owned row of sorted, nonoverlapping flame intervals.</summary>
public static class FlameRowIndex
{
    /// <summary>Checks that intervals are finite, nonempty, sorted, and disjoint. Touching edges are allowed.</summary>
    public static bool IsValid(ReadOnlySpan<FlameInterval> row)
    {
        for (var i = 0; i < row.Length; i++)
        {
            var interval = row[i];
            if (!double.IsFinite(interval.Start) || !double.IsFinite(interval.End) ||
                interval.Start >= interval.End || interval.SourceIndex < 0 ||
                (i > 0 && row[i - 1].End > interval.Start))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns the contiguous slice intersecting the finite, nonempty half-open viewport [min, max).</summary>
    /// <remarks>The row must satisfy <see cref="IsValid"/> and remain unchanged during the search.</remarks>
    public static Range VisibleRange(ReadOnlySpan<FlameInterval> row, double min, double max)
    {
        if (!double.IsFinite(min) || !double.IsFinite(max) || min >= max)
        {
            throw new ArgumentOutOfRangeException(nameof(min), "Viewport bounds must be finite and increasing.");
        }

        var low = 0;
        var high = row.Length;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (row[middle].End <= min)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        var first = low;
        high = row.Length;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (row[middle].Start < max)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return first..low;
    }
}
