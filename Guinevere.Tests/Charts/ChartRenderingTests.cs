using Guinevere.Charts;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Charts;

/// <summary>Checks the chart package through Guinevere's drawing and input coordinates.</summary>
public sealed class ChartRenderingTests
{
    /// <summary>A donut draws each arc and its hole in the same positions used for hit testing.</summary>
    [Fact]
    public void DonutDrawingAgreesWithHitTesting()
    {
        using var harness = new FrameHarness(120, 120);
        var area = new Rect(10, 10, 100, 100);
        var segments = new PieSegment[2];
        Assert.Equal(2, PieChart.CreateSegments([1, 0, 1], segments, angularGap: .2));
        Color[] palette = [Color.Red, Color.White, Color.Blue];
        harness.Frame(gui => PieChart.Draw(gui, area, segments, palette, .4f));
        using var snapshot = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(snapshot);

        Assert.Equal(SKColors.Red, pixels.GetPixel(60, 95));
        Assert.Equal(SKColors.Blue, pixels.GetPixel(60, 25));
        Assert.Equal(0, pixels.GetPixel(60, 60).Alpha);
        Assert.Equal(0, pixels.GetPixel(95, 60).Alpha);
        Assert.Equal(0, PieChart.HitTest(area, segments, new Vector2(60, 95), .4));
        Assert.Equal(2, PieChart.HitTest(area, segments, new Vector2(60, 25), .4));
        Assert.Equal(-1, PieChart.HitTest(area, segments, new Vector2(60, 60), .4));
        Assert.Equal(-1, PieChart.HitTest(area, segments, new Vector2(95, 60), .4));
    }

    /// <summary>Clipping and gaps preserve the visible portion of a line without connecting missing samples.</summary>
    [Fact]
    public void CartesianLineClipsAndBreaksAtMissingValues()
    {
        using var harness = new FrameHarness(120, 120);
        var plot = new CartesianPlot(new Rect(10, 10, 100, 100), new ChartRange(0, 10), new ChartRange(0, 10));
        ChartPoint[] points = [new(-2, 8), new(12, 8), new(4, double.NaN), new(6, 2)];
        harness.Frame(gui => CartesianChart.DrawLine(gui, plot, points, Color.Red, 2));
        using var snapshot = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(snapshot);

        Assert.Equal(SKColors.Red, pixels.GetPixel(60, 30));
        Assert.Equal(0, pixels.GetPixel(5, 30).Alpha);
        Assert.Equal(0, pixels.GetPixel(115, 30).Alpha);
        Assert.Equal(0, pixels.GetPixel(80, 79).Alpha);
    }

    /// <summary>Bars and bubbles identify the topmost source item and miss invalid samples.</summary>
    [Fact]
    public void CartesianHitsKeepSourceIndices()
    {
        var plot = new CartesianPlot(new Rect(0, 0, 100, 100), new ChartRange(0, 10), new ChartRange(-5, 5));
        ChartPoint[] points = [new(1, double.NaN), new(5, -20), new(5, -2), new(7, 3)];
        Assert.Equal(2, CartesianChart.HitBars(plot, points, new Vector2(50, 60), 10));
        Assert.Equal(1, CartesianChart.HitBars(plot, points, new Vector2(50, 90), 10));
        Assert.Equal(-1, CartesianChart.HitBars(plot, points, new Vector2(50, 100), 10));
        Assert.Equal(3, CartesianChart.HitPoints(plot, points, new Vector2(77, 20), radii: [1, 1, 1, 8]));
        Assert.Equal(-1, CartesianChart.HitPoints(plot, points, new Vector2(77, 20)));
        Assert.Equal(3, CartesianChart.HitLineSample(plot, points, new Vector2(75, 20), indices: [0, 3]));
        Assert.Equal(-1, CartesianChart.HitLineSample(plot, points, new Vector2(75, 20), indices: [0, 2]));
        Assert.True(plot.TryUnproject(new Vector2(50, 75), out var data));
        Assert.Equal(new ChartPoint(5, -2.5), data);
        Assert.True(plot.TryProject(data, out var screen));
        Assert.Equal(new Vector2(50, 75), screen);
        Assert.False(default(CartesianPlot).TryProject(data, out _));
    }

    /// <summary>Pie sectors, Cartesian series and radar outlines record commands without warmed allocations.</summary>
    [Fact]
    public void WarmDrawingRecordsCommandsWithoutAllocating()
    {
        using var surface = SKSurface.Create(new SKImageInfo(160, 160));
        var gui = new TestableGui { Input = new ScriptedInputHandler() };
        gui.SetScreenRect(160, 160);
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        var commands = gui.RootNode!.DrawList;
        commands.EnsureCapacity(32);
        var area = new Rect(10, 10, 100, 100);
        var plot = new CartesianPlot(area, new ChartRange(0, 1), new ChartRange(0, 1));
        ChartPoint[] points = [new(.2, .2), new(.5, .6), new(.8, .3)];
        var slices = new PieSegment[2];
        PieChart.CreateSegments([1, 1], slices);
        Color[] colors = [Color.Red, Color.Blue];
        var axes = new Vector2[3];
        RadarProjection.CreateAxes(axes);
        double[] xTicks = [.25, .5, .75];
        double[] yTicks = [.5];
        var axisColor = Color.White;
        var gridColor = Color.Gray;

        void Record()
        {
            commands.Clear();
            PieChart.Draw(gui, area, slices, colors);
            CartesianChart.DrawGrid(gui, plot, xTicks, yTicks, axisColor, gridColor);
            CartesianChart.DrawBars(gui, plot, points, colors[0], 10);
            CartesianChart.DrawPoints(gui, plot, points, colors[1]);
            CartesianChart.DrawLine(gui, plot, points, axisColor);
            RadarChart.DrawGrid(gui, area, axes, 3, gridColor);
            RadarChart.DrawSeries(gui, area, axes, axisColor);
        }

        Assert.Equal(0, AllocationProbe.Steady(Record));
        Assert.Equal(30, commands.PrimitiveCount);
        gui.EndFrame();
    }

    /// <summary>The frame-history band uses right-aligned slots and returns source indices.</summary>
    [Fact]
    public void BandKeepsUnusedSlotsOnTheLeft()
    {
        var band = new BarBand(new Rect(10, 15, 120, 72), count: 3, minimumSlots: 12);
        Assert.Equal(new Rect(100, 15, 10, 72), band.Slot(0));
        Assert.Equal(0, band.HitTest(new Vector2(100, 18)));
        Assert.Equal(2, band.HitTest(new Vector2(129, 20)));
        Assert.Equal(-1, band.HitTest(new Vector2(98, 20)));
        Assert.Equal(-1, band.HitTest(new Vector2(120, 90)));
        Assert.Equal(default, band.Slot(3));
    }

    /// <summary>History bars clip to the band and return the exact overlay bounds.</summary>
    [Fact]
    public void BandDrawsClippedBarsAndSkipsInvalidItems()
    {
        using var harness = new FrameHarness(120, 100);
        harness.Frame(static gui =>
        {
            var band = new BarBand(new Rect(10, 10, 80, 40), 4);
            if (gui.Pass == Pass.Pass1Build)
            {
                Assert.False(band.DrawBar(gui, 1, 10, 20, Color.Red, out _));
                return;
            }
            Assert.True(band.DrawBar(gui, 1, 40, 20, Color.Red, out var actual, gap: 2, topInset: 4));
            Assert.Equal(new Rect(31, 14, 18, 36), actual);
            Assert.False(band.DrawBar(gui, 4, 10, 20, Color.Red, out _));
            Assert.False(band.DrawBar(gui, 1, double.NaN, 20, Color.Red, out _));
            Assert.Equal(1, gui.CurrentNode.DrawList.PrimitiveCount);
        });
    }

    /// <summary>Panning translates the plot in screen coordinates and zooming holds the anchor fixed.</summary>
    [Fact]
    public void PanAndZoomUseScreenSpaceCoordinates()
    {
        var plot = new CartesianPlot(new Rect(20, 10, 200, 100), new ChartRange(0, 20), new ChartRange(-10, 10));
        var anchor = new Vector2(70, 35);
        Assert.True(plot.TryUnproject(anchor, out var value));
        var pan = plot.Pan(new Vector2(10, -10));
        Assert.Equal(new ChartRange(-1, 19), pan.X);
        Assert.Equal(new ChartRange(-12, 8), pan.Y);
        Assert.True(pan.TryProject(value, out var moved));
        Assert.Equal(new Vector2(80, 25), moved);
        var zoom = plot.Zoom(anchor, 2, 1);
        Assert.Equal(new ChartRange(2.5, 12.5), zoom.X);
        Assert.Equal(plot.Y, zoom.Y);
        Assert.True(zoom.TryProject(value, out var fixedPoint));
        Assert.Equal(anchor, fixedPoint);
        Assert.Equal(plot, plot.Zoom(anchor, double.NaN, 1));
        Assert.Equal(plot, plot.Zoom(new Vector2(-5, -5), 2, 2));
        Assert.Equal(plot, plot.Pan(new Vector2(float.NaN, 0)));
    }

    /// <summary>Warmed chart transforms and hit testing reuse the caller's storage.</summary>
    [Fact]
    public void PreparedChartsDoNotAllocateManagedMemory()
    {
        var points = new ChartPoint[100_000];
        for (var i = 0; i < points.Length; i++)
        {
            points[i] = new ChartPoint(i, i % 41);
        }
        var indices = new int[512];
        var plot = new CartesianPlot(new Rect(0, 0, 500, 300), new ChartRange(0, points.Length), new ChartRange(0, 40));
        double[] weights = [1, 5, 2, 0, 3];
        var segments = new PieSegment[weights.Length];
        var ranges = new[] { plot.Y, plot.Y, plot.Y };
        double[] values = [10, 20, 30];
        var vertices = new Vector2[3];
        var bins = new int[12];
        var ticks = new double[12];

        void Update()
        {
            CartesianTransforms.Decimate(points, indices);
            CartesianTransforms.Histogram(values, plot.Y, bins);
            CartesianTransforms.GenerateTicks(plot.Y, ticks.Length, ticks);
            CartesianChart.HitPoints(plot, points, new Vector2(30, 60));
            var slices = PieChart.CreateSegments(weights, segments);
            PieChart.HitTest(plot.Area, segments.AsSpan(0, slices), new Vector2(240, 180));
            RadarProjection.Project(ranges, values, vertices);
            RadarChart.HitSeries(plot.Area, vertices, new Vector2(220, 80));
        }

        Assert.Equal(0, AllocationProbe.Steady(Update));
    }
}
