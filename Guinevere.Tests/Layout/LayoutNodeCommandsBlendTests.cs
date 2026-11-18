namespace Guinevere.Tests.Layout;

/// <summary>Additive and interpolating size and spacing commands.</summary>
public class LayoutNodeCommandsBlendTests : LayoutNodeTestBase
{
    [Fact]
    public void AddWidthAndHeightSumWeightedContributionsFromZero()
    {
        var node = CreateNodeWithBuildStage()
            .AddWidth(Size.Percentage(0.5f), 0.5f).AddWidth(10f)
            .AddHeight(Size.Expand(), 2f);

        Assert.Equal(Size.Percentage(0.25f) + 10f, node.Style.Width);
        Assert.Equal(Size.Expand(2f), node.Style.Height);
    }

    [Fact]
    public void LerpWidthAndHeightStartFromContentWhenUnset()
    {
        var node = CreateNodeWithBuildStage()
            .LerpWidth(Size.Pixels(100f), 0.25f)
            .Height(40f).LerpHeight(Size.Percentage(1f), 0.5f);

        Assert.Equal(Size.FitContent(0.75f) + 25f, node.Style.Width);
        Assert.Equal(Size.Pixels(20f) + Size.Percentage(0.5f), node.Style.Height);
    }

    [Fact]
    public void GapPaddingAndMarginBlendEverySide()
    {
        var node = CreateNodeWithBuildStage()
            .Gap(10f).AddGap(Size.Percentage(0.1f)).LerpGap(Size.Pixels(0f), 0.5f)
            .Padding(4f).AddPadding(2f, 0.5f).LerpPadding(Size.Pixels(15f), 0.5f)
            .Margin(8f).AddMargin(Size.Expand()).LerpMargin(Size.Pixels(0f), 0.25f);

        Assert.Equal(Size.Pixels(5f) + Size.Percentage(0.05f), node.Style.Gap);
        AssertPaddingValues(node, 10f, 10f, 10f, 10f);
        Assert.Equal(Size.Pixels(6f) + Size.Expand(0.75f), node.Style.MarginLeft);
        Assert.Equal(node.Style.MarginLeft, node.Style.MarginBottom);
    }

    [Fact]
    public void BlendingInTheRenderPassChangesNothing()
    {
        var node = CreateNodeWithRenderStage()
            .AddWidth(10f).LerpHeight(10f, 1f).AddGap(10f).AddPadding(10f).LerpMargin(10f, 1f);

        Assert.Null(node.Style.Width);
        Assert.Null(node.Style.Height);
        Assert.True(node.Style.Gap.IsZero);
        AssertPaddingValues(node, 0f, 0f, 0f, 0f);
        AssertMarginValues(node, 0f, 0f, 0f, 0f);
    }
}
