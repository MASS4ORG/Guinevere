namespace Guinevere.Tests;

/// <summary>Rectangle slices, insets and offsets measured in <see cref="Size"/> values.</summary>
public class RectSlicingTests
{
    static readonly Rect Box = new(10f, 20f, 200f, 100f);

    [Fact]
    public void EdgeSlicesKeepTheOtherAxis()
    {
        Assert.Equal(new Rect(10f, 20f, 30f, 100f), Box.AlignLeft(30f));
        Assert.Equal(new Rect(180f, 20f, 30f, 100f), Box.AlignRight(30f));
        Assert.Equal(new Rect(95f, 20f, 30f, 100f), Box.AlignCenterX(30f));
        Assert.Equal(new Rect(10f, 20f, 200f, 10f), Box.AlignTop(10f));
        Assert.Equal(new Rect(10f, 110f, 200f, 10f), Box.AlignBottom(10f));
        Assert.Equal(new Rect(10f, 65f, 200f, 10f), Box.AlignCenterY(10f));
    }

    [Fact]
    public void PercentagesUseTheSameAxisAndRatiosTheOther()
    {
        Assert.Equal(new Rect(10f, 20f, 100f, 100f), Box.AlignLeft(Size.Percentage(0.5f)));
        Assert.Equal(new Rect(10f, 20f, 50f, 100f), Box.AlignLeft(Size.Ratio(0.5f)));
        Assert.Equal(new Rect(10f, 95f, 200f, 25f), Box.AlignBottom(Size.Percentage(0.25f)));
        Assert.Equal(new Rect(10f, 20f, 110f, 100f), Box.AlignLeft(Size.Percentage(0.5f) + 10f));
    }

    [Fact]
    public void PaddingInsetsAndExpandGrows()
    {
        Assert.Equal(new Rect(15f, 25f, 190f, 90f), Box.Padding(5f));
        Assert.Equal(new Rect(14f, 22f, 192f, 96f), Box.Padding(4f, 2f));
        Assert.Equal(new Rect(30f, 20f, 160f, 100f), Box.HPadding(Size.Percentage(0.1f)));
        Assert.Equal(new Rect(10f, 30f, 200f, 80f), Box.VPadding(Size.Percentage(0.1f)));
        Assert.Equal(new Rect(5f, 15f, 210f, 110f), Box.Expand(5f));
        Assert.Equal(new Rect(7f, 20f, 206f, 100f), Box.Expand(3f, 0f));
    }

    [Fact]
    public void OffsetsAndSettersMoveOrResize()
    {
        Assert.Equal(new Rect(15f, 20f, 200f, 100f), Box.AddX(5f));
        Assert.Equal(new Rect(10f, 70f, 200f, 100f), Box.AddY(Size.Percentage(0.5f)));
        Assert.Equal(new Rect(10f, 20f, 300f, 100f), Box.AddWidth(Size.Percentage(0.5f)));
        Assert.Equal(new Rect(10f, 20f, 200f, 150f), Box.AddHeight(50f));
        Assert.Equal(new Rect(1f, 2f, 200f, 100f), Box.SetX(1f).SetY(2f));
        Assert.Equal(new Rect(10f, 20f, 50f, 35f), Box.SetWidth(Size.Percentage(0.25f)).SetHeight(35f));
    }

    [Fact]
    public void ExpandAndFitTermsHaveNoMeaningInsideARectangle()
    {
        Assert.Equal(Box.AlignLeft(20f), Box.AlignLeft(Size.Pixels(20f) + Size.Expand() + Size.Fit));
    }
}
