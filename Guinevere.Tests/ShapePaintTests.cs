namespace Guinevere.Tests;

/// <summary>Checks radial gradient stops and compositing through rendered pixels.</summary>
public class ShapePaintTests
{
    static SKBitmap Render(Shape shape)
    {
        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        surface.Canvas.Clear(SKColors.Transparent);
        shape.Render(new Gui(), null!, surface.Canvas);
        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    /// <summary>The first radial color stays constant through the inner radius before blending outward.</summary>
    [Fact]
    public void RadialGradientUsesBothRadiusStops()
    {
        using var bitmap = Render(Shape.Rect(0, 0, 100, 100)
            .RadialGradientColor(Color.Red, Color.Blue, 20, 40));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(60, 50));
        var middle = bitmap.GetPixel(79, 50);
        Assert.InRange(middle.Red, (byte)125, (byte)140);
        Assert.InRange(middle.Blue, (byte)115, (byte)130);
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(95, 50));
    }

    /// <summary>A transparent radial overlay keeps the earlier fill while its opaque part paints above it.</summary>
    [Fact]
    public void RadialGradientCompositesLatestGradientOverEarlierFill()
    {
        using var bitmap = Render(Shape.Rect(0, 0, 100, 100)
            .RadialGradientColor(Color.Blue, Color.Blue, 0, 40)
            .RadialGradientColor(Color.Red, new Color(0xFF000000), 10, 30));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 50));
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(90, 50));
        var middle = bitmap.GetPixel(69, 50);
        Assert.Equal((byte)255, middle.Alpha);
        Assert.InRange(middle.Red, (byte)125, (byte)140);
        Assert.InRange(middle.Blue, (byte)115, (byte)130);
    }

    /// <summary>Offset radial centers move the inner color's constant region.</summary>
    [Fact]
    public void RadialGradientOffsetsItsCenter()
    {
        using var bitmap = Render(Shape.Rect(0, 0, 100, 100)
            .RadialGradientColor(Color.Red, Color.Blue, 10, 20, offsetX: 20, offsetY: -10));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(70, 40));
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(40, 60));
    }

    /// <summary>Radius bounds reject configurations that cannot describe an outward gradient.</summary>
    [Theory]
    [InlineData(-1f, 20f)]
    [InlineData(0f, 0f)]
    [InlineData(20f, 20f)]
    [InlineData(30f, 20f)]
    public void RadialGradientRejectsInvalidRadii(float innerRadius, float outerRadius) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Shape.Circle(20)
            .RadialGradientColor(Color.Red, Color.Blue, innerRadius, outerRadius));
}
