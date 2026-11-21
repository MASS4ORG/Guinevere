using Guinevere.Charts;

namespace Guinevere.Tests.Charts;

/// <summary>Checks the numeric range, tick, decimation and histogram contracts.</summary>
public sealed class CartesianTransformsTests
{
    /// <summary>Ranges with an overflowing width still support unit coordinate transforms.</summary>
    [Fact]
    public void RangeHandlesOppositeLargeEndpointsAndInvalidDefault()
    {
        var range = new ChartRange(-double.MaxValue, double.MaxValue);
        Assert.True(range.IsValid);
        Assert.Equal(0.5, range.Normalize(0), 12);
        Assert.Equal(0, range.Denormalize(0.5), 12);
        Assert.Equal(double.MinValue, range.Denormalize(0));
        Assert.Equal(double.MaxValue, range.Denormalize(1));
        Assert.Equal(double.PositiveInfinity, range.Denormalize(2));
        Assert.False(default(ChartRange).IsValid);
        Assert.True(double.IsNaN(default(ChartRange).Normalize(1)));
        Assert.True(double.IsNaN(default(ChartRange).Denormalize(1)));
    }

    /// <summary>Tick output remains inside the range and destination bounds.</summary>
    [Fact]
    public void TicksAreBoundedAndTruncated()
    {
        Span<double> ticks = stackalloc double[3];
        var count = CartesianTransforms.GenerateTicks(new ChartRange(-1, 9), 6, ticks);
        Assert.Equal(3, count);
        Assert.Equal(0, ticks[0], 12);
        Assert.Equal(2, ticks[1], 12);
        Assert.Equal(4, ticks[2], 12);
        Assert.Equal(0, CartesianTransforms.GenerateTicks(default, 6, ticks));
        Assert.Equal(0, CartesianTransforms.GenerateTicks(new ChartRange(0, double.Epsilon), 3, ticks));
        Assert.Equal(0, CartesianTransforms.GenerateTicks(new ChartRange(0, 3), 1, ticks));
        Assert.Equal(0, CartesianTransforms.GenerateTicks(new ChartRange(-double.MaxValue, double.MaxValue), 2, ticks));
    }

    /// <summary>Decimation retains endpoint and spike indices in source order.</summary>
    [Fact]
    public void DecimationPreservesBucketExtremaInSourceOrder()
    {
        ChartPoint[] points = [new(0, 0), new(1, 9), new(2, -4), new(3, 2), new(4, 8), new(5, 0)];
        Span<int> indices = stackalloc int[4];
        Assert.Equal(4, CartesianTransforms.Decimate(points, indices));
        Assert.Equal(new[] { 0, 1, 2, 5 }, indices.ToArray());
        points[3] = new(-1, 2);
        Assert.Equal(0, CartesianTransforms.Decimate(points, indices));
        Assert.Equal(0, CartesianTransforms.Decimate(points, indices[..1]));
        Assert.Equal(0, CartesianTransforms.Decimate([], indices));
        Assert.Equal(2, CartesianTransforms.Decimate(points.AsSpan(0, 2), indices));
        Assert.Equal(0, indices[0]);
        Assert.Equal(1, indices[1]);
    }

    /// <summary>The upper endpoint belongs to the last bin and outliers are discarded.</summary>
    [Fact]
    public void HistogramIgnoresOutliersAndIncludesUpperEndpoint()
    {
        double[] values = [-1, 0, 0.5, 1, 2, double.NaN, double.PositiveInfinity];
        Span<int> bins = stackalloc int[2];
        Assert.Equal(2, CartesianTransforms.Histogram(values, new ChartRange(0, 1), bins));
        Assert.Equal(new[] { 1, 2 }, bins.ToArray());
        Assert.Equal(0, CartesianTransforms.Histogram(values, default, bins));
        Assert.Equal(new[] { 0, 0 }, bins.ToArray());
    }
}
