using Guinevere.Graphs;

namespace Guinevere.Tests.Graphs;

public sealed class GraphCameraTests
{
    [Theory]
    [InlineData(0.01)]
    [InlineData(1)]
    [InlineData(16)]
    public void ZoomPreservesWorldPointUnderCursor(double zoom)
    {
        var camera = new GraphCamera();
        camera.SetView(new GraphPoint(-12345.25, 9876.5), zoom);
        var anchor = new Vector2(231.5f, 117.25f);
        var before = camera.ViewToWorld(anchor);
        camera.ZoomAt(anchor, 1.25);
        var after = camera.ViewToWorld(anchor);
        Assert.Equal(before.X, after.X, 8);
        Assert.Equal(before.Y, after.Y, 8);
        camera.ZoomAt(anchor, 0.000001);
        Assert.Equal(camera.MinimumZoom, camera.Zoom);
        camera.ZoomAt(anchor, 1e20);
        Assert.Equal(camera.MaximumZoom, camera.Zoom);
    }

    [Fact]
    public void LargeWorldOriginRetainsSmallLocalDistances()
    {
        var camera = new GraphCamera();
        camera.SetView(new GraphPoint(1e12, -1e12), 4);
        var point = new GraphPoint(1e12 + 0.125, -1e12 + 0.25);
        Assert.Equal(new Vector2(0.5f, 1), camera.WorldToView(point));
        Assert.Equal(point, camera.ViewToWorld(new Vector2(0.5f, 1)));
        camera.Pan(new Vector2(4, -8));
        Assert.Equal(new GraphPoint(1e12 - 1, -1e12 + 2), camera.Origin);
        camera.Reset();
        Assert.Equal(default, camera.Origin);
        Assert.Equal(1, camera.Zoom);
    }

    [Fact]
    public void FitUsesLogicalPaddingAndCentersDegenerateBounds()
    {
        var camera = new GraphCamera();
        var bounds = new GraphBounds(-100, 200, 200, 100);
        camera.Fit(bounds, new Vector2(464, 264));
        Assert.Equal(2, camera.Zoom);
        Assert.Equal(new Vector2(232, 132), camera.WorldToView(bounds.Center));
        Assert.Equal(new Vector2(32, 32), camera.WorldToView(new GraphPoint(-100, 200)));
        var before = camera.Origin;
        camera.Fit(bounds, Vector2.Zero);
        Assert.Equal(before, camera.Origin);
        camera.Fit(default, new Vector2(400, 300));
        Assert.Equal(camera.MaximumZoom, camera.Zoom);
        Assert.Equal(new Vector2(200, 150), camera.WorldToView(default));
        Assert.Equal(new GraphBounds(camera.Origin.X, camera.Origin.Y, 25, 18.75),
            camera.VisibleBounds(new Vector2(400, 300)));
    }

    [Fact]
    public void InvalidCameraAndBoundsInputsAreRejectedWithoutChangingView()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphCamera(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphCamera(2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphBounds(0, 0, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphBounds(0, 0, 1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphBounds(double.MaxValue, 0, double.MaxValue, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphBounds(double.NaN, 0, 1, 1));
        var camera = new GraphCamera();
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.SetView(new GraphPoint(double.NaN, 0), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.SetView(default, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.Pan(new Vector2(float.PositiveInfinity, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.ZoomAt(default, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.ZoomAt(default, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.Fit(default, new Vector2(400, 300), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.VisibleBounds(new Vector2(float.NaN, 1)));
        Assert.Equal(default, camera.Origin);
        Assert.Equal(1, camera.Zoom);
        Assert.Equal(default, camera.VisibleBounds(new Vector2(-1, -1)));
    }

    [Fact]
    public void BoundsUnionAndCullingIncludeCrossingEdgesAndBorders()
    {
        var bounds = new GraphBounds(-10, -20, 30, 40);
        Assert.True(bounds.Intersects(new GraphBounds(20, 20, 10, 10)));
        Assert.True(bounds.Intersects(new GraphBounds(-100, 0, 200, 0)));
        Assert.False(bounds.Intersects(new GraphBounds(21, 20, 10, 10)));
        Assert.Equal(new GraphBounds(-100, -20, 200, 40), bounds.Union(new GraphBounds(-100, 0, 200, 0)));
        Assert.Equal(20, bounds.Right);
        Assert.Equal(20, bounds.Bottom);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(1)]
    [InlineData(16)]
    public void AdaptiveGridRemainsBoundedAtEveryZoom(double zoom)
    {
        var camera = new GraphCamera();
        camera.SetView(new GraphPoint(-123, -234), zoom);
        var lines = GraphGrid.Lines(camera, new Vector2(1920, 1080)).ToArray();
        Assert.InRange(lines.Length, 50, 140);
        Assert.Contains(lines, static line => line.Major);
        Assert.Contains(lines, static line => !line.Major);
        Assert.All(lines, static line => Assert.True(float.IsFinite(line.Start.X) && float.IsFinite(line.Start.Y)));
        Assert.DoesNotContain(GraphGrid.Lines(camera, new Vector2(1920, 1080), false), static line => line.Axis);
        camera.Reset();
        Assert.Contains(GraphGrid.Lines(camera, new Vector2(400, 300)), static line => line.Axis);
    }

    [Fact]
    public void MinimapPreservesAspectRatioAndContainsViewport()
    {
        var content = new GraphBounds(-1000, -100, 2000, 200);
        var visible = new GraphBounds(-100, -500, 200, 1000);
        var target = new Rect(20, 30, 200, 100);
        var map = new GraphMinimap(content, visible, target);
        var projected = map.Map(content);
        Assert.Equal(10, projected.W / projected.H);
        Assert.Equal(target.Center, projected.Center);
        Assert.True(target.Contains(map.Viewport.Position));
        Assert.True(target.Contains(map.Viewport.BottomRight));
        Assert.Equal(content.Center, map.Unmap(projected.Center));
        Assert.Equal(map.Unmap(new Vector2(20, 30)), map.Unmap(new Vector2(-10000, -10000)));
        Assert.Throws<ArgumentOutOfRangeException>(static () => new GraphMinimap(default, default, default));
        Assert.Throws<ArgumentOutOfRangeException>(
            static () => new GraphMinimap(default, default, new Rect(0, 0, 10, 0)));
        Assert.True(float.IsFinite(new GraphMinimap(default, default, target).Viewport.X));
    }
}
