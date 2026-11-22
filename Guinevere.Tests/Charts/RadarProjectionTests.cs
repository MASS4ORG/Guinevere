using Guinevere.Charts;

namespace Guinevere.Tests.Charts;

/// <summary>Checks per-dimension scaling and radar hit testing.</summary>
public sealed class RadarProjectionTests
{
    /// <summary>Each dimension uses its own scale and clamps data to the chart radius.</summary>
    [Fact]
    public void ProjectsIndependentlyScaledAxesAndClipsOutOfRangeValues()
    {
        ChartRange[] ranges = [new(0, 10), new(100, 200), new(-1, 1)];
        double[] values = [5, 250, 0];
        var output = new Vector2[3];

        Assert.True(RadarProjection.Project(ranges, values, output));
        Assert.Equal(0.5f, output[0].Length(), 5);
        Assert.Equal(1f, output[1].Length(), 5);
        Assert.Equal(0.5f, output[2].Length(), 5);
    }

    /// <summary>Missing points leave gaps at their source indices.</summary>
    [Fact]
    public void MissingValuesAndInvalidRangesKeepAxisIdentity()
    {
        ChartRange[] ranges = [new(0, 1), new(0, 1), new(0, 0), new(0, 1)];
        double[] values = [0.25, double.NaN, 0.5, 0.75];
        var output = new Vector2[4];

        Assert.False(RadarProjection.Project(ranges, values, output));
        Assert.True(float.IsNaN(output[1].X));
        Assert.True(float.IsNaN(output[2].X));
        Assert.Equal(3, RadarProjection.HitTestVertex(output[3], output, 0.001f));
    }

    /// <summary>Axis endpoint hits use chart dimension order and the caller's tolerance.</summary>
    [Fact]
    public void AxisHitTestingUsesStableIndicesAndHonorsTolerance()
    {
        var point = RadarProjection.AxisVertex(2, 5);

        Assert.Equal(2, RadarProjection.HitTestAxis(point, 5, 0.001f));
        Assert.Equal(-1, RadarProjection.HitTestAxis(Vector2.Zero, 5, 0.1f));
    }

    /// <summary>Projection rejects a dataset with fewer than three dimensions.</summary>
    [Fact]
    public void RequiresAtLeastThreeDimensions()
    {
        var error = Assert.Throws<ArgumentException>(static () =>
            RadarProjection.Project([], [], []));
        Assert.Contains("at least three", error.Message);
    }

    /// <summary>Nonfinite orientation and tolerance are rejected.</summary>
    [Fact]
    public void RejectsInvalidAngleAndTolerance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(static () => RadarProjection.AxisVertex(0, 3, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(
            static () => RadarProjection.HitTestVertex(Vector2.Zero, [], float.PositiveInfinity));
    }
}
