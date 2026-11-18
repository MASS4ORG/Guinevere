namespace Guinevere.Tests.Styling;

/// <summary>Parsing <c>.pss</c> size text into <see cref="Size"/> values.</summary>
public class StyleValueSizeTests
{
    public static TheoryData<string, Size> Sizes => new()
    {
        { "12", Size.Pixels(12f) },
        { "12px", Size.Pixels(12f) },
        { "-4px", Size.Pixels(-4f) },
        { "25%", Size.Percentage(0.25f) },
        { "ratio(0.5)", Size.Ratio(0.5f) },
        { "expand", Size.Expand() },
        { "expand(2)", Size.Expand(2f) },
        { "fit", Size.Fit },
        { "auto", Size.Fit },
        { "fit(0.5)", Size.FitContent(0.5f) },
        { "fit-largest", Size.FitLargest() },
        { "fit-largest(3)", Size.FitLargest(3f) },
        { "50% + 8px", Size.Percentage(0.5f) + 8f },
        { "  50%   -  8px ", Size.Percentage(0.5f) - Size.Pixels(8f) },
        { "expand + 10px - 2%", Size.Expand() + 10f - Size.Percentage(0.02f) },
    };

    [Theory]
    [MemberData(nameof(Sizes))]
    public void TrySizeReadsTermsAndSums(string text, Size expected)
    {
        Assert.True(StyleValue.TrySize(text, out var size));
        Assert.Equal(expected, size);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bad")]
    [InlineData("10px +")]
    [InlineData("10px 20px")]
    [InlineData("ratio(x)")]
    [InlineData("grow(2)")]
    [InlineData("expand(")]
    public void TrySizeRejectsInvalidText(string? text)
    {
        Assert.False(StyleValue.TrySize(text, out _));
    }

    [Fact]
    public void ReadSizesSplitsShorthandSidesButKeepsSums()
    {
        var sides = new Size[4];

        var count = StyleValue.ReadSizes("8px 50% + 4px expand", sides);

        Assert.Equal(3, count);
        Assert.Equal([Size.Pixels(8f), Size.Percentage(0.5f) + 4f, Size.Expand()], sides[..3]);
    }

    [Theory]
    [InlineData("1px 2px 3px 4px 5px")]
    [InlineData("1px bad")]
    [InlineData("")]
    public void ReadSizesRejectsTooManyOrInvalidSides(string text)
    {
        Assert.Equal(0, StyleValue.ReadSizes(text, new Size[4]));
    }

    [Fact]
    public void SizesFormatAsTheyParse()
    {
        var size = Size.Percentage(0.5f) + 8f - Size.Expand(2f);

        Assert.Equal("8px + 50% - expand(2)", StyleValue.Format(size));
        Assert.True(StyleValue.TrySize(StyleValue.Format(size), out var parsed));
        Assert.Equal(size, parsed);
    }
}
