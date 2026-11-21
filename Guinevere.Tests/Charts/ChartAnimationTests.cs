using Guinevere.Charts;

namespace Guinevere.Tests.Charts;

/// <summary>Checks stable source order and edge cases for allocation-free chart interpolation.</summary>
public sealed class ChartAnimationTests
{
    /// <summary>Mixes finite values, retains missing values between endpoints and supports in-place updates.</summary>
    [Fact]
    public void InterpolatesIntoCallerBuffers()
    {
        double[] start = [double.MinValue, 2, double.NaN];
        double[] end = [double.MaxValue, 8, 4];
        var values = new double[3];
        Assert.Equal(3, ChartAnimation.Interpolate(start, end, .5, values));
        Assert.Equal(0, values[0]);
        Assert.Equal(5, values[1]);
        Assert.True(double.IsNaN(values[2]));
        Assert.Equal(3, ChartAnimation.Interpolate(start, end, -1, values));
        Assert.Equal(start, values);
        Assert.Equal(3, ChartAnimation.Interpolate(start, end, 2, start));
        Assert.Equal(end, start);
    }

    /// <summary>Rejects mismatched inputs, short outputs and shifted aliases.</summary>
    [Fact]
    public void RejectsInvalidBuffersAndProgress()
    {
        var values = new double[4];
        Assert.Equal(0, ChartAnimation.Interpolate([1, 2, 3], [2, 3], .5, values));
        Assert.Equal(0, ChartAnimation.Interpolate([1, 2, 3], [2, 3, 4], .5, values.AsSpan(0, 1)));
        Assert.Equal(0, ChartAnimation.Interpolate([1], [2], double.NaN, values));
        Assert.Equal(0, ChartAnimation.Interpolate(values.AsSpan(0, 3), [2, 3, 4], .5, values.AsSpan(1)));
    }

    /// <summary>Point transitions retain source order and can reuse the starting buffer.</summary>
    [Fact]
    public void InterpolatesPointsAndRetainsGaps()
    {
        ChartPoint[] from = [new(0, 2), new(4, double.NaN)];
        ChartPoint[] to = [new(2, -4), new(4, 7)];
        Assert.Equal(0, ChartAnimation.InterpolatePoints(from, to, double.NaN, from));
        Assert.Equal(2, ChartAnimation.InterpolatePoints(from, to, .5, from));
        Assert.Equal(new ChartPoint(1, -1), from[0]);
        Assert.True(double.IsNaN(from[1].Y));
        Assert.Equal(2, ChartAnimation.InterpolatePoints(from, to, 1, from));
        Assert.Equal(to, from);
    }
}
