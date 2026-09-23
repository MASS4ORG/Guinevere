namespace Guinevere.Tests.Styling;

/// <summary>CSS alignment keywords map to a fraction of the free space.</summary>
public class StyleLayoutAlignTests
{
    [Theory]
    [InlineData("flex-start", 0f)]
    [InlineData("start", 0f)]
    [InlineData("left", 0f)]
    [InlineData("top", 0f)]
    [InlineData("center", 0.5f)]
    [InlineData("middle", 0.5f)]
    [InlineData("flex-end", 1f)]
    [InlineData("end", 1f)]
    [InlineData("right", 1f)]
    [InlineData("bottom", 1f)]
    [InlineData("stretch", 0f)]
    [InlineData("CENTER", 0f)]
    public void KeywordsMapToFractions(string keyword, float expected) =>
        Assert.Equal(expected, StyleLayout.AlignFraction(keyword));
}
