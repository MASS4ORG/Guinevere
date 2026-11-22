using Guinevere.Charts;

namespace Guinevere.Tests.Charts;

/// <summary>Checks the interval ordering and binary-search contracts for flame chart lanes.</summary>
public sealed class FlameRowIndexTests
{
    /// <summary>Culling keeps source identities and uses half-open viewport edges.</summary>
    [Fact]
    public void VisibleRangeUsesHalfOpenEdgesAndPreservesSourceIndices()
    {
        FlameInterval[] row = [new(0, 2, 17), new(2, 4, 3), new(6, 9, 42)];
        Assert.True(FlameRowIndex.IsValid(row));
        Assert.Equal(1..2, FlameRowIndex.VisibleRange(row, 2, 6));
        Assert.Equal(3, row[FlameRowIndex.VisibleRange(row, 2, 6)][0].SourceIndex);
        Assert.Equal(2..2, FlameRowIndex.VisibleRange(row, 4, 6));
        Assert.Equal(0..0, FlameRowIndex.VisibleRange(row, -2, 0));
        Assert.Equal(3..3, FlameRowIndex.VisibleRange(row, 9, 10));
        Assert.Equal(0..0, FlameRowIndex.VisibleRange([], 0, 1));
    }

    /// <summary>Rows reject invalid intervals, overlapping intervals, and invalid source indices.</summary>
    [Fact]
    public void ValidationRejectsOverlapInvalidBoundsAndSourceIndices()
    {
        Assert.False(FlameRowIndex.IsValid([new(0, 2, 0), new(1, 3, 1)]));
        Assert.False(FlameRowIndex.IsValid([new(1, 1, 0)]));
        Assert.False(FlameRowIndex.IsValid([new(double.NaN, 2, 0)]));
        Assert.False(FlameRowIndex.IsValid([new(0, double.PositiveInfinity, 0)]));
        Assert.False(FlameRowIndex.IsValid([new(0, 1, -1)]));
        Assert.Throws<ArgumentOutOfRangeException>(static () => FlameRowIndex.VisibleRange([], 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(static () => FlameRowIndex.VisibleRange([], double.NaN, 1));
    }
}
