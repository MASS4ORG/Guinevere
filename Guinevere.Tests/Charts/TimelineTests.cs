using Guinevere.Charts;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Charts;

/// <summary>Timeline clipping, validation and hit ordering.</summary>
public sealed class TimelineTests
{
    static readonly TimelinePlot Plot = new(new Rect(10, 20, 100, 25), new ChartRange(0, 10), 10);

    /// <summary>Intervals and lane rectangles remain within the plot boundaries.</summary>
    [Fact]
    public void ClipsIntervalsAndPartialRows()
    {
        Assert.True(Plot.TryProject(-5, 5, 0, out var left));
        Assert.Equal(new Rect(10, 20, 50, 10), left);
        Assert.True(Plot.TryProject(9, 12, 2, out var right));
        Assert.Equal(new Rect(100, 40, 10, 5), right);
        Assert.False(Plot.TryProject(-2, 0, 0, out _));
        Assert.False(Plot.TryProject(10, 12, 0, out _));
        Assert.False(Plot.TryProject(1, 2, 3, out _));
        Assert.True(Plot.TryProject(0, 0, 0, out var instant));
        Assert.Equal(new Rect(10, 20, 1, 10), instant);
    }

    /// <summary>Invalid intervals are discarded and valid spacing leaves a visible gap.</summary>
    [Fact]
    public void RejectsInvalidInputsAndAppliesGaps()
    {
        Assert.False(Plot.TryProject(double.NaN, 3, 0, out _));
        Assert.False(Plot.TryProject(3, 2, 0, out _));
        Assert.False(Plot.TryProject(1, 2, -1, out _));
        Assert.False(Plot.TryProject(1, 2, 0, out _, rowGap: float.NaN));
        Assert.False(new TimelinePlot(new Rect(0, 0, 10, 10), new ChartRange(1, 1), 10)
            .TryProject(1, 2, 0, out _));
        Assert.True(Plot.TryProject(1, 2, 0, out var bounds, horizontalGap: 2, rowGap: 3));
        Assert.Equal(new Rect(20, 20, 8, 7), bounds);
    }

    /// <summary>Overlapping timeline hits select the topmost interval.</summary>
    [Fact]
    public void HitTestUsesLastDrawnIndexAndWiderNarrowHits()
    {
        TimelineSpan[] spans = [new(1, 1.01, 0, Color.Red), new(1, 1.01, 0, Color.Blue)];
        Assert.Equal(-1, TimelineChart.HitTest(Plot, spans, new Vector2(22, 21)));
        Assert.Equal(1, TimelineChart.HitTest(Plot, spans, new Vector2(22, 21), minimumWidth: 3));
        Assert.Equal(-1, TimelineChart.HitTest(Plot, spans, new Vector2(23, 21), minimumWidth: 3));
        Assert.Equal(-1, TimelineChart.HitTest(Plot, spans, new Vector2(float.NaN, 21)));
    }

    /// <summary>Drawing only records the visible intervals during the render pass.</summary>
    [Fact]
    public void DrawRecordsOnlyVisibleIntervals()
    {
        using var harness = new FrameHarness();
        TimelineSpan[] spans = [new(1, 2, 0, Color.Red), new(4, 8, 1, Color.Blue), new(0, 1, 3, Color.Blue)];
        harness.Frame(gui =>
        {
            TimelineChart.Draw(gui, Plot, spans);
            Assert.Equal(gui.Pass == Pass.Pass2Render ? 2 : 0, gui.CurrentNode.DrawList.PrimitiveCount);
        });
    }
}
