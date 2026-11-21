using Guinevere.Charts;

namespace Guinevere.Tests.Charts;

/// <summary>Checks circular slice generation, numeric edge cases and pointer mapping.</summary>
public sealed class PieChartTests
{
    /// <summary>Valid slices retain the index of their input weights.</summary>
    [Fact]
    public void CreateSegmentsPreservesSourceIndicesAndIgnoresInvalidWeights()
    {
        double[] weights = [2, 0, double.NaN, -1, 1];
        Span<PieSegment> result = stackalloc PieSegment[2];

        var count = PieChart.CreateSegments(weights, result);

        Assert.Equal(2, count);
        Assert.Equal(0, result[0].SourceIndex);
        Assert.Equal(4, result[1].SourceIndex);
        Assert.Equal(Math.PI * 4 / 3, result[0].SweepAngle, 10);
    }

    /// <summary>Large positive weights normalize without overflow and report the required capacity.</summary>
    [Fact]
    public void CreateSegmentsHandlesOverflowAndBoundedDestination()
    {
        double[] weights = [double.MaxValue, double.MaxValue];
        Span<PieSegment> result = stackalloc PieSegment[1];

        var count = PieChart.CreateSegments(weights, result, Math.PI / 2);

        Assert.Equal(2, count);
        Assert.Equal(Math.PI, result[0].SweepAngle, 10);
        Assert.Equal(Math.PI / 2, result[0].StartAngle, 10);
    }

    /// <summary>Hits in visible slices return their index and hits in the hole or gaps miss.</summary>
    [Fact]
    public void HitTestReturnsSourceIndexAndRespectsGapAndHole()
    {
        double[] weights = [1, 1, 1];

        Assert.Equal(0, PieChart.HitTest(weights, 1, 0));
        Assert.Equal(-1, PieChart.HitTest(weights, 0, 0, holeRadius: 0.2));
        Assert.Equal(-1, PieChart.HitTest(weights, Math.Cos(2 * Math.PI / 3), Math.Sin(2 * Math.PI / 3),
            angularGap: 0.1));
    }

    /// <summary>Uniformly tiny weights form visible slices when normalized together.</summary>
    [Fact]
    public void TinyPositiveWeightsRemainRepresented()
    {
        double[] weights = [double.Epsilon, double.Epsilon];
        Span<PieSegment> result = stackalloc PieSegment[2];

        Assert.Equal(2, PieChart.CreateSegments(weights, result));
        Assert.Equal(Math.PI, result[0].SweepAngle, 10);
    }

    /// <summary>Empty weights leave no geometry and an invalid hole is rejected.</summary>
    [Fact]
    public void HandlesEmptySlicesAndRejectsInvalidHole()
    {
        Assert.Equal(0, PieChart.CreateSegments([0, -1, double.PositiveInfinity], []));
        Assert.Equal(-1, PieChart.HitTest([], 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(static () => PieChart.HitTest([1], 0, 0, holeRadius: 1));
    }

    /// <summary>An exploded slice is hittable at its new radius.</summary>
    [Fact]
    public void ExplosionMovesTheHitRegion()
    {
        Span<PieSegment> segments = stackalloc PieSegment[2];
        Assert.Equal(2, PieChart.CreateSegments([1, 1], segments));
        var area = new Rect(20, 20, 100, 100);
        var pointer = new Vector2(70, 125);
        Assert.Equal(-1, PieChart.HitTest(area, segments, pointer));
        Assert.Equal(0, PieChart.HitTest(area, segments, pointer, selection: new PieSelection(0, 10)));
    }
}
