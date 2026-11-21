namespace Guinevere.Tests;

/// <summary>Checks that the allocation probe stays strict about repeated allocation and waits for a steady state.</summary>
public class AllocationProbeTests
{
    static object? _sink;

    /// <summary>A workload that allocates nothing measures zero.</summary>
    [Fact]
    public void AnAllocationFreeWorkloadMeasuresZero()
    {
        var sum = 0;

        Assert.Equal(0, AllocationProbe.Steady(() => sum += 1));
        Assert.True(sum > 0);
    }

    /// <summary>An allocation in every call shows in the result.</summary>
    [Fact]
    public void APerCallAllocationIsReported()
    {
        Assert.True(AllocationProbe.Steady(static () => _sink = new byte[1_000]) >= 1_000);
    }

    /// <summary>List growth is paid once and settles before two passes agree, so it does not show.</summary>
    [Fact]
    public void OneOffListGrowthSettlesBeforeTheResult()
    {
        // After the warm-up the list holds 4 items, so the first measured add grows it and the next ones do not.
        var list = new List<int> { 1, 2, 3 };

        Assert.Equal(0, AllocationProbe.Steady(() => list.Add(1)));
        Assert.True(list.Count >= 7);
    }

    /// <summary>A pass hit by a transient allocation is not taken as the steady value.</summary>
    [Fact]
    public void OneTransientBlipIsIgnored()
    {
        var calls = 0;
        var result = AllocationProbe.Steady(() =>
        {
            // Call 1 is the warm-up, so call 3 is the second measured pass.
            if (++calls == 3)
            {
                _sink = new byte[1_000];
            }
        });

        Assert.Equal(0, result);
        Assert.True(calls >= 5);
    }

    /// <summary>Passes within the tolerance agree, so a small variation does not keep the probe measuring.</summary>
    [Fact]
    public void PassesWithinTheToleranceAgree()
    {
        var calls = 0;
        var result = AllocationProbe.Steady(() => _sink = new byte[1_000 + (++calls % 2 * 10)], tolerance: 100);

        Assert.InRange(result, 1_000, 1_100);
        Assert.True(calls <= 4);
    }

    /// <summary>A workload that never settles reports its smallest pass after the pass limit.</summary>
    [Fact]
    public void AWorkloadThatNeverSettlesReportsItsSmallestPass()
    {
        var calls = 0;
        var result = AllocationProbe.Steady(() => _sink = new byte[++calls * 100], maxPasses: 4);

        Assert.InRange(result, 200, 300);
        Assert.Equal(5, calls);
    }

    /// <summary>A frame that allocates nothing measures zero bytes a frame after its warm-up.</summary>
    [Fact]
    public void AnAllocationFreeFrameMeasuresZeroBytes()
    {
        var frames = 0;

        Assert.Equal(0, AllocationProbe.BytesPerFrame(() => frames++, warmFrames: 5, measuredFrames: 10));
        Assert.True(frames >= 25);
    }

    /// <summary>A frame that allocates every time reports its size, averaged over the measured frames.</summary>
    [Fact]
    public void AFrameThatAllocatesEveryTimeReportsItsSize()
    {
        var bytes = AllocationProbe.BytesPerFrame(static () => _sink = new byte[1_000], warmFrames: 5,
            measuredFrames: 10);

        Assert.InRange(bytes, 1_000, 1_100);
    }

    /// <summary>A probe needs at least two passes to compare.</summary>
    [Fact]
    public void FewerThanTwoPassesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(static () => AllocationProbe.Steady(static () => { }, maxPasses: 1));
    }
}
