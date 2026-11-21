namespace Guinevere.Tests;

/// <summary>Checks that the allocation probe stays strict about repeated allocation and tolerates one blip.</summary>
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

    /// <summary>List growth that happens on two of the measured passes, as doubling does, is reported.</summary>
    [Fact]
    public void AmortizedListGrowthIsReported()
    {
        // After the warm-up the list holds 4 items, so the measured adds grow it at items 5 and 9.
        var list = new List<int> { 1, 2, 3 };

        Assert.True(AllocationProbe.Steady(() => list.Add(1)) > 0);
        Assert.Equal(9, list.Count);
    }

    /// <summary>A single pass that allocates is tolerated, while two such passes are not.</summary>
    [Fact]
    public void OneTransientBlipIsIgnoredAndTwoAreNot()
    {
        var calls = 0;
        var one = AllocationProbe.Steady(() =>
        {
            // Call 1 is the warm-up, so call 4 is the third measured pass.
            if (++calls == 4)
            {
                _sink = new byte[1_000];
            }
        });
        calls = 0;
        var two = AllocationProbe.Steady(() =>
        {
            if (++calls is 3 or 5)
            {
                _sink = new byte[1_000];
            }
        });

        Assert.Equal(0, one);
        Assert.True(two >= 1_000);
    }
}
