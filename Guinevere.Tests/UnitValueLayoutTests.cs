namespace Guinevere.Tests;

/// <summary>Correctness fixtures for composable layout size expressions.</summary>
public class UnitValueLayoutTests : LayoutNodeTestBase
{
    [Fact]
    public void Lerp_BetweenPixelsAndPercentage_PreservesBothContributions()
    {
        var value = UnitValue.Lerp(UnitValue.Pixels(100f), UnitValue.Percentage(0.5f), 0.25f);

        Assert.Equal(75f, value.PixelsContribution);
        Assert.Equal(0.125f, value.PercentageContribution);
        Assert.Equal(0f, value.ExpandContribution);
    }

    [Fact]
    public void Addition_ComposesEverySupportedMode()
    {
        var value = UnitValue.Pixels(10f) + UnitValue.Percentage(0.2f) + UnitValue.Ratio(0.5f)
                    + UnitValue.Expand(2f) + UnitValue.FitContent(0.3f) + UnitValue.FitLargest(0.4f);

        Assert.Equal(10f, value.PixelsContribution);
        Assert.Equal(0.2f, value.PercentageContribution);
        Assert.Equal(0.5f, value.RatioContribution);
        Assert.Equal(2f, value.ExpandContribution);
        Assert.Equal(0.3f, value.FitContentContribution);
        Assert.Equal(0.4f, value.FitLargestContribution);
    }

    [Fact]
    public void BlendedWidth_ResolvesWithoutModeDiscontinuity()
    {
        var gui = CreateTestGui(400, 200);
        SetGuiStage(gui, Pass.Pass1Build);
        var child = CreateTestLayoutNode(gui, gui.RootNode).Width(
            UnitValue.Lerp(UnitValue.Pixels(100f), UnitValue.Percentage(0.5f), 0.5f));
        child.Height(20f);
        gui.RootNode!.AddChild(child);

        gui.RootNode.CalculateLayout();

        Assert.Equal(150f, child.Rect.W, 2);
    }

    [Theory]
    [InlineData(0f, 100f)]
    [InlineData(0.25f, 125f)]
    [InlineData(0.5f, 150f)]
    [InlineData(0.75f, 175f)]
    [InlineData(1f, 200f)]
    public void PixelToPercentageAnimation_IsContinuous(float amount, float expected)
    {
        var gui = CreateTestGui(400, 200);
        SetGuiStage(gui, Pass.Pass1Build);
        var child = CreateTestLayoutNode(gui, gui.RootNode)
            .Width(UnitValue.Lerp(UnitValue.Pixels(100f), UnitValue.Percentage(0.5f), amount))
            .Height(20f);
        gui.RootNode!.AddChild(child);

        gui.RootNode.CalculateLayout();

        Assert.Equal(expected, child.Rect.W, 2);
    }

    [Fact]
    public void ExpandExpressions_ShareRemainingSpaceByWeight()
    {
        var gui = CreateTestGui(400, 100);
        SetGuiStage(gui, Pass.Pass1Build);
        gui.RootNode!.Style.Direction = Axis.Horizontal;
        var first = CreateTestLayoutNode(gui, gui.RootNode).Width(UnitValue.Expand(1f)).Height(20f);
        var second = CreateTestLayoutNode(gui, gui.RootNode).Width(UnitValue.Expand(3f)).Height(20f);
        gui.RootNode.AddChild(first);
        gui.RootNode.AddChild(second);

        gui.RootNode.CalculateLayout();

        Assert.Equal(100f, first.Rect.W, 2);
        Assert.Equal(300f, second.Rect.W, 2);
    }

    [Fact]
    public void FitLargest_UsesLargestChildOnAxis()
    {
        var gui = CreateTestGui(400, 100);
        SetGuiStage(gui, Pass.Pass1Build);
        var container = CreateTestLayoutNode(gui, gui.RootNode).Width(UnitValue.FitLargest()).Height(20f);
        container.Style.Direction = Axis.Vertical;
        container.AddChild(CreateTestLayoutNode(gui, container, 40f, 10f));
        container.AddChild(CreateTestLayoutNode(gui, container, 90f, 10f));
        gui.RootNode!.AddChild(container);

        gui.RootNode.CalculateLayout();

        Assert.Equal(90f, container.Rect.W, 2);
    }
}
