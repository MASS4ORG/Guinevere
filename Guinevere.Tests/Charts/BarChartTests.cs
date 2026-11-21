using Guinevere.Charts;

namespace Guinevere.Tests.Charts;

/// <summary>Checks prepared grouped and stacked bar geometry.</summary>
public sealed class BarChartTests
{
    /// <summary>Both orientations preserve independent stacks, group positions and source identity.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProjectsGroupedStacks(bool horizontal)
    {
        var plot = new CartesianPlot(new Rect(0, 0, 100, 100), new ChartRange(0, 10), new ChartRange(0, 10));
        var output = new BarChartRect[4];
        Assert.Equal(4, BarChart.Project(plot, [5], [2, -1, 1, 2], [0, 0, 1, 1], output, 20, 5, horizontal));
        Assert.Equal(new BarChartRect(horizontal ? new Rect(50, 40, 20, 10) : new Rect(40, 30, 10, 20), 0, 0), output[0]);
        Assert.Equal(new BarChartRect(horizontal ? new Rect(40, 40, 10, 10) : new Rect(40, 50, 10, 10), 0, 1), output[1]);
        Assert.Equal(new BarChartRect(horizontal ? new Rect(60, 50, 20, 10) : new Rect(50, 20, 10, 20), 0, 3), output[3]);
        Assert.Equal(1, BarChart.HitTest(output, horizontal ? new Vector2(45, 45) : new Vector2(45, 55)));
    }

    /// <summary>Invalid configuration does not write, and invalid values do not affect later segments.</summary>
    [Fact]
    public void ValidatesCapacityAndSkipsInvalidValues()
    {
        var plot = new CartesianPlot(new Rect(0, 0, 100, 100), new ChartRange(0, 10), new ChartRange(0, 10));
        var output = new BarChartRect[3];
        Assert.Equal(0, BarChart.Project(plot, [5], [1, 2, 3], [0, 2, 2], output, 10));
        Assert.Equal(0, BarChart.Project(plot, [5], [1, 2, 3], [0, 0, 0], output.AsSpan(0, 2), 10));
        Assert.Equal(2, BarChart.Project(plot, [5], [2, double.NaN, 3], [0, 0, 0], output, 10));
        Assert.Equal(2, output[1].Series);
        Assert.Equal(new Rect(45, 50, 10, 30), output[1].Bounds);
    }
}
