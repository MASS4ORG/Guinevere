namespace Guinevere.Tests;

/// <summary>Measures managed allocation of a repeatable workload while tolerating one transient blip.</summary>
public static class AllocationProbe
{
    /// <summary>
    /// Runs the workload once to warm it up, then measures every one of <paramref name="passes"/> runs and returns the
    /// second-largest delta, so a single pass hit by transient runtime allocation is ignored. Two or more non-zero
    /// passes, such as amortized growth or a per-call allocation, make the result non-zero.
    /// </summary>
    public static long Steady(Action workload, int passes = 5)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(passes, 2);
        workload();
        var deltas = new long[passes];
        for (var pass = 0; pass < passes; pass++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            workload();
            deltas[pass] = GC.GetAllocatedBytesForCurrentThread() - before;
        }

        Array.Sort(deltas);
        return deltas[^2];
    }
}
