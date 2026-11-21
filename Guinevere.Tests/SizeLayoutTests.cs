namespace Guinevere.Tests;

/// <summary>Correctness fixtures for composable layout size expressions.</summary>
public class SizeLayoutTests : LayoutNodeTestBase
{
    [Fact]
    public void EqualExpressionsRequireEqualContributions()
    {
        var mixed = Size.Pixels(20) + Size.Percentage(0.5f) + Size.FitContent();
        var same = Size.Pixels(20) + Size.Percentage(0.5f) + Size.FitContent();
        var different = Size.Pixels(20) + Size.Percentage(0.5f) + Size.FitLargest();

        Assert.Equal(mixed, same);
        Assert.NotEqual(mixed, different);
        Assert.NotEqual(Size.Pixels(50), Size.Percentage(0.5f));
    }

    [Fact]
    public void LerpBetweenPixelsAndPercentagePreservesBothContributions()
    {
        var value = Size.Lerp(Size.Pixels(100f), Size.Percentage(0.5f), 0.25f);

        Assert.Equal(75f, value.PixelsContribution);
        Assert.Equal(0.125f, value.PercentageContribution);
        Assert.Equal(0f, value.ExpandContribution);
    }

    [Fact]
    public void AdditionComposesEverySupportedMode()
    {
        var value = Size.Pixels(10f) + Size.Percentage(0.2f) + Size.Ratio(0.5f)
                    + Size.Expand(2f) + Size.FitContent(0.3f) + Size.FitLargest(0.4f);

        Assert.Equal(10f, value.PixelsContribution);
        Assert.Equal(0.2f, value.PercentageContribution);
        Assert.Equal(0.5f, value.RatioContribution);
        Assert.Equal(2f, value.ExpandContribution);
        Assert.Equal(0.3f, value.FitContentContribution);
        Assert.Equal(0.4f, value.FitLargestContribution);
    }

    [Fact]
    public void SubtractionAndNegationFlipEveryTerm()
    {
        var value = Size.Pixels(10f) - (Size.Percentage(0.5f) + Size.Expand(2f));

        Assert.Equal(Size.Pixels(10f) + Size.Percentage(-0.5f) + Size.Expand(-2f), value);
        Assert.Equal(Size.Pixels(-10f), -Size.Pixels(10f));
        Assert.True(Size.Pixels(3f).IsPixelsOnly);
        Assert.False((Size.Pixels(3f) + Size.Ratio(1f)).IsPixelsOnly);
        Assert.True(default(Size).IsZero);
    }

    [Fact]
    public void NodeReferencesMergeByNodeAndDropWhenWeightless()
    {
        var gui = CreateTestGui();
        var a = CreateTestLayoutNode(gui);
        var b = CreateTestLayoutNode(gui);

        var blended = Size.Lerp(Size.PercentOf(a), Size.PercentOf(b), 0.25f) + Size.PercentOf(a, 0.25f);

        Assert.Equal([new NodeReference(a, 1f), new NodeReference(b, 0.25f)], blended.References.ToArray());
        Assert.True(Size.PercentOf(a, 0f).IsZero);
        Assert.True((Size.PercentOf(a) - Size.PercentOf(a)).IsZero);
        Assert.True((Size.PercentOf(a) * 0f).References.IsEmpty);
        Assert.NotEqual(Size.PercentOf(a), Size.PercentOf(b));
        Assert.Throws<ArgumentNullException>(static () => Size.PercentOf(null!));
    }

    [Fact]
    public void ToStringUsesStylesheetSyntax()
    {
        Assert.Equal("0px", default(Size).ToString());
        Assert.Equal("12px", Size.Pixels(12f).ToString());
        Assert.Equal("-25% + ratio(2) + fit(1) + fit-largest(0.5)",
            (Size.Percentage(-0.25f) + Size.Ratio(2f) + Size.Fit + Size.FitLargest(0.5f)).ToString());
    }

    [Fact]
    public void BlendedWidthResolvesWithoutModeDiscontinuity()
    {
        var gui = CreateTestGui(400, 200);
        SetGuiStage(gui, Pass.Pass1Build);
        var child = CreateTestLayoutNode(gui, gui.RootNode).Width(
            Size.Lerp(Size.Pixels(100f), Size.Percentage(0.5f), 0.5f));
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
    public void PixelToPercentageAnimationIsContinuous(float amount, float expected)
    {
        var gui = CreateTestGui(400, 200);
        SetGuiStage(gui, Pass.Pass1Build);
        var child = CreateTestLayoutNode(gui, gui.RootNode)
            .Width(Size.Lerp(Size.Pixels(100f), Size.Percentage(0.5f), amount))
            .Height(20f);
        gui.RootNode!.AddChild(child);

        gui.RootNode.CalculateLayout();

        Assert.Equal(expected, child.Rect.W, 2);
    }

    [Fact]
    public void ExpandExpressionsShareRemainingSpaceByWeight()
    {
        var gui = CreateTestGui(400, 100);
        SetGuiStage(gui, Pass.Pass1Build);
        gui.RootNode!.Style.Direction = Axis.Horizontal;
        var first = CreateTestLayoutNode(gui, gui.RootNode).Width(Size.Expand()).Height(20f);
        var second = CreateTestLayoutNode(gui, gui.RootNode).Width(Size.Expand(3f)).Height(20f);
        gui.RootNode.AddChild(first);
        gui.RootNode.AddChild(second);

        gui.RootNode.CalculateLayout();

        Assert.Equal(100f, first.Rect.W, 2);
        Assert.Equal(300f, second.Rect.W, 2);
    }

    [Fact]
    public void FitLargestUsesLargestChildOnAxis()
    {
        var gui = CreateTestGui(400, 100);
        SetGuiStage(gui, Pass.Pass1Build);
        var container = CreateTestLayoutNode(gui, gui.RootNode).Width(Size.FitLargest()).Height(20f);
        container.Style.Direction = Axis.Vertical;
        container.AddChild(CreateTestLayoutNode(gui, container, 40f, 10f));
        container.AddChild(CreateTestLayoutNode(gui, container, 90f, 10f));
        gui.RootNode!.AddChild(container);

        gui.RootNode.CalculateLayout();

        Assert.Equal(90f, container.Rect.W, 2);
    }
}
