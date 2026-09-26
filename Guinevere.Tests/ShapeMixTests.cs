namespace Guinevere.Tests;

public class ShapeMixTests
{
    [Fact]
    public void MixKeepsEndpointShapesAndDoesNotMutateInputs()
    {
        var left = Shape.Rect(0, 0, 40, 40).SolidColor(Color.FromArgb(255, 255, 0, 0));
        var right = Shape.Rect(60, 0, 100, 40).SolidColor(Color.FromArgb(255, 0, 0, 255));
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
}
