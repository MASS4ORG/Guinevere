namespace Guinevere.Tests;

/// <summary>
/// Tests for the layout constraints added for UI-Toolkit parity: percentage sizing and
/// min/max width/height clamping.
/// </summary>
public class LayoutConstraintsTests : LayoutNodeTestBase
{
    LayoutNode Tree(Gui gui, Action<LayoutNode> configure,
        float rootW = 800f, float rootH = 600f)
    {
        var root = LayoutNode.CreateRoot(gui, rootW, rootH);
        var child = CreateTestLayoutNode(gui, root);
        configure(child);
        root.AddChild(child);
        root.CalculateLayout();
        return child;
    }

    /// <summary>A percentage width resolves against the parent's inner width.</summary>
    [Theory]
    [InlineData(0.5f, 400f)]
    [InlineData(0.25f, 200f)]
    [InlineData(1.0f, 800f)]
    public void WidthPercent_ResolvesAgainstParent(float fraction, float expected)
    {
        var gui = CreateTestGui();
        var child = Tree(gui, c => c.WidthPercent(fraction).Height(50f));

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
        var child = Tree(gui, c => c.Expand().MaxWidth(300f));

        Assert.Equal(300f, child.Rect.W, 1);
    }

    /// <summary>MinWidth widens a would-be-narrower child.</summary>
    [Fact]
    public void MinWidth_WidensSmallChild()
    {
        var gui = CreateTestGui();
        var child = Tree(gui, c => c.Width(40f).Height(40f).MinWidth(120f));

        Assert.Equal(120f, child.Rect.W, 1);
    }

    /// <summary>MaxHeight clamps a percentage height that would exceed it.</summary>
    [Fact]
    public void MaxHeight_ClampsPercentHeight()
    {
        var gui = CreateTestGui();
        var child = Tree(gui, c => c.Width(100f).HeightPercent(0.9f).MaxHeight(200f));

        Assert.Equal(200f, child.Rect.H, 1);
    }

    /// <summary>Constraints on the node's own size apply when it is the root's single child filling it.</summary>
    [Fact]
    public void HeightConstraint_AppliesToOwnSize()
    {
        var gui = CreateTestGui();
        var child = Tree(gui, c => c.Width(100f).HeightConstraint(min: 80f, max: 150f).Height(400f));

        Assert.Equal(150f, child.Rect.H, 1);
    }

    /// <summary>An unset constraint (-1) never changes the size.</summary>
    [Fact]
    public void UnsetConstraints_AreNoOps()
    {
        var gui = CreateTestGui();
        var child = Tree(gui, c => c.Width(123f).Height(45f));

        Assert.Equal(123f, child.Rect.W, 1);
        Assert.Equal(45f, child.Rect.H, 1);
    }

    /// <summary>A wrapped row breaks children onto new lines and stacks the lines on the cross axis.</summary>
    [Fact]
    public void WrappedRow_BreaksChildrenIntoLines()
    {
        var gui = CreateTestGui(250, 400);
        var root = LayoutNode.CreateRoot(gui, 250f, 400f);

        var row = CreateTestLayoutNode(gui, root).Direction(Axis.Horizontal).Wrap(0).Gap(10f);
        root.AddChild(row);

        // Five 100-wide items in a 250-wide row → 2 per line (100 + 10 + 100 = 210 ≤ 250).
        var items = new LayoutNode[5];
        for (var i = 0; i < 5; i++)
        {
            items[i] = CreateTestLayoutNode(gui, row).Width(100f).Height(30f);
            row.AddChild(items[i]);
        }

        root.CalculateLayout();

        Assert.Equal(items[0].Rect.Y, items[1].Rect.Y, 1);           // line 1
        Assert.True(items[2].Rect.Y > items[0].Rect.Y + 20f);        // line 2 is lower
        Assert.Equal(items[2].Rect.Y, items[3].Rect.Y, 1);
        Assert.True(items[4].Rect.Y > items[2].Rect.Y + 20f);        // line 3
        Assert.Equal(items[0].Rect.X, items[2].Rect.X, 1);           // lines start at the same X

        // Three lines of 30 + two 10 gaps = 110.
        Assert.Equal(110f, row.Rect.H, 1);
    }

    /// <summary>
    /// A content-sized container's own padding is added to its size, so fixed-size children fit
    /// inside its inner box instead of overflowing (the "content spills past the card" bug).
    /// </summary>
    [Fact]
    public void ContentSizedContainer_IncludesOwnPadding()
    {
        var gui = CreateTestGui();
        var root = LayoutNode.CreateRoot(gui, 800f, 600f);

        var card = CreateTestLayoutNode(gui, root).Padding(16f).Direction(Axis.Vertical).Gap(10f);
        root.AddChild(card);

        var a = CreateTestLayoutNode(gui, card).Width(100f).Height(40f);
        var b = CreateTestLayoutNode(gui, card).Width(100f).Height(60f);
        card.AddChild(a);
        card.AddChild(b);

        root.CalculateLayout();

        // children 40 + 60 + gap 10 = 110, plus 16 top + 16 bottom padding = 142
        Assert.Equal(142f, card.Rect.H, 1);
        Assert.True(b.Rect.Y + b.Rect.H <= card.Rect.Y + card.Rect.H - 16f + 0.5f,
            "the bottom child must not overflow the card's bottom padding");
    }
}
