namespace Guinevere.Tests;

/// <summary>
/// Verifies that layout node calculation produces correct positioning and sizing for various configurations.
/// </summary>
public class LayoutNodeCalculationTests : LayoutNodeTestBase
{
    #region Single Node Layout Tests

    /// <summary>
    /// Verifies that the root node uses the screen rectangle as its layout bounds.
    /// </summary>
    [Theory]
    [InlineData(800f, 600f)]
    [InlineData(1024f, 768f)]
    [InlineData(400f, 300f)]
    public void RootNode_CalculateLayout_UsesScreenRect(float screenWidth, float screenHeight)
    {
        // Arrange
        var gui = CreateTestGui((int)screenWidth, (int)screenHeight);
        var root = LayoutNode.CreateRoot(gui, screenWidth, screenHeight);

        // Act
        root.CalculateLayout();

        // Assert
        AssertRectValues(root.Rect, 0f, 0f, screenWidth, screenHeight);
    }

    /// <summary>
    /// Verifies that a single child node with explicit width and height calculates its rect correctly.
    /// </summary>
    [Theory]
    [InlineData(100f, 80f)]
    [InlineData(200f, 150f)]
    [InlineData(50f, 40f)]
    public void SingleChildNode_WithExplicitSize_CalculatesCorrectly(float width, float height)
    {
        // Arrange
        var gui = CreateTestGui();
        var root = LayoutNode.CreateRoot(gui, 800f, 600f);
        var child = CreateTestLayoutNode(gui, root).Width(width).Height(height);
        root.AddChild(child);

        // Act
        root.CalculateLayout();

        // Assert
        AssertRectValues(child.Rect, 0f, 0f, width, height);
    }

    /// <summary>
    /// Verifies that a child node with full expansion fills its parent's entire bounds.
    /// </summary>
    [Fact]
    public void SingleChildNode_WithExpansion_FillsParent()
    {
        // Arrange
        var gui = CreateTestGui();
        var root = LayoutNode.CreateRoot(gui, 800f, 600f);
        var child = CreateTestLayoutNode(gui, root).Expand();
        root.AddChild(child);

        // Act
        root.CalculateLayout();

        // Assert
        AssertRectValues(child.Rect, 0f, 0f, 800f, 600f);
    }

    /// <summary>
    /// Verifies that a child node with partial expansion calculates dimensions using the given percentages.
    /// </summary>
    [Theory]
    [InlineData(0.5f, 0.7f)]
    [InlineData(0.3f, 0.8f)]
    [InlineData(1.0f, 1.0f)]
    public void SingleChildNode_WithPartialExpansion_CalculatesCorrectly(float widthPercent, float heightPercent)
    {
        // Arrange
        var gui = CreateTestGui();
        var root = LayoutNode.CreateRoot(gui, 800f, 600f);
        var child = CreateTestLayoutNode(gui, root).Expand(widthPercent, heightPercent);
        root.AddChild(child);

        // Act
        root.CalculateLayout();

        // Assert
        var expectedWidth = 800f * widthPercent;
        var expectedHeight = 600f * heightPercent;
        AssertRectValues(child.Rect, 0f, 0f, expectedWidth, expectedHeight);
    }

    #endregion

    #region Margin and Padding Layout Tests

    /// <summary>
    /// Verifies that a child node with uniform margin is positioned correctly with reduced size and proper outer rect.
    /// </summary>
    [Theory]
    [InlineData(10f)]
    [InlineData(20f)]
    [InlineData(5f)]
    public void SingleChildNode_WithUniformMargin_CalculatesCorrectLayout(float margin)
    {
        // Arrange
        var gui = CreateTestGui(200, 200);
        var root = LayoutNode.CreateRoot(gui, 200f, 200f);
        var child = CreateTestLayoutNode(gui, root).Expand().Margin(margin);
        root.AddChild(child);

        // Act
        root.CalculateLayout();

        // Assert
        // Child should be positioned with margin offset and reduced size
        var expectedWidth = 200f - (2 * margin);
        var expectedHeight = 200f - (2 * margin);
        AssertRectValues(child.Rect, margin, margin, expectedWidth, expectedHeight);

        // OuterRect should extend back to original bounds
        AssertRectValues(child.OuterRect, 0f, 0f, 200f, 200f);
    }

    /// <summary>
    /// Verifies that a child node with asymmetric margins positions correctly using individual top, right, bottom, and left values.
    /// </summary>
    [Theory]
    [InlineData(5f, 10f, 15f, 20f)]
    [InlineData(2f, 4f, 6f, 8f)]
    public void SingleChildNode_WithAsymmetricMargin_CalculatesCorrectLayout(float top, float right, float bottom,
        float left)
    {
        // Arrange
        var gui = CreateTestGui(200, 200);
        var root = LayoutNode.CreateRoot(gui, 200f, 200f);
        var child = CreateTestLayoutNode(gui, root).Expand().Margin(top, right, bottom, left);
        root.AddChild(child);

        // Act
        root.CalculateLayout();

        // Assert
        var expectedX = left;
        var expectedY = top;
        var expectedWidth = 200f - (left + right);
        var expectedHeight = 200f - (top + bottom);
        AssertRectValues(child.Rect, expectedX, expectedY, expectedWidth, expectedHeight);

        // OuterRect should extend back to original bounds
        AssertRectValues(child.OuterRect, 0f, 0f, 200f, 200f);
    }

    /// <summary>
    /// Verifies that parent padding affects the child layout by offsetting and shrinking the child's rect.
    /// </summary>
    [Theory]
    [InlineData(8f)]
    [InlineData(12f)]
    [InlineData(4f)]
    public void ParentWithPadding_AffectsChildLayout(float padding)
    {
        // Arrange
        var gui = CreateTestGui(200, 200);
        var parent = LayoutNode.CreateRoot(gui, 200f, 200f).Padding(padding);
        var child = CreateTestLayoutNode(gui, parent).Expand();
        parent.AddChild(child);

        // Act
        parent.CalculateLayout();

        // Assert
        // Child should be positioned within the parent's inner rect
        var expectedX = padding;
        var expectedY = padding;
        var expectedWidth = 200f - (2 * padding);
        var expectedHeight = 200f - (2 * padding);
        AssertRectValues(child.Rect, expectedX, expectedY, expectedWidth, expectedHeight);
    }

    #endregion

    #region Vertical Layout Tests

    /// <summary>
    /// Verifies that a vertical layout with two equal-height children divides the space evenly.
    /// </summary>
    [Fact]
    public void VerticalLayout_WithTwoEqualChildren_DividesSpaceEvenly()
    {
        // Arrange
        var gui = CreateTestGui(200, 200);
        var root = LayoutNode.CreateRoot(gui, 200f, 200f).Direction(Axis.Vertical);
        var child1 = CreateTestLayoutNode(gui, root).ExpandHeight();
        var child2 = CreateTestLayoutNode(gui, root).ExpandHeight();
        root.AddChild(child1);
        root.AddChild(child2);

        // Act
        root.CalculateLayout();

        // Assert
        AssertRectValues(child1.Rect, 0f, 0f, 200f, 100f);
        AssertRectValues(child2.Rect, 0f, 100f, 200f, 100f);
    }

    /// <summary>
    /// Verifies that a vertical layout with a fixed-height child and an expanding child calculates dimensions correctly.
    /// </summary>
    [Fact]
    public void VerticalLayout_WithFixedAndExpandingChild_CalculatesCorrectly()
    {
        // Arrange
        var gui = CreateTestGui(200, 200);
        var root = LayoutNode.CreateRoot(gui, 200f, 200f).Direction(Axis.Vertical);
        var fixedChild = CreateTestLayoutNode(gui, root).Height(50f);
        var expandingChild = CreateTestLayoutNode(gui, root).ExpandHeight();
        root.AddChild(fixedChild);
        root.AddChild(expandingChild);

        // Act
        root.CalculateLayout();

        // Assert
        AssertRectValues(fixedChild.Rect, 0f, 0f, 200f, 50f);
        AssertRectValues(expandingChild.Rect, 0f, 50f, 200f, 150f);
    }

    /// <summary>
    /// Verifies that a vertical layout with gap adds the correct amount of space between children.
    /// </summary>
    [Theory]
    [InlineData(5f)]
    [InlineData(10f)]
    [InlineData(15f)]
    public void VerticalLayout_WithGap_AddsSpaceBetweenChildren(float gap)
    {
        // Arrange
        var gui = CreateTestGui(200, 200);
        var root = LayoutNode.CreateRoot(gui, 200f, 200f).Direction(Axis.Vertical).Gap(gap);
        var child1 = CreateTestLayoutNode(gui, root).Height(50f);
        var child2 = CreateTestLayoutNode(gui, root).Height(50f);
        root.AddChild(child1);
        root.AddChild(child2);

        // Act
        root.CalculateLayout();

        // Assert
        AssertRectValues(child1.Rect, 0f, 0f, 200f, 50f);
        AssertRectValues(child2.Rect, 0f, 50f + gap, 200f, 50f);
    }

    /// <summary>
    /// Verifies that child margins are applied correctly in a vertical layout affecting position and size.
    /// </summary>
    [Fact]
    public void VerticalLayout_WithChildMargins_CalculatesCorrectly()
    {
        // Arrange
        var gui = CreateTestGui(200, 300);
        var root = LayoutNode.CreateRoot(gui, 200f, 300f).Direction(Axis.Vertical);
        var child1 = CreateTestLayoutNode(gui, root).Height(50f).Margin(10f);
        var child2 = CreateTestLayoutNode(gui, root).Height(50f).Margin(5f, 15f, 20f, 25f);
        root.AddChild(child1);
        root.AddChild(child2);

        // Act
        root.CalculateLayout();

        // Assert
        // Child1: positioned at (10, 10) with reduced width due to horizontal margins
        AssertRectValues(child1.Rect, 10f, 10f, 180f, 50f);

        // Child2: positioned after child1 + its bottom margin + child2's top margin
        // X position affected by the left margin, width reduced by horizontal margins
        var expectedChild2Y =
            10f + 50f + 10f + 5f; // child1.top + child1.height + child1.bottomMargin + child2.topMargin
        AssertRectValues(child2.Rect, 25f, expectedChild2Y, 160f, 50f); // width = 200 - 25 - 15 = 160
    }

    /// <summary>
    /// Verifies that a vertical layout with three children distributes height based on different expansion ratios.
    /// </summary>
    [Fact]
    public void VerticalLayout_WithThreeChildren_DifferentExpansionRatios()
    {
        // Arrange
        var gui = CreateTestGui(200, 300);
        var root = LayoutNode.CreateRoot(gui, 200f, 300f).Direction(Axis.Vertical);
        var child1 = CreateTestLayoutNode(gui, root).Expand(1.0f, 0.2f); // 20% height
        var child2 = CreateTestLayoutNode(gui, root).Expand(1.0f, 0.3f); // 30% height
        var child3 = CreateTestLayoutNode(gui, root).Expand(1.0f, 0.5f); // 50% height
        root.AddChild(child1);
        root.AddChild(child2);
        root.AddChild(child3);

        // Act
        root.CalculateLayout();

        // Assert
        // Total expansion ratio = 0.2 + 0.3 + 0.5 = 1.0
        var expectedHeight1 = 300f * (0.2f / 1.0f); // 60f
        var expectedHeight2 = 300f * (0.3f / 1.0f); // 90f
        var expectedHeight3 = 300f * (0.5f / 1.0f); // 150f

        AssertRectValues(child1.Rect, 0f, 0f, 200f, expectedHeight1);
        AssertRectValues(child2.Rect, 0f, expectedHeight1, 200f, expectedHeight2);
        AssertRectValues(child3.Rect, 0f, expectedHeight1 + expectedHeight2, 200f, expectedHeight3);
    }

    #endregion

    #region Horizontal Layout Tests

    /// <summary>
    /// Verifies that a horizontal layout with two equal-width children divides the space evenly.
    /// </summary>
    [Fact]
    public void HorizontalLayout_WithTwoEqualChildren_DividesSpaceEvenly()
    {
        // Arrange
        var gui = CreateTestGui(200, 200);
        var root = LayoutNode.CreateRoot(gui, 200f, 200f).Direction(Axis.Horizontal);
        var child1 = CreateTestLayoutNode(gui, root).ExpandWidth();
        var child2 = CreateTestLayoutNode(gui, root).ExpandWidth();
        root.AddChild(child1);
        root.AddChild(child2);

        // Act
        root.CalculateLayout();

        // Assert
        AssertRectValues(child1.Rect, 0f, 0f, 100f, 200f);
        AssertRectValues(child2.Rect, 100f, 0f, 100f, 200f);
    }

    /// <summary>
    /// Verifies that a horizontal layout with a fixed-width child and an expanding child calculates dimensions correctly.
    /// </summary>
    [Fact]
    public void HorizontalLayout_WithFixedAndExpandingChild_CalculatesCorrectly()
    {
        // Arrange
        var gui = CreateTestGui(300, 100);
        var root = LayoutNode.CreateRoot(gui, 300f, 100f).Direction(Axis.Horizontal);
        var fixedChild = CreateTestLayoutNode(gui, root).Width(80f);
        var expandingChild = CreateTestLayoutNode(gui, root).ExpandWidth();
        root.AddChild(fixedChild);
        root.AddChild(expandingChild);

        // Act
        root.CalculateLayout();

        // Assert
        AssertRectValues(fixedChild.Rect, 0f, 0f, 80f, 100f);
        AssertRectValues(expandingChild.Rect, 80f, 0f, 220f, 100f);
    }

    /// <summary>
    /// Verifies that a horizontal layout with gap adds the correct amount of space between children.
    /// </summary>
    [Theory]
    [InlineData(8f)]
    [InlineData(12f)]
    [InlineData(20f)]
    public void HorizontalLayout_WithGap_AddsSpaceBetweenChildren(float gap)
    {
        // Arrange
        var gui = CreateTestGui(300, 100);
        var root = LayoutNode.CreateRoot(gui, 300f, 100f).Direction(Axis.Horizontal).Gap(gap);
        var child1 = CreateTestLayoutNode(gui, root).Width(100f);
        var child2 = CreateTestLayoutNode(gui, root).Width(100f);
        root.AddChild(child1);
        root.AddChild(child2);

        // Act
        root.CalculateLayout();

        // Assert
        AssertRectValues(child1.Rect, 0f, 0f, 100f, 100f);
        AssertRectValues(child2.Rect, 100f + gap, 0f, 100f, 100f);
    }

    /// <summary>
    /// Verifies that child margins are applied correctly in a horizontal layout affecting position and size.
    /// </summary>
    [Fact]
    public void HorizontalLayout_WithChildMargins_CalculatesCorrectly()
    {
        // Arrange
        var gui = CreateTestGui(400, 100);
        var root = LayoutNode.CreateRoot(gui, 400f, 100f).Direction(Axis.Horizontal);
        var child1 = CreateTestLayoutNode(gui, root).Width(100f).Margin(5f, 10f, 15f, 20f);
        var child2 = CreateTestLayoutNode(gui, root).Width(100f).Margin(8f);
        root.AddChild(child1);
        root.AddChild(child2);

        // Act
        root.CalculateLayout();

        // Assert
        // Child1: positioned with left margin, height reduced by vertical margins
        AssertRectValues(child1.Rect, 20f, 5f, 100f, 80f); // height = 100 - 5 - 15 = 80

        // Child2: positioned after child1 + its right margin + child2's left margin
        var expectedChild2X =
            20f + 100f + 10f + 8f; // child1.left + child1.width + child1.rightMargin + child2.leftMargin
        AssertRectValues(child2.Rect, expectedChild2X, 8f, 100f, 84f); // height = 100 - 8 - 8 = 84
    }

    #endregion

    #region Nested Layout Tests

    /// <summary>
    /// Verifies that a nested vertical-in-horizontal layout calculates positions and sizes correctly across both axes.
    /// </summary>
    [Fact]
    public void NestedLayout_VerticalInHorizontal_CalculatesCorrectly()
    {
        // Arrange
        var gui = CreateTestGui(400, 200);
        var root = LayoutNode.CreateRoot(gui, 400f, 200f).Direction(Axis.Horizontal);

        var leftPanel = CreateTestLayoutNode(gui, root).Width(200f).Direction(Axis.Vertical);
        var rightPanel = CreateTestLayoutNode(gui, root).ExpandWidth().Direction(Axis.Vertical);

        var leftChild1 = CreateTestLayoutNode(gui, leftPanel).Height(80f);
        var leftChild2 = CreateTestLayoutNode(gui, leftPanel).ExpandHeight();

        var rightChild1 = CreateTestLayoutNode(gui, rightPanel).Height(60f);
        var rightChild2 = CreateTestLayoutNode(gui, rightPanel).ExpandHeight();

        root.AddChild(leftPanel);
        root.AddChild(rightPanel);
        leftPanel.AddChild(leftChild1);
        leftPanel.AddChild(leftChild2);
        rightPanel.AddChild(rightChild1);
        rightPanel.AddChild(rightChild2);

        // Act
        root.CalculateLayout();

        // Assert
        // Left panel
        AssertRectValues(leftPanel.Rect, 0f, 0f, 200f, 200f);
        AssertRectValues(leftChild1.Rect, 0f, 0f, 200f, 80f);
        AssertRectValues(leftChild2.Rect, 0f, 80f, 200f, 120f);

        // Right panel
        AssertRectValues(rightPanel.Rect, 200f, 0f, 200f, 200f);
        AssertRectValues(rightChild1.Rect, 200f, 0f, 200f, 60f);
        AssertRectValues(rightChild2.Rect, 200f, 60f, 200f, 140f);
    }

    /// <summary>
    /// Verifies that nested margins and padding compound correctly through multiple layout levels.
    /// </summary>
    [Fact]
    public void NestedLayout_WithMarginsAndPadding_CalculatesCorrectly()
    {
        // Arrange
        var gui = CreateTestGui(300, 200);
        var root = LayoutNode.CreateRoot(gui, 300f, 200f)
            .Direction(Axis.Vertical)
            .Padding(10f);

        var container = CreateTestLayoutNode(gui, root)
            .ExpandWidth()
            .Height(100f)
            .Margin(5f)
            .Padding(8f)
            .Direction(Axis.Horizontal);

        var child1 = CreateTestLayoutNode(gui, container).ExpandWidth();
        var child2 = CreateTestLayoutNode(gui, container).ExpandWidth();

        root.AddChild(container);
        container.AddChild(child1);
        container.AddChild(child2);

        // Act
        root.CalculateLayout();

        // Assert
        // Root has 10 px padding on all sides, so inner rect is (10, 10, 280, 180)
        // Container has 5 px margin, so positioned at (15, 15) with size (270, 100)
        AssertRectValues(container.Rect, 15f, 15f, 270f, 100f);

        // Container has 8 px padding, so inner rect is (23, 23, 254, 84)
        // Children split the inner width equally: 254/2 = 127 each
        AssertRectValues(child1.Rect, 23f, 23f, 127f, 84f);
        AssertRectValues(child2.Rect, 150f, 23f, 127f, 84f);
    }

    /// <summary>
    /// Verifies that a three-level nested layout calculates all node positions and sizes correctly.
    /// </summary>
    [Fact]
    public void NestedLayout_ThreeLevelsDeep_CalculatesCorrectly()
    {
        // Arrange
        var gui = CreateTestGui(400, 300);
        var root = LayoutNode.CreateRoot(gui, 400f, 300f).Direction(Axis.Vertical);

        var level1 = CreateTestLayoutNode(gui, root).Expand().Direction(Axis.Horizontal);
        var level2Left = CreateTestLayoutNode(gui, level1).Width(200f).Direction(Axis.Vertical);
        var level2Right = CreateTestLayoutNode(gui, level1).ExpandWidth().Direction(Axis.Vertical);

        var level31 = CreateTestLayoutNode(gui, level2Left).Height(100f);
        var level32 = CreateTestLayoutNode(gui, level2Left).ExpandHeight();
        var level33 = CreateTestLayoutNode(gui, level2Right).ExpandHeight();

        root.AddChild(level1);
        level1.AddChild(level2Left);
        level1.AddChild(level2Right);
        level2Left.AddChild(level31);
        level2Left.AddChild(level32);
        level2Right.AddChild(level33);

        // Act
        root.CalculateLayout();

        // Assert
        AssertRectValues(level1.Rect, 0f, 0f, 400f, 300f);
        AssertRectValues(level2Left.Rect, 0f, 0f, 200f, 300f);
        AssertRectValues(level2Right.Rect, 200f, 0f, 200f, 300f);
        AssertRectValues(level31.Rect, 0f, 0f, 200f, 100f);
        AssertRectValues(level32.Rect, 0f, 100f, 200f, 200f);
        AssertRectValues(level33.Rect, 200f, 0f, 200f, 300f);
    }

    #endregion

    #region Edge Cases and Complex Scenarios

    /// <summary>
    /// Verifies that a zero-sized parent handles layout gracefully without errors.
    /// </summary>
    [Fact]
    public void Layout_WithZeroSizedParent_HandlesGracefully()
    {
        // Arrange
        var gui = CreateTestGui(0, 0);
        var root = LayoutNode.CreateRoot(gui, 0f, 0f);
        var child = CreateTestLayoutNode(gui, root).Expand();
        root.AddChild(child);

        // Act
        root.CalculateLayout();

        // Assert
        AssertRectValues(root.Rect, 0f, 0f, 0f, 0f);
        AssertRectValues(child.Rect, 0f, 0f, 0f, 0f);
    }

    /// <summary>
    /// Verifies that negative margins cause the child to extend beyond the parent bounds correctly.
    /// </summary>
    [Fact]
    public void Layout_WithNegativeMargins_CalculatesCorrectly()
    {
        // Arrange
        var gui = CreateTestGui(200, 200);
        var root = LayoutNode.CreateRoot(gui, 200f, 200f);
        var child = CreateTestLayoutNode(gui, root).Expand().Margin(-10f);
        root.AddChild(child);

        // Act
        root.CalculateLayout();

        // Assert
        // Child should extend beyond parent bounds due to the negative margin
        AssertRectValues(child.Rect, -10f, -10f, 220f, 220f);
        AssertRectValues(child.OuterRect, 0f, 0f, 200f, 200f);
    }

    /// <summary>
    /// Verifies that excessive margins clamp the child size to a minimum viable dimension.
    /// </summary>
    [Fact]
    public void Layout_WithExcessiveMargins_ClampsToMinimumSize()
    {
        // Arrange
        var gui = CreateTestGui(100, 100);
        var root = LayoutNode.CreateRoot(gui, 100f, 100f);
        var child = CreateTestLayoutNode(gui, root).Expand().Margin(60f); // More than half the available space
        root.AddChild(child);

        // Act
        root.CalculateLayout();

        // Assert
        // Child should have a minimum viable size even with excessive margins
        var expectedWidth = Math.Max(0f, 100f - 120f); // Should be 0 or clamped to a minimum
        var expectedHeight = Math.Max(0f, 100f - 120f);
        AssertRectValues(child.Rect, 60f, 60f, expectedWidth, expectedHeight);
    }

    /// <summary>
    /// Verifies that a mix of fixed and expanding children distributes remaining space correctly after fixed sizes are allocated.
    /// </summary>
    [Fact]
    public void Layout_MixedFixedAndExpandingChildren_DistributesSpaceCorrectly()
    {
        // Arrange
        var gui = CreateTestGui(400, 300);
        var root = LayoutNode.CreateRoot(gui, 400f, 300f).Direction(Axis.Vertical);

        var fixed1 = CreateTestLayoutNode(gui, root).Height(50f);
        var expanding1 = CreateTestLayoutNode(gui, root).Expand(1.0f, 0.6f);
        var fixed2 = CreateTestLayoutNode(gui, root).Height(30f);
        var expanding2 = CreateTestLayoutNode(gui, root).Expand(1.0f, 0.4f);
        var fixed3 = CreateTestLayoutNode(gui, root).Height(40f);

        root.AddChild(fixed1);
        root.AddChild(expanding1);
        root.AddChild(fixed2);
        root.AddChild(expanding2);
        root.AddChild(fixed3);

        // Act
        root.CalculateLayout();

        // Assert
        // Fixed children take their specified heights: 50 + 30 + 40 = 120
        // Remaining space: 300 - 120 = 180
        // Expanding children split remaining space: 0.6 + 0.4 = 1.0
        var expandingHeight1 = 180f * 0.6f; // 108f
        var expandingHeight2 = 180f * 0.4f; // 72f

        AssertRectValues(fixed1.Rect, 0f, 0f, 400f, 50f);
        AssertRectValues(expanding1.Rect, 0f, 50f, 400f, expandingHeight1);
        AssertRectValues(fixed2.Rect, 0f, 158f, 400f, 30f); // 50 + 108
        AssertRectValues(expanding2.Rect, 0f, 188f, 400f, expandingHeight2); // 50 + 108 + 30
        AssertRectValues(fixed3.Rect, 0f, 260f, 400f, 40f); // 50 + 108 + 30 + 72
    }

    #endregion
}
