namespace Guinevere.Tests;

/// <summary>
/// Tests for the layout constraints added for UI-Toolkit parity: percentage sizing and
/// min/max width/height clamping.
/// </summary>
public class LayoutConstraintsTests : LayoutNodeTestBase
{
    private (LayoutNode root, LayoutNode child) Tree(Gui gui, Action<LayoutNode> configure,
        float rootW = 800f, float rootH = 600f)
    {
        var root = LayoutNode.CreateRoot(gui, rootW, rootH);
        var child = CreateTestLayoutNode(gui, root);
        configure(child);
        root.AddChild(child);
        root.CalculateLayout();
        return (root, child);
    }

    /// <summary>A percentage width resolves against the parent's inner width.</summary>
    [Theory]
    [InlineData(0.5f, 400f)]
    [InlineData(0.25f, 200f)]
    [InlineData(1.0f, 800f)]
    public void WidthPercent_ResolvesAgainstParent(float fraction, float expected)
    {
        var gui = CreateTestGui();
        var (_, child) = Tree(gui, c => c.WidthPercent(fraction).Height(50f));

        Assert.Equal(expected, child.Rect.W, 1);
    }

    /// <summary>A percentage height resolves against the parent's inner height (padding included).</summary>
    [Fact]
    public void HeightPercent_AccountsForParentPadding()
    {
        var gui = CreateTestGui();
        var root = LayoutNode.CreateRoot(gui, 800f, 600f);
        root.Padding(50f);
        var child = CreateTestLayoutNode(gui, root).HeightPercent(0.5f).Width(100f);
        root.AddChild(child);
        root.CalculateLayout();

        // inner height = 600 - 2*50 = 500; 50% => 250
        Assert.Equal(250f, child.Rect.H, 1);
    }

    /// <summary>MaxWidth clamps a would-be-wider child.</summary>
    [Fact]
    public void MaxWidth_ClampsExpandingChild()
    {
        var gui = CreateTestGui();
        var (_, child) = Tree(gui, c => c.Expand().MaxWidth(300f));

        Assert.Equal(300f, child.Rect.W, 1);
    }

    /// <summary>MinWidth widens a would-be-narrower child.</summary>
    [Fact]
    public void MinWidth_WidensSmallChild()
    {
        var gui = CreateTestGui();
        var (_, child) = Tree(gui, c => c.Width(40f).Height(40f).MinWidth(120f));

        Assert.Equal(120f, child.Rect.W, 1);
    }

    /// <summary>MaxHeight clamps a percentage height that would exceed it.</summary>
    [Fact]
    public void MaxHeight_ClampsPercentHeight()
    {
        var gui = CreateTestGui();
        var (_, child) = Tree(gui, c => c.Width(100f).HeightPercent(0.9f).MaxHeight(200f));

        Assert.Equal(200f, child.Rect.H, 1);
    }

    /// <summary>Constraints on the node's own size apply when it is the root's single child filling it.</summary>
    [Fact]
    public void HeightConstraint_AppliesToOwnSize()
    {
        var gui = CreateTestGui();
        var (_, child) = Tree(gui, c => c.Width(100f).HeightConstraint(min: 80f, max: 150f).Height(400f));

        Assert.Equal(150f, child.Rect.H, 1);
    }

    /// <summary>An unset constraint (-1) never changes the size.</summary>
    [Fact]
    public void UnsetConstraints_AreNoOps()
    {
        var gui = CreateTestGui();
        var (_, child) = Tree(gui, c => c.Width(123f).Height(45f));

        Assert.Equal(123f, child.Rect.W, 1);
        Assert.Equal(45f, child.Rect.H, 1);
    }
}
