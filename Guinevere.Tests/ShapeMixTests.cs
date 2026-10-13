namespace Guinevere.Tests;

/// <summary>Verifies filled shape interpolation, smooth unions, and independent geometry transforms.</summary>
public class ShapeMixTests
{
    /// <summary>Verifies that distance interpolation responds to blend weight and preserves a centered intermediate fill.</summary>
    [Fact]
    public void MixInterpolatesDistancesRatherThanUnitingShapes()
    {
        var left = Shape.Circle(30, new Vector2(-20, 0));
        var right = Shape.Circle(30, new Vector2(20, 0));

        var middle = left.Mix(right, 0.5f);
        var nearLeft = left.Mix(right, 0.1f);

        Assert.True(middle.Path.Contains(0, 0));
        Assert.False(middle.Path.Contains(-40, 0));
        Assert.False(middle.Path.Contains(40, 0));
        Assert.InRange(middle.Path.Bounds.MidX, -1, 1);
        Assert.True(nearLeft.Path.Bounds.MidX < -10);
        Assert.True(nearLeft.Path.Contains(-35, 0));
    }

    /// <summary>Verifies that blending retains holes and installs the interpolated paint in the rendering layer.</summary>
    [Fact]
    public void MixPreservesHolesAndUsesBlendedPaintInRenderingLayer()
    {
        var ring = Shape.Circle(40) - Shape.Circle(15);
        ring.SolidColor(Color.FromArgb(255, 255, 0, 0));
        var other = ring.Copy().SolidColor(Color.FromArgb(255, 0, 0, 255));

        var mix = ring.Mix(other, 0.5f);

        Assert.False(mix.Path.Contains(0, 0));
        Assert.True(mix.Path.Contains(25, 0));
        Assert.Equal(new SkiaSharp.SKColor(127, 0, 127), mix.Paint!.Color);
        Assert.Same(mix.Paint, mix.Layers[0][0].paint);
        Assert.NotSame(ring.Paint, mix.Paint);
    }

    /// <summary>Verifies that smooth unions blend nearby boundaries while preserving separated outer edges.</summary>
    [Fact]
    public void SmoothUnionBridgesNearbyShapesWithoutExpandingDistantEdges()
    {
        var left = Shape.Circle(20, new Vector2(-25, 0));
        var right = Shape.Circle(20, new Vector2(25, 0));
        var original = left.Path.Bounds;

        var sharp = left.Union(right);
        var smooth = left.Union(right, 30);

        Assert.False(sharp.Path.Contains(0, 0));
        Assert.True(smooth.Path.Contains(0, 0));
        Assert.InRange(smooth.Path.Bounds.Left, -46, -44);
        Assert.InRange(smooth.Path.Bounds.Right, 44, 46);
        Assert.Equal(original, left.Path.Bounds);
        Assert.Same(left.Paint, smooth.Layers[0][0].paint);
    }

    /// <summary>Verifies that smooth unions retain distant holes and empty fills have defined blending endpoints.</summary>
    [Fact]
    public void SmoothUnionKeepsDistantHolesAndHandlesEmptyShapes()
    {
        var ring = Shape.Circle(40) - Shape.Circle(15);
        var remote = Shape.Circle(10, new Vector2(100, 0));
        var empty = Shape.Circle(10) - Shape.Circle(10);

        var smooth = ring.Union(remote, 20);

        Assert.False(smooth.Path.Contains(0, 0));
        Assert.True(smooth.Path.Contains(25, 0));
        Assert.True(smooth.Path.Contains(100, 0));
        Assert.Equal(ring.Path.Bounds, ring.Union(empty, 20).Path.Bounds);
        Assert.Equal(ring.Path.Bounds, empty.Union(ring, 20).Path.Bounds);
        Assert.True(ring.Mix(empty, 0.5f).Path.IsEmpty);
        Assert.True(empty.Mix(ring, 0.5f).Path.IsEmpty);
        Assert.Equal(ring.Path.Bounds, ring.Mix(empty, -1).Path.Bounds);
        Assert.Equal(ring.Path.Bounds, empty.Mix(ring, 2).Path.Bounds);
    }

    /// <summary>Verifies that bounded distance sampling supports tiny and large filled shapes.</summary>
    [Fact]
    public void MixHandlesDifferentGeometryScales()
    {
        var tiny = Shape.Circle(0.01f);
        var large = Shape.Circle(5000);
        Assert.True(tiny.Mix(tiny, 0.3f).Path.Contains(0, 0));
        Assert.True(large.Mix(large, 0.7f).Path.Contains(4000, 0));
    }

    /// <summary>Verifies that a sampled blend stays within a fraction of a cell of the exact boundary with bounded detail.</summary>
    [Fact]
    public void MixOfIdenticalCirclesStaysAccurateAndBounded()
    {
        var circle = Shape.Circle(100);

        var mixed = circle.Mix(circle.Copy(), 0.3f);

        Assert.InRange(mixed.Path.Bounds.Width, 199, 201);
        Assert.InRange(mixed.Path.Bounds.Height, 199, 201);
        Assert.True(mixed.Path.Contains(98, 0));
        Assert.False(mixed.Path.Contains(102, 0));
        Assert.InRange(mixed.Path.PointCount, 8, 2000);
    }

    /// <summary>Verifies that a distance blend of separated fills can vanish when neither field reaches zero.</summary>
    [Fact]
    public void MixOfDisjointFillsDoesNotRetainTheirUnion()
    {
        var left = Shape.Rect(0, 0, 40, 40);
        var right = Shape.Rect(60, 0, 100, 40);

        Assert.True(left.Mix(right, 0.5f).Path.IsEmpty);
    }

    /// <summary>Verifies that blending preserves endpoint geometry and leaves both inputs unchanged.</summary>
    [Fact]
    public void MixKeepsEndpointShapesAndDoesNotMutateInputs()
    {
        var left = Shape.Rect(0, 0, 40, 40).SolidColor(Color.FromArgb(255, 255, 0, 0));
        var right = Shape.Rect(20, 0, 60, 40).SolidColor(Color.FromArgb(255, 0, 0, 255));
        var leftBounds = left.Path.Bounds;
        var rightBounds = right.Path.Bounds;

        var atStart = left.Mix(right, 0);
        var middle = left.Mix(right, 0.5f);
        var atEnd = left.Mix(right, 1);

        Assert.Equal(leftBounds, atStart.Path.Bounds);
        Assert.Equal(rightBounds, atEnd.Path.Bounds);
        Assert.False(middle.Path.IsEmpty);
        Assert.Equal(leftBounds, left.Path.Bounds);
        Assert.Equal(rightBounds, right.Path.Bounds);
    }

    /// <summary>Verifies that scaling transforms the main path and additional layers once without changing their inputs.</summary>
    [Fact]
    public void ScaleTransformsDistinctLayersOnceAroundMainCenter()
    {
        var shape = Shape.Circle(10, new Vector2(5, 7));
        var layer = Shape.Rect(0, 0, 10, 20);
        shape.AddToLayer(1, layer.Path, new SKPaint());
        var mainBounds = shape.Path.Bounds;
        var layerBounds = layer.Path.Bounds;

        var scaled = shape.Scale(2, 3);

        Assert.Equal(new SKRect(-15, -23, 25, 37), scaled.Path.Bounds);
        Assert.Equal(new SKRect(-5, -14, 15, 46), scaled.Layers[1][0].path.Bounds);
        Assert.Same(scaled.Path, scaled.Layers[0][0].path);
        Assert.Equal(mainBounds, shape.Path.Bounds);
        Assert.Equal(layerBounds, layer.Path.Bounds);
    }

    /// <summary>Verifies that contraction offsets a circle by the requested distance and keeps its rendering paint.</summary>
    [Fact]
    public void ExpandContractsCircleUniformlyAndPreservesPaint()
    {
        var circle = Shape.Circle(30, new Vector2(20, 40)).SolidColor(Color.FromArgb(255, 255, 0, 0));

        var contracted = circle.Expand(-5);
        var expanded = circle.Expand(5);

        Assert.Equal(new SKRect(-5, 15, 45, 65), contracted.Path.Bounds);
        Assert.Equal(new SKRect(-15, 5, 55, 75), expanded.Path.Bounds);
        Assert.Same(circle.Paint, contracted.Layers[0][0].paint);
        Assert.Same(circle.Paint, expanded.Layers[0][0].paint);
        Assert.Equal(circle.Path.Bounds, circle.Expand(0).Path.Bounds);
        Assert.Equal(new SKRect(-10, 10, 50, 70), circle.Path.Bounds);
        Assert.True(circle.Expand(-35).Path.IsEmpty);
    }

    /// <summary>Verifies that contracting a ring shrinks its outer boundary and expands its inner hole uniformly.</summary>
    [Fact]
    public void ExpandContractsBothRingBoundaries()
    {
        var ring = Shape.Circle(40) - Shape.Circle(20);

        var contracted = ring.Expand(-5);

        Assert.True(contracted.Path.Contains(30, 0));
        Assert.False(contracted.Path.Contains(23, 0));
        Assert.False(contracted.Path.Contains(37, 0));
        Assert.Equal(new SKRect(-35, -35, 35, 35), contracted.Path.Bounds);
        Assert.True(ring.Expand(-11).Path.IsEmpty);
    }

    /// <summary>Verifies that onion outlines extend the requested distance on both sides of the original boundary.</summary>
    [Fact]
    public void OnionCreatesUniformThicknessAroundCircle()
    {
        var circle = Shape.Circle(30);

        var onion = circle.Onion(5);

        Assert.False(onion.Path.Contains(0, 0));
        Assert.False(onion.Path.Contains(23, 0));
        Assert.True(onion.Path.Contains(26, 0));
        Assert.True(onion.Path.Contains(34, 0));
        Assert.False(onion.Path.Contains(36, 0));
        Assert.Equal(new SKRect(-35, -35, 35, 35), onion.Path.Bounds);
    }
}
