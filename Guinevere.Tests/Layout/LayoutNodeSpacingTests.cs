namespace Guinevere.Tests.Layout;

/// <summary>Padding, margins, gaps and alignment resolved from <see cref="Size"/> values.</summary>
public class LayoutNodeSpacingTests : LayoutNodeTestBase
{
    /// <summary>A container at the root's top-left holding fixed-size children.</summary>
    (LayoutNode Root, LayoutNode Container, LayoutNode[] Children) Flow(Axis axis, float width, float height,
        Action<LayoutNode> configure, params (float W, float H)[] children)
    {
        var gui = CreateTestGui();
        var root = LayoutNode.CreateRoot(gui, 800f, 600f);
        var container = CreateTestLayoutNode(gui, root, width, height).Direction(axis);
        configure(container);
        root.AddChild(container);
        var nodes = new LayoutNode[children.Length];
        for (var i = 0; i < children.Length; i++)
        {
            nodes[i] = container.AddChild(CreateTestLayoutNode(gui, container, children[i].W, children[i].H));
        }

        return (root, container, nodes);
    }

    [Fact]
    public void PercentagePaddingResolvesAgainstTheParentContentBox()
    {
        var (root, container, _) = Flow(Axis.Vertical, 200f, 100f,
            static c => c.Padding(Size.Percentage(0.05f)), (10f, 10f));

        root.CalculateLayout();

        // 5% of the root's 800 x 600 content box: 40 horizontally, 30 vertically.
        Assert.Equal(new Rect(40f, 30f, 120f, 40f), container.InnerRect);
    }

    [Fact]
    public void PercentageGapUsesTheContainerContentBoxAlongTheFlow()
    {
        var (root, _, children) = Flow(Axis.Vertical, 100f, 300f,
            static c => c.Gap(Size.Percentage(0.1f)), (50f, 50f), (50f, 50f));

        root.CalculateLayout();

        Assert.Equal(80f, children[1].Rect.Y, 2);
    }

    [Fact]
    public void ExpandingGapSpacesChildrenBetween()
    {
        var (root, _, children) = Flow(Axis.Horizontal, 400f, 50f,
            static c => c.Gap(Size.Expand()), (50f, 50f), (50f, 50f), (50f, 50f));

        root.CalculateLayout();

        Assert.Equal([0f, 175f, 350f], children.Select(static c => c.Rect.X));
    }

    [Theory]
    [InlineData(SpacingTypes.SpaceBetween, 0f, 175f, 350f)]
    [InlineData(SpacingTypes.SpaceEvenly, 62.5f, 175f, 287.5f)]
    [InlineData(SpacingTypes.SpaceAround, 41.667f, 175f, 308.333f)]
    [InlineData(SpacingTypes.Center, 125f, 175f, 225f)]
    [InlineData(SpacingTypes.End, 250f, 300f, 350f)]
    public void SpacingDistributesFreeSpaceAlongTheFlow(SpacingTypes spacing, float first, float second, float third)
    {
        var (root, _, children) = Flow(Axis.Horizontal, 400f, 50f,
            c => c.Spacing(spacing), (50f, 50f), (50f, 50f), (50f, 50f));

        root.CalculateLayout();

        Assert.Equal(first, children[0].Rect.X, 2);
        Assert.Equal(second, children[1].Rect.X, 2);
        Assert.Equal(third, children[2].Rect.X, 2);
    }

    [Fact]
    public void CenteredContentWithGapsIsCenteredAsAGroup()
    {
        var (root, _, children) = Flow(Axis.Vertical, 100f, 300f,
            static c => c.Gap(10f).AlignContentY(0.5f), (50f, 50f), (50f, 50f), (50f, 50f));

        root.CalculateLayout();

        // 3 x 50 + 2 x 10 = 170 used, so 130 free and 65 above the group.
        Assert.Equal(65f, children[0].Rect.Y, 2);
        Assert.Equal(300f - 65f, children[2].Rect.Y + children[2].Rect.H, 2);
    }

    [Fact]
    public void ExpandingMarginsCenterAcrossAndPushAlongTheFlow()
    {
        var (root, _, children) = Flow(Axis.Vertical, 400f, 300f, static _ => { }, (100f, 50f), (100f, 50f));
        children[0].MarginLeft(Size.Expand()).MarginRight(Size.Expand());
        children[1].MarginTop(Size.Expand());

        root.CalculateLayout();

        Assert.Equal(150f, children[0].Rect.X, 2);
        Assert.Equal(250f, children[1].Rect.Y, 2);
    }

    [Fact]
    public void AlignSelfOverridesTheParentCrossAlignment()
    {
        var (root, _, children) = Flow(Axis.Vertical, 400f, 300f, static c => c.AlignContentX(0f),
            (100f, 50f), (100f, 50f));
        children[1].AlignSelf(1f);

        root.CalculateLayout();

        Assert.Equal(0f, children[0].Rect.X, 2);
        Assert.Equal(300f, children[1].Rect.X, 2);
    }

    [Fact]
    public void AlignmentAddsPixelOffsetsToTheFraction()
    {
        var (root, _, children) = Flow(Axis.Vertical, 400f, 300f,
            static c => c.AlignContent(Size.Percentage(0.5f) + 10f, Size.Pixels(5f)), (100f, 50f));

        root.CalculateLayout();

        Assert.Equal(160f, children[0].Rect.X, 2);
        Assert.Equal(5f, children[0].Rect.Y, 2);
    }

    [Fact]
    public void AbsoluteOffsetsResolvePercentagesOfTheirBox()
    {
        var (root, _, children) = Flow(Axis.Vertical, 200f, 100f, static _ => { }, (20f, 20f));
        children[0].Absolute(Size.Percentage(0.5f), Size.Percentage(0.25f));

        root.CalculateLayout();

        Assert.Equal(new Vector2(100f, 25f), children[0].Rect.Position);
    }

    [Fact]
    public void RelativeSpacingDisablesTheIntrinsicSizeCache()
    {
        var (root, container, _) = Flow(Axis.Vertical, -1f, -1f,
            static c => c.Width(Size.Fit).Padding(Size.Percentage(0.01f)), (100f, 100f));

        root.CalculateLayout();

        // 1% of 800 on each side horizontally and 1% of 600 vertically around the 100 x 100 child.
        Assert.Equal(116f, container.Rect.W, 2);
        Assert.Equal(112f, container.Rect.H, 2);
    }
}
