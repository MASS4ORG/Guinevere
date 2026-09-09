namespace Guinevere.Tests;

/// <summary>
/// Verifies the behavior of layout node properties.
/// </summary>
public class LayoutNodePropertiesTests : LayoutNodeTestBase
{
    #region Margin Properties Tests

    /// <summary>
    /// Verifies that margin properties with a uniform margin value return the correct values.
    /// </summary>
    [Theory]
    [InlineData(10f)]
    [InlineData(0f)]
    [InlineData(25f)]
    public void MarginProperties_WithUniformMargin_ReturnCorrectValues(float margin)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();
        node.Margin(margin);

        // Act & Assert
        Assert.Equal(margin, node.Style.MarginTop);
        Assert.Equal(margin, node.Style.MarginRight);
        Assert.Equal(margin, node.Style.MarginBottom);
        Assert.Equal(margin, node.Style.MarginLeft);
    }

    /// <summary>
    /// Verifies that margin properties with horizontal and vertical values return the correct values.
    /// </summary>
    [Theory]
    [InlineData(5f, 10f)]
    [InlineData(0f, 15f)]
    [InlineData(20f, 0f)]
    public void MarginProperties_WithHorizontalVertical_ReturnCorrectValues(float horizontal, float vertical)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();
        node.Margin(horizontal, vertical);

        // Act & Assert
        Assert.Equal(vertical, node.Style.MarginTop);
        Assert.Equal(horizontal, node.Style.MarginRight);
        Assert.Equal(vertical, node.Style.MarginBottom);
        Assert.Equal(horizontal, node.Style.MarginLeft);
    }

    /// <summary>
    /// Verifies that margin properties with individual top, right, bottom, and left values return the correct values.
    /// </summary>
    [Theory]
    [InlineData(1f, 2f, 3f, 4f)]
    [InlineData(0f, 5f, 10f, 15f)]
    [InlineData(25f, 20f, 15f, 10f)]
    public void MarginProperties_WithIndividualValues_ReturnCorrectValues(float top, float right, float bottom,
        float left)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();
        node.Margin(top, right, bottom, left);

        // Act & Assert
        Assert.Equal(top, node.Style.MarginTop);
        Assert.Equal(right, node.Style.MarginRight);
        Assert.Equal(bottom, node.Style.MarginBottom);
        Assert.Equal(left, node.Style.MarginLeft);
    }

    /// <summary>
    /// Verifies that margin properties have default values of zero.
    /// </summary>
    [Fact]
    public void MarginProperties_DefaultValues_ReturnZero()
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act & Assert
        Assert.Equal(0f, node.Style.MarginTop);
        Assert.Equal(0f, node.Style.MarginRight);
        Assert.Equal(0f, node.Style.MarginBottom);
        Assert.Equal(0f, node.Style.MarginLeft);
    }

    #endregion

    #region Padding Properties Tests

    /// <summary>
    /// Verifies that padding properties with a uniform padding value return the correct values.
    /// </summary>
    [Theory]
    [InlineData(8f)]
    [InlineData(0f)]
    [InlineData(15f)]
    public void PaddingProperties_WithUniformPadding_ReturnCorrectValues(float padding)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();
        node.Padding(padding);

        // Act & Assert
        Assert.Equal(padding, node.Style.PaddingTop);
        Assert.Equal(padding, node.Style.PaddingRight);
        Assert.Equal(padding, node.Style.PaddingBottom);
        Assert.Equal(padding, node.Style.PaddingLeft);
    }

    /// <summary>
    /// Verifies that padding properties with horizontal and vertical values return the correct values.
    /// </summary>
    [Theory]
    [InlineData(6f, 12f)]
    [InlineData(0f, 8f)]
    [InlineData(15f, 0f)]
    public void PaddingProperties_WithHorizontalVertical_ReturnCorrectValues(float horizontal, float vertical)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();
        node.Padding(horizontal, vertical);

        // Act & Assert
        Assert.Equal(vertical, node.Style.PaddingTop);
        Assert.Equal(horizontal, node.Style.PaddingRight);
        Assert.Equal(vertical, node.Style.PaddingBottom);
        Assert.Equal(horizontal, node.Style.PaddingLeft);
    }

    /// <summary>
    /// Verifies that padding properties with individual top, right, bottom, and left values return the correct values.
    /// </summary>
    [Theory]
    [InlineData(2f, 4f, 6f, 8f)]
    [InlineData(0f, 3f, 6f, 9f)]
    [InlineData(20f, 15f, 10f, 5f)]
    public void PaddingProperties_WithIndividualValues_ReturnCorrectValues(float top, float right, float bottom,
        float left)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();
        node.Padding(top, right, bottom, left);

        // Act & Assert
        Assert.Equal(top, node.Style.PaddingTop);
        Assert.Equal(right, node.Style.PaddingRight);
        Assert.Equal(bottom, node.Style.PaddingBottom);
        Assert.Equal(left, node.Style.PaddingLeft);
    }

    /// <summary>
    /// Verifies that padding properties have default values of zero.
    /// </summary>
    [Fact]
    public void PaddingProperties_DefaultValues_ReturnZero()
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act & Assert
        Assert.Equal(0f, node.Style.PaddingTop);
        Assert.Equal(0f, node.Style.PaddingRight);
        Assert.Equal(0f, node.Style.PaddingBottom);
        Assert.Equal(0f, node.Style.PaddingLeft);
    }

    #endregion

    #region InnerRect Tests

    /// <summary>
    /// Verifies that the inner rect is calculated correctly with uniform padding.
    /// </summary>
    [Theory]
    [InlineData(100f, 100f, 200f, 150f, 10f)]
    [InlineData(50f, 75f, 120f, 80f, 5f)]
    [InlineData(0f, 0f, 100f, 100f, 0f)]
    public void InnerRect_WithUniformPadding_CalculatesCorrectly(float x, float y, float w, float h, float padding)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();
        node.Left(x).Top(y).Width(w).Height(h).Padding(padding);

        // Force rect update
        SetPrivateRect(node, new Rect(x, y, w, h));

        // Act
        var innerRect = node.InnerRect;

        // Assert
        var expectedX = x + padding;
        var expectedY = y + padding;
        var expectedW = w - (2 * padding);
        var expectedH = h - (2 * padding);

        AssertRectValues(innerRect, expectedX, expectedY, expectedW, expectedH);
    }

    /// <summary>
    /// Verifies that the inner rect is calculated correctly with asymmetric padding.
    /// </summary>
    [Theory]
    [InlineData(50f, 60f, 150f, 120f, 5f, 10f, 15f, 20f)]
    [InlineData(0f, 0f, 100f, 100f, 2f, 4f, 6f, 8f)]
    public void InnerRect_WithAsymmetricPadding_CalculatesCorrectly(float x, float y, float w, float h,
        float paddingTop, float paddingRight, float paddingBottom, float paddingLeft)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();
        node.Left(x).Top(y).Width(w).Height(h).Padding(paddingTop, paddingRight, paddingBottom, paddingLeft);
        SetPrivateRect(node, new Rect(x, y, w, h));

        // Act
        var innerRect = node.InnerRect;

        // Assert
        var expectedX = x + paddingLeft;
        var expectedY = y + paddingTop;
        var expectedW = w - (paddingLeft + paddingRight);
        var expectedH = h - (paddingTop + paddingBottom);

        AssertRectValues(innerRect, expectedX, expectedY, expectedW, expectedH);
    }

    /// <summary>
    /// Verifies that the inner rect equals the original rect when padding is zero.
    /// </summary>
    [Fact]
    public void InnerRect_WithZeroPadding_EqualsOriginalRect()
    {
        // Arrange
        var node = CreateNodeWithBuildStage();
        var rect = new Rect(25f, 35f, 100f, 80f);
        node.Left(rect.X).Top(rect.Y).Width(rect.W).Height(rect.H).Padding(0f);
        SetPrivateRect(node, rect);

        // Act
        var innerRect = node.InnerRect;

        // Assert
        AssertRectValues(innerRect, rect.X, rect.Y, rect.W, rect.H);
    }

    #endregion

    #region OuterRect Tests

    /// <summary>
    /// Verifies that the outer rect is calculated correctly with uniform margin.
    /// </summary>
    [Theory]
    [InlineData(100f, 100f, 200f, 150f, 10f)]
    [InlineData(50f, 75f, 120f, 80f, 5f)]
    [InlineData(0f, 0f, 100f, 100f, 0f)]
    public void OuterRect_WithUniformMargin_CalculatesCorrectly(float x, float y, float w, float h, float margin)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();
        node.Left(x).Top(y).Width(w).Height(h).Margin(margin);
        SetPrivateRect(node, new Rect(x, y, w, h));

        // Act
        var outerRect = node.OuterRect;

        // Assert
        var expectedX = x - margin;
        var expectedY = y - margin;
        var expectedW = w + (2 * margin);
        var expectedH = h + (2 * margin);

        AssertRectValues(outerRect, expectedX, expectedY, expectedW, expectedH);
    }

    /// <summary>
    /// Verifies that the outer rect is calculated correctly with asymmetric margin.
    /// </summary>
    [Theory]
    [InlineData(50f, 60f, 150f, 120f, 5f, 10f, 15f, 20f)]
    [InlineData(0f, 0f, 100f, 100f, 2f, 4f, 6f, 8f)]
    public void OuterRect_WithAsymmetricMargin_CalculatesCorrectly(float x, float y, float w, float h,
        float marginTop, float marginRight, float marginBottom, float marginLeft)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();
        node.Left(x).Top(y).Width(w).Height(h).Margin(marginTop, marginRight, marginBottom, marginLeft);
        SetPrivateRect(node, new Rect(x, y, w, h));

        // Act
        var outerRect = node.OuterRect;

        // Assert
        var expectedX = x - marginLeft;
        var expectedY = y - marginTop;
        var expectedW = w + (marginLeft + marginRight);
        var expectedH = h + (marginTop + marginBottom);

        AssertRectValues(outerRect, expectedX, expectedY, expectedW, expectedH);
    }

    /// <summary>
    /// Verifies that the outer rect equals the original rect when margin is zero.
    /// </summary>
    [Fact]
    public void OuterRect_WithZeroMargin_EqualsOriginalRect()
    {
        // Arrange
        var node = CreateNodeWithBuildStage();
        var rect = new Rect(25f, 35f, 100f, 80f);
        node.Left(rect.X).Top(rect.Y).Width(rect.W).Height(rect.H).Margin(0f);
        SetPrivateRect(node, rect);

        // Act
        var outerRect = node.OuterRect;

        // Assert
        AssertRectValues(outerRect, rect.X, rect.Y, rect.W, rect.H);
    }

    #endregion

    #region Rect and Center Tests

    /// <summary>
    /// Verifies that the Rect property returns the correct rect.
    /// </summary>
    [Theory]
    [InlineData(50f, 60f, 100f, 80f)]
    [InlineData(0f, 0f, 200f, 150f)]
    [InlineData(-10f, -5f, 50f, 30f)]
    public void Rect_ReturnsCorrectRect(float x, float y, float w, float h)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();
        var expectedRect = new Rect(x, y, w, h);
        SetPrivateRect(node, expectedRect);

        // Act
        var rect = node.Rect;

        // Assert
        AssertRectValues(rect, x, y, w, h);
    }

    /// <summary>
    /// Verifies that the Center property is calculated correctly.
    /// </summary>
    [Theory]
    [InlineData(50f, 60f, 100f, 80f)]
    [InlineData(0f, 0f, 200f, 150f)]
    [InlineData(-10f, -5f, 50f, 30f)]
    public void Center_CalculatesCorrectly(float x, float y, float w, float h)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();
        SetPrivateRect(node, new Rect(x, y, w, h));

        // Act
        var center = node.Center;

        // Assert
        var expectedCenterX = x + w / 2;
        var expectedCenterY = y + h / 2;
        Assert.Equal(expectedCenterX, center.X, 2);
        Assert.Equal(expectedCenterY, center.Y, 2);
    }

    #endregion

    #region Children and Parent Tests

    /// <summary>
    /// Verifies that the Children property returns a read-only list of child nodes.
    /// </summary>
    [Fact]
    public void Children_ReturnsReadOnlyList()
    {
        // Arrange
        var parent = CreateNodeWithBuildStage();
        var gui = CreateTestGui();
        var child1 = CreateTestLayoutNode(gui, parent);
        var child2 = CreateTestLayoutNode(gui, parent);

        parent.AddChild(child1);
        parent.AddChild(child2);

        // Act
        var children = parent.Children;

        // Assert
        Assert.IsAssignableFrom<IReadOnlyList<LayoutNode>>(children);
        Assert.Equal(2, children.Count);
        Assert.Contains(child1, children);
        Assert.Contains(child2, children);
    }

    /// <summary>
    /// Verifies that the Parent property returns the correct parent node.
    /// </summary>
    [Fact]
    public void Parent_ReturnsCorrectParent()
    {
        // Arrange
        var parent = CreateNodeWithBuildStage();
        var gui = CreateTestGui();
        var child = CreateTestLayoutNode(gui, parent);

        // Act & Assert
        Assert.Equal(parent, child.Parent);
        Assert.Null(parent.Parent);
    }

    #endregion

    #region ZIndex Tests

    /// <summary>
    /// Verifies that SetZIndex sets and returns the correct value.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(100)]
    [InlineData(-50)]
    public void SetZIndex_SetsAndReturnsCorrectValue(int zIndex)
    {
        // Arrange
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass1Build);

        // Act
        using var scope = gui.Node().Enter();
        gui.SetZIndex(zIndex);

        // Assert
        Assert.Equal(zIndex, scope.Node.Scope.Get<LayoutNodeScopeZIndex>().Value);
        Assert.Equal(zIndex, scope.Get<LayoutNodeScopeZIndex>().Value);
    }

    /// <summary>
    /// Verifies that the ZIndex has a default value of zero.
    /// </summary>
    [Fact]
    public void ZIndex_DefaultValue_IsZero()
    {
        // Arrange & Act
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass1Build);

        using var nodeScope = gui.Node().Enter();

        // Assert
        Assert.Equal(0, nodeScope.Get<LayoutNodeScopeZIndex>().Value);
    }

    /// <summary>
    /// Verifies that the ZIndex inherits from the parent node.
    /// </summary>
    [Fact]
    public void ZIndex_InheritsFromParent()
    {
        // Arrange
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass1Build);

        // Act
        var parentScope = gui.Node().Enter();
        var childScope = gui.Node().Enter();
        gui.SetZIndex(5);

        // Assert
        Assert.Equal(5, childScope.Get<LayoutNodeScopeZIndex>().Value);
        Assert.Equal(0, parentScope.Get<LayoutNodeScopeZIndex>().Value);
    }

    /// <summary>
    /// Verifies that a child node's ZIndex overrides the parent's ZIndex.
    /// </summary>
    [Fact]
    public void ZIndex_ChildOverridesParent()
    {
        // Arrange
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass1Build);

        // Act
        using var parentScope = gui.Node().Enter();
        gui.SetZIndex(5);
        using var childScope = gui.Node().Enter();
        gui.SetZIndex(10);

        // Assert
        Assert.Equal(5, parentScope.Node.Scope.Get<LayoutNodeScopeZIndex>().Value);
        Assert.Equal(10, childScope.Node.Scope.Get<LayoutNodeScopeZIndex>().Value);
        Assert.Equal(10, childScope.Get<LayoutNodeScopeZIndex>().Value); // Child's own ZIndex takes precedence
    }

    /// <summary>
    /// Verifies that ZIndex values across a complex node hierarchy are resolved and sorted correctly.
    /// </summary>
    [Fact]
    public void ZIndex_HierarchyIntegrationTest()
    {
        // Arrange
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass1Build);
        var flatList = new List<(int z, LayoutNode node)>();

        // Act - Create a complex hierarchy with different ZIndex values
        using (var level1Scope = gui.Node(300, 300).Enter())
        {
            gui.SetZIndex(5);
            var level1Node = level1Scope.Node;

            using (var level2Scope = gui.Node(200, 200).Enter())
            {
                // Level 2 doesn't set ZIndex - should inherit from level 1
                var level2Node = level2Scope.Node;

                using (var level3AScope = gui.Node(100, 100).Enter())
                {
                    gui.SetZIndex(15);
                    var level3ANode = level3AScope.Node;

                    // Add nodes to a flat list as renderer would
                    flatList.Add((level3ANode.Scope.Get<LayoutNodeScopeZIndex>().Value, level3ANode));
                }

                using (var level3BScope = gui.Node(100, 100).Enter())
                {
                    // Level 3b doesn't set ZIndex - should inherit from level 2 (which inherits from level 1)
                    var level3BNode = level3BScope.Node;

                    flatList.Add((level3BNode.Scope.Get<LayoutNodeScopeZIndex>().Value, level3BNode));
                }

                flatList.Add((level2Node.Scope.Get<LayoutNodeScopeZIndex>().Value, level2Node));
            }

            flatList.Add((level1Node.Scope.Get<LayoutNodeScopeZIndex>().Value, level1Node));
        }

        // Create another top-level node with a different ZIndex
        using (var topScope = gui.Node(100, 100).Enter())
        {
            gui.SetZIndex(1);
            var topNode = topScope.Node;
            flatList.Add((topNode.Scope.Get<LayoutNodeScopeZIndex>().Value, topNode));
        }

        // Sort as renderer would
        var sortedList = flatList.OrderBy(value => value.z).ToList();

        // Assert
        Assert.Equal(5, sortedList.Count);

        // Verify rendering order (low ZIndex first)
        Assert.Equal(1, sortedList[0].z); // Top-level node with ZIndex 1
        Assert.Equal(5, sortedList[1].z); // Level 1 node with ZIndex 5
        Assert.Equal(5, sortedList[2].z); // Level 2 node inheriting ZIndex 5
        Assert.Equal(5, sortedList[3].z); // Level 3b node inheriting ZIndex 5
        Assert.Equal(15, sortedList[4].z); // Level 3a node with ZIndex 15
    }

    #endregion

    #region DrawList Tests

    /// <summary>
    /// Verifies that the DrawList property is initialized.
    /// </summary>
    [Fact]
    public void DrawList_IsInitialized()
    {
        // Arrange & Act
        var node = CreateNodeWithBuildStage();

        // Assert
        Assert.NotNull(node.DrawList);
    }

    /// <summary>
    /// Verifies that the DrawList property can be set and retrieved.
    /// </summary>
    [Fact]
    public void DrawList_CanBeSetAndRetrieved()
    {
        // Arrange
        var node = CreateNodeWithBuildStage();
        var newDrawList = new DrawList();

        // Act
        node.DrawList = newDrawList;

        // Assert
        Assert.Same(newDrawList, node.DrawList);
    }

    #endregion

    #region Complex Property Interactions

    /// <summary>
    /// Verifies that margin and padding do not affect each other.
    /// </summary>
    [Fact]
    public void MarginAndPadding_DoNotAffectEachOther()
    {
        // Arrange
        var node = CreateNodeWithBuildStage();
        node.Left(50f).Top(50f).Width(100f).Height(100f);
        SetPrivateRect(node, new Rect(50f, 50f, 100f, 100f));

        // Act
        node.Margin(10f, 15f, 20f, 25f).Padding(5f, 8f, 12f, 16f);

        // Assert
        AssertMarginValues(node, 10f, 15f, 20f, 25f);
        AssertPaddingValues(node, 5f, 8f, 12f, 16f);

        var innerRect = node.InnerRect;
        var outerRect = node.OuterRect;

        // Inner rect should only be affected by padding
        AssertRectValues(innerRect, 66f, 55f, 76f, 83f); // 50+16, 50+5, 100-8-16, 100-5-12

        // Outer rect should only be affected by margin
        AssertRectValues(outerRect, 25f, 40f, 140f, 130f); // 50-25, 50-10, 100+15+25, 100+10+20
    }

    /// <summary>
    /// Verifies that negative margin and padding values produce valid rects.
    /// </summary>
    [Fact]
    public void NegativeMarginAndPadding_ProduceValidRects()
    {
        // Arrange
        var node = CreateNodeWithBuildStage();
        node.Left(100f).Top(100f).Width(200f).Height(150f);
        SetPrivateRect(node, new Rect(100f, 100f, 200f, 150f));

        // Act
        node.Margin(-5f).Padding(-3f);

        // Assert
        var innerRect = node.InnerRect;
        var outerRect = node.OuterRect;

        // Negative padding makes inner rect larger
        AssertRectValues(innerRect, 97f, 97f, 206f, 156f); // 100-3, 100-3, 200+6, 150+6

        // Negative margin makes outer rect smaller
        AssertRectValues(outerRect, 105f, 105f, 190f, 140f); // 100+5, 100+5, 200-10, 150-10
    }

    #endregion

    #region Helper Methods

    private void SetPrivateRect(LayoutNode node, Rect rect)
    {
        var field = node.GetType().GetField("_rect",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null)
            throw new ArgumentException("Field '_rect' not found on LayoutNode");

        field.SetValue(node, rect);
    }

    #endregion
}
