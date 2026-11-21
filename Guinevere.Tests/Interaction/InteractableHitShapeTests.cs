using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Interaction;

/// <summary>A rectangular interactable decides hover with comparisons and agrees with Skia on every border point.</summary>
public class InteractableHitShapeTests
{
    static readonly Rect Box = new(10, 10, 20, 20);

    static bool Hovered(Rect rect, Vector2 pointer)
    {
        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(pointer);
        var gui = new TestableGui { Input = input };
        return new InteractableElement(rect, gui, "hit").OnHover();
    }

    static bool Skia(Rect rect, Vector2 pointer)
    {
        using var builder = new SKPathBuilder();
        builder.AddRect(rect);
        using var path = builder.Detach();
        return path.Contains(pointer.X, pointer.Y);
    }

    /// <summary>Points clearly inside hover and points clearly outside do not.</summary>
    [Theory]
    [InlineData(20, 20, true)]
    [InlineData(10.5f, 29.5f, true)]
    [InlineData(9.5f, 20, false)]
    [InlineData(30.5f, 20, false)]
    [InlineData(20, 9.5f, false)]
    [InlineData(20, 30.5f, false)]
    [InlineData(-100, -100, false)]
    public void PointsInsideHoverAndPointsOutsideDoNot(float x, float y, bool expected) =>
        Assert.Equal(expected, Hovered(Box, new Vector2(x, y)));

    /// <summary>Points on an edge, a corner or a degenerate rectangle are answered as Skia answers them.</summary>
    [Theory]
    [InlineData(10, 20)]
    [InlineData(30, 20)]
    [InlineData(20, 10)]
    [InlineData(20, 30)]
    [InlineData(10, 10)]
    [InlineData(30, 30)]
    [InlineData(10, 30)]
    [InlineData(30, 10)]
    public void BorderPointsAgreeWithSkia(float x, float y) =>
        Assert.Equal(Skia(Box, new Vector2(x, y)), Hovered(Box, new Vector2(x, y)));

    /// <summary>A rectangle without area still asks Skia, so it behaves as before.</summary>
    [Theory]
    [InlineData(10, 10)]
    [InlineData(10, 11)]
    public void ARectangleWithoutAreaAgreesWithSkia(float x, float y)
    {
        var flat = new Rect(10, 10, 0, 5);

        Assert.Equal(Skia(flat, new Vector2(x, y)), Hovered(flat, new Vector2(x, y)));
    }
}
