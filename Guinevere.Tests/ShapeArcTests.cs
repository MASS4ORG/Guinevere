namespace Guinevere.Tests;

/// <summary>Checks rounded arcs form a single filled lane without internal shadow seams.</summary>
public class ShapeArcTests
{
    /// <summary>A rounded quarter arc contains the whole lane and excludes the center.</summary>
    [Fact]
    public void Arc_ContainsLaneAndRoundedCaps()
    {
        var shape = Shape.Arc(40, 10, Angle.Degrees(-90), Angle.Degrees(90));
        foreach (var degree in new[] { -90f, -75f, -45f, -15f, 0f })
        {
            var direction = Angle.Degrees(degree).GetDirectionVector();
            Assert.True(shape.Path.Contains(direction.X * 40, direction.Y * 40));
            Assert.False(shape.Path.Contains(direction.X * 25, direction.Y * 25));
        }
        Assert.True(shape.Path.Contains(-5, -40));
        Assert.True(shape.Path.Contains(40, 5));
        Assert.False(shape.Path.Contains(0, 0));
    }

    /// <summary>Inset shadows shade the true perimeter and leave the arc's cap overlap filled uniformly.</summary>
    [Fact]
    public void Arc_InsetShadowHasNoInternalCapSeam()
    {
        using var surface = SKSurface.Create(new SKImageInfo(120, 120));
        var shape = Shape.Arc(40, 10, Angle.Degrees(-90), Angle.Degrees(90)).Move(60, 60)
            .SolidColor(Color.White).InnerShadow(Color.Black, 0, 2);
        shape.Render(new Gui(), null!, surface.Canvas);
        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        Assert.Equal(SKColors.White, bitmap.GetPixel(69, 20));
        Assert.Equal(SKColors.White, bitmap.GetPixel(100, 51));
        Assert.Equal(SKColors.Black, bitmap.GetPixel(60, 11));
    }

    /// <summary>A full revolution closes into a ring with no caps invading its central hole.</summary>
    [Fact]
    public void Arc_FullTurnFormsRing()
    {
        var shape = Shape.Arc(40, 10, Angle.Degrees(-90), Angle.FullCircle);
        foreach (var point in new[] { new Vector2(40, 0), new Vector2(0, 40), new Vector2(-40, 0),
                     new Vector2(0, -40) })
            Assert.True(shape.Path.Contains(point.X, point.Y));
        Assert.False(shape.Path.Contains(0, 0));
        Assert.False(shape.Path.Contains(0, -29));
    }
}
