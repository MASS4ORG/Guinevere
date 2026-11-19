using System.Runtime.CompilerServices;

namespace Guinevere.Tests.Layout;

public class LayoutStyleStorageTests
{
    public static TheoryData<string> Values => new()
    {
        "pixels", "percentage", "ratio", "expand", "fit", "fit-largest", "percentage+pixels", "expand+pixels",
        "percentage+expand", "negative", "zero"
    };

    static Size Make(string name) => name switch
    {
        "pixels" => Size.Pixels(12f),
        "percentage" => Size.Percentage(0.5f),
        "ratio" => Size.Ratio(2f),
        "expand" => Size.Expand(3f),
        "fit" => Size.FitContent(),
        "fit-largest" => Size.FitLargest(0.5f),
        "percentage+pixels" => Size.Percentage(1f) - 10f,
        "expand+pixels" => Size.Expand() + 4f,
        "percentage+expand" => Size.Percentage(0.25f) + Size.Expand(2f) + 1f,
        "negative" => Size.Pixels(-3f),
        _ => default
    };

    [Theory]
    [MemberData(nameof(Values))]
    public void EveryDistanceRoundTripsExactly(string name)
    {
        var value = Make(name);
        var style = LayoutStyle.Default;

        style.Width = value;
        style.MaxHeight = value;
        style.AlignSelf = value;
        style.Gap = value;
        style.MarginLeft = value;
        style.PaddingBottom = value;
        style.AlignContentHorizontal = value;
        style.AbsoluteY = value;

        Assert.Equal(value, style.Width);
        Assert.Equal(value, style.MaxHeight);
        Assert.Equal(value, style.AlignSelf);
        Assert.Equal(value, style.Gap);
        Assert.Equal(value, style.MarginLeft);
        Assert.Equal(value, style.PaddingBottom);
        Assert.Equal(value, style.AlignContentHorizontal);
        Assert.Equal(value, style.AbsoluteY);
    }

    [Fact]
    public void AValueWithANodeReferenceRoundTrips()
    {
        var node = LayoutNode.CreateRoot(new Gui(), 100, 100);
        var value = Size.PercentOf(node, 0.5f) + 8f;
        var style = LayoutStyle.Default;

        style.Height = value;

        Assert.Equal(value, style.Height);
        Assert.Equal(1, style.Height!.Value.References.Length);
    }

    [Fact]
    public void ACopyDoesNotSeeLaterEditsToTheOriginal()
    {
        var original = LayoutStyle.Default;
        original.Width = Size.Percentage(0.25f) + Size.Expand(2f);
        var copy = original;

        original.Width = Size.Ratio(1f) + Size.FitContent();
        original.Height = Size.Percentage(0.5f) + Size.Expand();

        Assert.Equal(Size.Percentage(0.25f) + Size.Expand(2f), copy.Width);
        Assert.Null(copy.Height);
    }

    [Fact]
    public void ReplacingAMixedValueWithASimpleOneReadsTheSimpleOne()
    {
        var style = LayoutStyle.Default;
        style.Gap = Size.Percentage(0.25f) + Size.Expand(2f);

        style.Gap = 6f;

        Assert.Equal(Size.Pixels(6f), style.Gap);
        Assert.True(style.SpacingIsPixels);
        Assert.Equal(6f, style.GapPixels);
    }

    [Fact]
    public void FastReadsMatchTheValues()
    {
        var style = LayoutStyle.Default;
        style.Width = Size.Expand(2f) + 5f;
        style.Height = Size.Percentage(0.5f) + Size.Expand(3f);

        Assert.Equal(2f, style.WidthExpandWeight);
        Assert.Equal(3f, style.HeightExpandWeight);
        Assert.True(style.ExpandsWidth);
        Assert.True(style.ExpandsHeight);
        Assert.Equal(5f, style.FixedWidth);
        Assert.False(style.WidthIsFixed);
    }

    [Fact]
    public void UnsetValuesKeepTheirDefaults()
    {
        var style = LayoutStyle.Default;

        Assert.Null(style.Width);
        Assert.Null(style.AlignSelf);
        Assert.Equal(default, style.Gap);
        Assert.Equal(default, style.AlignContentVertical);
        Assert.Equal(default, style.AbsoluteX);
    }

    [Fact]
    public void TheStyleStaysSmallBecauseEveryNodeCarriesOne()
    {
        Assert.True(Unsafe.SizeOf<LayoutStyle>() <= 208,
            $"LayoutStyle is {Unsafe.SizeOf<LayoutStyle>()} B; every layout node embeds one, keep it compact");
    }
}
