namespace Guinevere.Tests;

/// <summary>Measures managed allocation of a repeatable workload once its steady state is reached.</summary>
public static class AllocationProbe
{
    /// <summary>
    /// Runs the workload once to warm it up, then measures passes until two consecutive ones agree within
    /// <paramref name="tolerance"/> bytes and returns the smaller of the two. One-off growth and runtime blips settle
    /// before the pair agrees, while a per-call allocation repeats and shows in the result; a workload that never
    /// agrees within <paramref name="maxPasses"/> reports its smallest pass.
    /// </summary>
    public static long Steady(Action workload, int maxPasses = 12, long tolerance = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPasses, 2);
        workload();
        var previous = Measure(workload);
        var smallest = previous;
        for (var pass = 1; pass < maxPasses; pass++)
        {
            var current = Measure(workload);
            if (Math.Abs(current - previous) <= tolerance)
            {
                return Math.Min(current, previous);
            }

            smallest = Math.Min(smallest, current);
            previous = current;
        }

        return smallest;
    }

    /// <summary>
    /// Bytes one call of <paramref name="frame"/> allocates in steady state: it warms up for
    /// <paramref name="warmFrames"/> calls, then averages <paramref name="measuredFrames"/> calls per pass until two
    /// passes agree within 64 bytes a frame.
    /// </summary>
    public static long BytesPerFrame(Action frame, int warmFrames = 60, int measuredFrames = 300)
    {
        for (var i = 0; i < warmFrames; i++)
        {
            frame();
        }

        GC.Collect();
        var total = Steady(() =>
        {
            for (var i = 0; i < measuredFrames; i++)
            {
                frame();
            }
        }, tolerance: measuredFrames * 64L);
        return total / measuredFrames;
    }

    static long Measure(Action workload)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        workload();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
