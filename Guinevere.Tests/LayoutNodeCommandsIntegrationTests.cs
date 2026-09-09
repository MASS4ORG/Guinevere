namespace Guinevere.Tests;

/// <summary>
/// Verifies that layout node command chains work correctly when combined.
/// </summary>
public class LayoutNodeCommandsIntegrationTests : LayoutNodeTestBase
{
    #region Margin and Padding Integration Tests

    /// <summary>
    /// Verifies that margin and padding commands combined with size and position produce the correct inner and outer rects.
    /// </summary>
    [Theory]
    [InlineData(10f, 5f, 15f, 8f)]
    [InlineData(0f, 0f, 0f, 0f)]
    [InlineData(20f, 10f, 5f, 15f)]
    public void MarginAndPadding_Together_ProduceCorrectRects(float margin, float padding, float width, float height)
    {
        // Arrange
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass1Build);
        var child = CreateTestLayoutNode(gui);

        // Act
        child.Margin(margin).Padding(padding).Width(width).Height(height).Left(50f).Top(60f);
        SetPrivateRect(child, new Rect(50f, 60f, width > 0 ? width : 100f, height > 0 ? height : 80f));

        var rect = child.Rect;
        var expectedInnerX = rect.X + padding;
        var expectedInnerY = rect.Y + padding;
        var expectedInnerW = rect.W - (2 * padding);
        var expectedInnerH = rect.H - (2 * padding);

        var innerRect = child.InnerRect;
        AssertRectValues(innerRect, expectedInnerX, expectedInnerY, expectedInnerW, expectedInnerH);

        var expectedOuterX = rect.X - margin;
        var expectedOuterY = rect.Y - margin;
        var expectedOuterW = rect.W + (2 * margin);
        var expectedOuterH = rect.H + (2 * margin);

        var outerRect = child.OuterRect;
        AssertRectValues(outerRect, expectedOuterX, expectedOuterY, expectedOuterW, expectedOuterH);
    }

    /// <summary>
    /// Verifies that specific asymmetric margin and padding values produce the correct inner and outer rects.
    /// </summary>
    [Fact]
    public void SpecificMarginAndPadding_ProduceAsymmetricRects()
    {
        // Arrange
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass1Build);
        var node = CreateTestLayoutNode(gui);

        // Act - Asymmetric margins and padding
        node.Left(50f).Top(50f).Width(100f).Height(100f)
            .Margin(5f, 10f, 15f, 20f) // top, right, bottom, left
            .Padding(2f, 4f, 6f, 8f);   // top, right, bottom, left

        SetPrivateRect(node, new Rect(50f, 50f, 100f, 100f));

        // Assert
        var innerRect = node.InnerRect;
        AssertRectValues(innerRect, 58f, 52f, 88f, 92f); // 50+8, 50+2, 100-4-8, 100-2-6

        var outerRect = node.OuterRect;
        AssertRectValues(outerRect, 30f, 45f, 130f, 120f); // 50-20, 50-5, 100+20+10, 100+5+15
    }

    #endregion

    #region Size and Expansion Integration Tests

    /// <summary>
    /// Verifies that explicit width and height take precedence over expansion settings.
    /// </summary>
    [Theory]
    [InlineData(0.5f, 0.7f, 100f, 200f)]
    [InlineData(1.0f, 1.0f, 150f, 300f)]
    [InlineData(0.3f, 0.4f, 80f, 120f)]
    public void ExpandWithExplicitSize_ExplicitSizeTakesPrecedence(float expandW, float expandH, float explicitW, float explicitH)
    {
        // Arrange
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass1Build);
        var node = CreateTestLayoutNode(gui);

        // Act - Set expansion first, then explicit size
        node.Expand(expandW, expandH).Width(explicitW).Height(explicitH);

        // Assert
        Assert.Equal(explicitW, GetStyleProperty<float?>(node, "Width"));
        Assert.Equal(explicitH, GetStyleProperty<float?>(node, "Height"));
        Assert.True(GetStyleProperty<bool>(node, "IsExpanded"));
    }

    /// <summary>
    /// Verifies that the size command sets both width and height dimensions.
    /// </summary>
    [Fact]
    public void SizeCommand_SetsBothDimensions()
    {
        // Arrange
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass1Build);
        var node = CreateTestLayoutNode(gui);

        // Act
        node.Size(75f);

        // Assert
        Assert.Equal(75f, GetStyleProperty<float?>(node, "Width"));
        Assert.Equal(75f, GetStyleProperty<float?>(node, "Height"));
    }

    /// <summary>
    /// Verifies that expanding the width does not affect the height.
    /// </summary>
    [Fact]
    public void ExpandWidth_DoesNotAffectHeight()
    {
        // Arrange
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass1Build);
        var node = CreateTestLayoutNode(gui);

        // Act
        node.ExpandWidth(0.8f).Height(50f);

        // Assert
        Assert.True(GetStyleProperty<bool>(node, "ExpandWidth"));
        Assert.False(GetStyleProperty<bool>(node, "ExpandHeight"));
        Assert.Equal(0.8f, GetStyleProperty<float>(node, "ExpandWidthPercentage"));
        Assert.Equal(50f, GetStyleProperty<float?>(node, "Height"));
    }

    /// <summary>
    /// Verifies that expanding the height does not affect the width.
    /// </summary>
    [Fact]
    public void ExpandHeight_DoesNotAffectWidth()
    {
        // Arrange
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass1Build);
        var node = CreateTestLayoutNode(gui);

        // Act
        node.ExpandHeight(0.6f).Width(80f);

        // Assert
        Assert.False(GetStyleProperty<bool>(node, "ExpandWidth"));
        Assert.True(GetStyleProperty<bool>(node, "ExpandHeight"));
        Assert.Equal(0.6f, GetStyleProperty<float>(node, "ExpandHeightPercentage"));
        Assert.Equal(80f, GetStyleProperty<float?>(node, "Width"));
    }

    #endregion

    #region Layout Direction and Alignment Integration Tests

    /// <summary>
    /// Verifies that direction combined with alignment produces the correct combination of properties.
    /// </summary>
    [Theory]
    [InlineData(Axis.Horizontal, 0.0f, 0.5f)]
    [InlineData(Axis.Horizontal, 0.5f, 1.0f)]
    [InlineData(Axis.Vertical, 0.0f, 0.5f)]
    [InlineData(Axis.Vertical, 1.0f, 0.0f)]
    public void DirectionWithAlignment_SetsCorrectCombination(Axis direction, float alignContent, float alignSelf)
    {
        // Arrange
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass1Build);
        var node = CreateTestLayoutNode(gui);

        // Act
        node.Direction(direction).AlignContent(alignContent).AlignSelf(alignSelf);

        // Assert
        Assert.Equal(direction, GetStyleProperty<Axis>(node, "Direction"));
        Assert.Equal(alignContent, GetStyleProperty<float>(node, "AlignContentHorizontal"));
        Assert.Equal(alignContent, GetStyleProperty<float>(node, "AlignContentVertical"));
        Assert.Equal(alignSelf, GetStyleProperty<float>(node, "AlignSelf"));
    }

    /// <summary>
    /// Verifies that direction combined with gap works correctly.
    /// </summary>
    [Theory]
    [InlineData(Axis.Horizontal, 10f)]
    [InlineData(Axis.Vertical, 15f)]
    [InlineData(Axis.None, 0f)]
    public void DirectionWithGap_WorksCorrectly(Axis direction, float gap)
    {
        // Arrange
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass1Build);
        var node = CreateTestLayoutNode(gui);

        // Act
        node.Direction(direction).Gap(gap);

        // Assert
        Assert.Equal(direction, GetStyleProperty<Axis>(node, "Direction"));
        Assert.Equal(gap, GetStyleProperty<float>(node, "Gap"));
    }

    #endregion

    #region Position Integration Tests

    /// <summary>
    /// Verifies that position combined with margin affects the outer rect.
    /// </summary>
    [Theory]
    [InlineData(25f, 35f, 10f)]
    [InlineData(0f, 0f, 5f)]
    [InlineData(-10f, -5f, 0f)]
    public void PositionWithMargin_AffectsOuterRect(float left, float top, float margin)
    {
        // Arrange
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass1Build);
        var node = CreateTestLayoutNode(gui);

        // Act
        node.Width(100f).Height(80f).Left(left).Top(top).Margin(margin);
        SetPrivateRect(node, new Rect(left, top, 100f, 80f));

        // Assert
        Assert.Equal(left, node.Rect.X);
        Assert.Equal(top, node.Rect.Y);

        var outerRect = node.OuterRect;
        Assert.Equal(left - margin, outerRect.X);
        Assert.Equal(top - margin, outerRect.Y);
        Assert.Equal(100f + (2 * margin), outerRect.W);
        Assert.Equal(80f + (2 * margin), outerRect.H);
    }

    /// <summary>
    /// Verifies that position combined with padding affects the inner rect.
    /// </summary>
    [Theory]
    [InlineData(50f, 60f, 8f)]
    [InlineData(0f, 0f, 12f)]
    [InlineData(100f, 200f, 0f)]
    public void PositionWithPadding_AffectsInnerRect(float left, float top, float padding)
    {
        // Arrange
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass1Build);
        var node = CreateTestLayoutNode(gui);

        // Act
        node.Width(120f).Height(100f).Left(left).Top(top).Padding(padding);
        SetPrivateRect(node, new Rect(left, top, 120f, 100f));

        // Assert
        Assert.Equal(left, node.Rect.X);
        Assert.Equal(top, node.Rect.Y);

        var innerRect = node.InnerRect;
        Assert.Equal(left + padding, innerRect.X);
        Assert.Equal(top + padding, innerRect.Y);
        Assert.Equal(120f - (2 * padding), innerRect.W);
        Assert.Equal(100f - (2 * padding), innerRect.H);
    }

    #endregion

    #region Complex Command Chains

    /// <summary>
    /// Verifies that a complex layout command chain applies all commands correctly.
    /// </summary>
    [Fact]
    public void ComplexLayoutChain_AllCommandsWork()
    {
        // Arrange
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass1Build);
        var node = CreateTestLayoutNode(gui);

        // Act - Complex realistic layout setup
        node.Direction(Axis.Horizontal)
            .Expand(0.8f, 0.6f)
            .Margin(5f, 10f, 15f, 20f)
            .Padding(2f, 4f, 6f, 8f)
            .Gap(12f)
            .AlignContent(0.25f)
            .AlignSelf(0.75f)
            .Wrap(1)
            .Left(30f)
            .Top(40f)
            .Width(200f)
            .Height(150f);

        // Assert all properties
        Assert.Equal(Axis.Horizontal, GetStyleProperty<Axis>(node, "Direction"));
        Assert.True(GetStyleProperty<bool>(node, "IsExpanded"));
        Assert.Equal(0.8f, GetStyleProperty<float>(node, "ExpandWidthPercentage"));
        Assert.Equal(0.6f, GetStyleProperty<float>(node, "ExpandHeightPercentage"));

        AssertMarginValues(node, 5f, 10f, 15f, 20f);
        AssertPaddingValues(node, 2f, 4f, 6f, 8f);

        Assert.Equal(12f, GetStyleProperty<float>(node, "Gap"));
        Assert.Equal(0.25f, GetStyleProperty<float>(node, "AlignContentHorizontal"));
        Assert.Equal(0.25f, GetStyleProperty<float>(node, "AlignContentVertical"));
        Assert.Equal(0.75f, GetStyleProperty<float>(node, "AlignSelf"));
        Assert.True(GetStyleProperty<bool>(node, "Wrap"));

        Assert.Equal(30f, node.Rect.X);
        Assert.Equal(40f, node.Rect.Y);
        Assert.Equal(200f, GetStyleProperty<float?>(node, "Width"));
        Assert.Equal(150f, GetStyleProperty<float?>(node, "Height"));
    }

    /// <summary>
    /// Verifies that when commands are overridden, the last call wins.
    /// </summary>
    [Fact]
    public void OverridingCommands_LastCallWins()
    {
        // Arrange
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass1Build);
        var node = CreateTestLayoutNode(gui);

        // Act - Multiple calls to the same command type
        node.Width(100f)
            .Height(80f)
            .Margin(5f)
            .Padding(3f)
            .Direction(Axis.Horizontal)
            .Gap(10f)
            .Width(150f)      // Override width
            .Height(120f)     // Override height
            .Margin(8f)       // Override margin
            .Padding(6f)      // Override padding
            .Direction(Axis.Vertical) // Override direction
            .Gap(15f);        // Override gap

        // Assert - Last values should win
        Assert.Equal(150f, GetStyleProperty<float?>(node, "Width"));
        Assert.Equal(120f, GetStyleProperty<float?>(node, "Height"));
        AssertMarginValues(node, 8f, 8f, 8f, 8f);
        AssertPaddingValues(node, 6f, 6f, 6f, 6f);
        Assert.Equal(Axis.Vertical, GetStyleProperty<Axis>(node, "Direction"));
        Assert.Equal(15f, GetStyleProperty<float>(node, "Gap"));
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Verifies that negative values are accepted and stored as-is.
    /// </summary>
    [Fact]
    public void NegativeValues_AreAccepted()
    {
        // Arrange
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass1Build);
        var node = CreateTestLayoutNode(gui);

        // Act
        node.Width(-10f)
            .Height(-5f)
            .Left(-20f)
            .Top(-15f)
            .Margin(-2f)
            .Padding(-1f)
            .Gap(-5f);

        // Assert - Negative values should be stored as-is
        Assert.Equal(-10f, GetStyleProperty<float?>(node, "Width"));
        Assert.Equal(-5f, GetStyleProperty<float?>(node, "Height"));
        Assert.Equal(-20f, node.Rect.X);
        Assert.Equal(-15f, node.Rect.Y);
        AssertMarginValues(node, -2f, -2f, -2f, -2f);
        AssertPaddingValues(node, -1f, -1f, -1f, -1f);
        Assert.Equal(-5f, GetStyleProperty<float>(node, "Gap"));
    }

    /// <summary>
    /// Verifies that zero values are accepted and stored as-is.
    /// </summary>
    [Fact]
    public void ZeroValues_AreAccepted()
    {
        // Arrange
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass1Build);
        var node = CreateTestLayoutNode(gui);

        // Act
        node.Width(0f)
            .Height(0f)
            .Left(0f)
            .Top(0f)
            .Margin(0f)
            .Padding(0f)
            .Gap(0f)
            .AlignContent(0f)
            .AlignSelf(0f)
            .Expand(0f, 0f)
            .ExpandWidth(0f)
            .ExpandHeight(0f);

        // Assert
        Assert.Equal(0f, GetStyleProperty<float?>(node, "Width"));
        Assert.Equal(0f, GetStyleProperty<float?>(node, "Height"));
        Assert.Equal(0f, node.Rect.X);
        Assert.Equal(0f, node.Rect.Y);
        AssertMarginValues(node, 0f, 0f, 0f, 0f);
        AssertPaddingValues(node, 0f, 0f, 0f, 0f);
        Assert.Equal(0f, GetStyleProperty<float>(node, "Gap"));
        Assert.Equal(0f, GetStyleProperty<float>(node, "AlignContentHorizontal"));
        Assert.Equal(0f, GetStyleProperty<float>(node, "AlignContentVertical"));
        Assert.Equal(0f, GetStyleProperty<float>(node, "AlignSelf"));
        Assert.Equal(0f, GetStyleProperty<float>(node, "ExpandWidthPercentage"));
        Assert.Equal(0f, GetStyleProperty<float>(node, "ExpandHeightPercentage"));
    }

    /// <summary>
    /// Verifies that extreme float values are accepted and stored as-is.
    /// </summary>
    [Fact]
    public void ExtremeValues_AreAccepted()
    {
        // Arrange
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass1Build);
        var node = CreateTestLayoutNode(gui);

        // Act
        node.Width(float.MaxValue)
            .Height(float.MaxValue)
            .Left(float.MinValue)
            .Top(float.MinValue)
            .Margin(float.MaxValue)
            .Padding(float.MaxValue)
            .Gap(float.MaxValue)
            .AlignContent(float.MaxValue)
            .AlignSelf(float.MaxValue)
            .Expand(float.MaxValue, float.MaxValue);

        // Assert
        Assert.Equal(float.MaxValue, GetStyleProperty<float?>(node, "Width"));
        Assert.Equal(float.MaxValue, GetStyleProperty<float?>(node, "Height"));
        Assert.Equal(float.MinValue, node.Rect.X);
        Assert.Equal(float.MinValue, node.Rect.Y);
        AssertMarginValues(node, float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue);
        AssertPaddingValues(node, float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue);
        Assert.Equal(float.MaxValue, GetStyleProperty<float>(node, "Gap"));
        Assert.Equal(float.MaxValue, GetStyleProperty<float>(node, "AlignContentHorizontal"));
        Assert.Equal(float.MaxValue, GetStyleProperty<float>(node, "AlignContentVertical"));
        Assert.Equal(float.MaxValue, GetStyleProperty<float>(node, "AlignSelf"));
    }

    #endregion

    #region Pass Transition Tests

    /// <summary>
    /// Verifies that transitioning from the build stage to the render stage stops accepting commands.
    /// </summary>
    [Fact]
    public void StageTransition_BuildToRender_StopsAcceptingCommands()
    {
        // Arrange
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass1Build);
        var node = CreateTestLayoutNode(gui);

        // Act & Assert - Pass1Build pass
        node.Width(100f).Height(80f);
        Assert.Equal(100f, GetStyleProperty<float?>(node, "Width"));
        Assert.Equal(80f, GetStyleProperty<float?>(node, "Height"));

        // Switch to render pass
        SetGuiStage(gui, Pass.Pass2Render);

        // Try to modify - should be ignored
        node.Width(200f).Height(160f);
        Assert.Equal(100f, GetStyleProperty<float?>(node, "Width"));
        Assert.Equal(80f, GetStyleProperty<float?>(node, "Height"));
    }

    /// <summary>
    /// Verifies that transitioning from the render stage to the build stage starts accepting commands again.
    /// </summary>
    [Fact]
    public void StageTransition_RenderToBuild_StartsAcceptingCommands()
    {
        // Arrange
        var gui = CreateTestGui();
        SetGuiStage(gui, Pass.Pass2Render);
        var node = CreateTestLayoutNode(gui);

        // Act & Assert - Pass2Render pass (commands ignored)
        node.Width(100f).Height(80f);
        Assert.Null(GetStyleProperty<float?>(node, "Width"));
        Assert.Null(GetStyleProperty<float?>(node, "Height"));

        // Switch to build pass
        SetGuiStage(gui, Pass.Pass1Build);

        // Try to modify - should work
        node.Width(200f).Height(160f);
        Assert.Equal(200f, GetStyleProperty<float?>(node, "Width"));
        Assert.Equal(160f, GetStyleProperty<float?>(node, "Height"));
    }

    #endregion

    #region Helper Methods

    private void SetPrivateRect(LayoutNode node, Rect rect)
    {
        var field = node.GetType().GetField("_rect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null)
            throw new ArgumentException("Field '_rect' not found on LayoutNode");

        field.SetValue(node, rect);
    }

    #endregion
}
