using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>Verifies executed command counts and optional timing across repeated and empty frames.</summary>
public class RenderDiagnosticsTests
{
    /// <summary>Totals distinguish primitives, mutable shapes, clips and opaque callbacks.</summary>
    [Fact]
    public void CountsExecutedCommandsAndResetsAfterClear()
    {
        using var surface = SKSurface.Create(new SKImageInfo(100, 50));
        var gui = new Gui { Input = Substitute.For<IInputHandler>() };
        var node = LayoutNode.CreateRoot(gui, 100, 50);
        var callback = Substitute.For<IDrawListEntry>();
        var shape = Shape.Rect(1, 1, 8, 8).SolidColor(Color.Blue);
        using var path = shape.Path;
        using var paint = shape.Paint;
        node.DrawList.Add(shape);
        node.DrawList.AddClip(new Rect(0, 0, 90, 40));
        node.DrawList.AddClip(shape, Vector2.Zero);
        node.DrawList.Add(callback);
        node.DrawList.Add(new PrimitiveCommand(PrimitiveKind.Rect, new Rect(1, 1, 8, 8), SKColors.Red));
        for (var i = 0; i < 2; i++)
        {
            node.DrawList.Render(gui, node, surface.Canvas);
            Assert.Equal(new DrawStatistics
            {
                Commands = 5,
                Primitives = 1,
                PrimitiveDrawCalls = 1,
                Clips = 2,
                Drawables = 1,
                Callbacks = 1
            }, node.DrawList.LastRenderStatistics);
            surface.Canvas.RestoreToCount(1);
        }
        node.DrawList.Clear();
        Assert.Equal(default, node.DrawList.LastRenderStatistics);
        node.DrawList.Render(gui, node, surface.Canvas);
        Assert.Equal(default, node.DrawList.LastRenderStatistics);
    }

    /// <summary>Failed replay never publishes stale successful statistics.</summary>
    [Fact]
    public void CallbackFailureClearsLastStatistics()
    {
        using var surface = SKSurface.Create(new SKImageInfo(100, 50));
        var gui = new Gui { Input = Substitute.For<IInputHandler>() };
        var node = LayoutNode.CreateRoot(gui, 100, 50);
        node.DrawList.Add(new PrimitiveCommand(PrimitiveKind.Rect, new Rect(1, 1, 8, 8), SKColors.Red));
        node.DrawList.Render(gui, node, surface.Canvas);
        var callback = Substitute.For<IDrawListEntry>();
        callback.WhenForAnyArgs(x => x.Execute(default!, default!, default!))
            .Do(_ => throw new InvalidOperationException());
        node.DrawList.Add(callback);
        Assert.Throws<InvalidOperationException>(() => node.DrawList.Render(gui, node, surface.Canvas));
        Assert.Equal(default, node.DrawList.LastRenderStatistics);
    }

    /// <summary>Frame totals aggregate separate nodes and disabled timing clears previous measurements.</summary>
    [Fact]
    public void GuiAggregatesAndResetsTimings()
    {
        using var harness = new FrameHarness();
        harness.Gui.MeasureRenderTimings = true;
        harness.Frame(static gui =>
        {
            for (var i = 0; i < 2; i++)
            {
                using var node = gui.Node(100, 30).Enter();
                gui.DrawLine(new Vector2(5, 5), new Vector2(10, 5), Color.Blue, 2);
            }
        });
        Assert.Equal(2, harness.Gui.RenderStatistics.PrimitiveDrawCalls);
        Assert.Equal(2, harness.Gui.RenderStatistics.Primitives);
        Assert.True(harness.Gui.RenderSortTime > TimeSpan.Zero);
        Assert.True(harness.Gui.RenderReplayTime > TimeSpan.Zero);
        harness.Gui.MeasureRenderTimings = false;
        harness.Frame(static _ => { });
        Assert.Equal(default, harness.Gui.RenderStatistics);
        Assert.Equal(TimeSpan.Zero, harness.Gui.RenderSortTime);
        Assert.Equal(TimeSpan.Zero, harness.Gui.RenderReplayTime);
    }
}
