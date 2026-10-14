namespace Guinevere.Tests;

/// <summary>
/// Tests for layout node command extension methods.
/// </summary>
public class LayoutNodeCommandsTests : LayoutNodeTestBase
{
    #region Expand Tests

    /// <summary>
    /// Verifies that Expand sets expansion properties with the given width and height percentages during build stage.
    /// </summary>
    [Theory]
    [InlineData(1.0f, 1.0f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(0.8f, 0.3f)]
    [InlineData(0.0f, 1.0f)]
    [InlineData(1.0f, 0.0f)]
    public void ExpandWithBuildStageSetsExpansionProperties(float widthPercentage, float heightPercentage)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.Expand(widthPercentage, heightPercentage);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.True(GetStyleProperty<bool>(node, "IsExpanded"));
        Assert.Equal(widthPercentage, GetStyleProperty<float>(node, "ExpandWidthPercentage"));
        Assert.Equal(heightPercentage, GetStyleProperty<float>(node, "ExpandHeightPercentage"));
    }

    /// <summary>
    /// Verifies that Expand with default parameters sets full expansion (1.0, 1.0).
    /// </summary>
    [Fact]
    public void ExpandWithDefaultParametersSetsFullExpansion()
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.Expand();

        // Assert
        VerifyFluentReturn(node, result);
        Assert.True(GetStyleProperty<bool>(node, "IsExpanded"));
        Assert.Equal(1.0f, GetStyleProperty<float>(node, "ExpandWidthPercentage"));
        Assert.Equal(1.0f, GetStyleProperty<float>(node, "ExpandHeightPercentage"));
    }

    /// <summary>
    /// Verifies that Expand does not set expansion properties during render stage.
    /// </summary>
    [Fact]
    public void ExpandWithRenderStageDoesNotSetProperties()
    {
        // Arrange
        var node = CreateNodeWithRenderStage();

        // Act
        var result = node.Expand(0.5f, 0.7f);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.False(GetStyleProperty<bool>(node, "IsExpanded"));
        Assert.Equal(1.0f, GetStyleProperty<float>(node, "ExpandWidthPercentage")); // Default value
        Assert.Equal(1.0f, GetStyleProperty<float>(node, "ExpandHeightPercentage")); // Default value
    }

    #endregion

    #region ExpandWidth Tests

    /// <summary>
    /// Verifies that ExpandWidth sets width expansion with the given percentage during build stage.
    /// </summary>
    [Theory]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(1.0f)]
    public void ExpandWidthWithBuildStageSetsWidthExpansion(float percentage)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.ExpandWidth(percentage);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.True(GetStyleProperty<bool>(node, "ExpandWidth"));
        Assert.Equal(percentage, GetStyleProperty<float>(node, "ExpandWidthPercentage"));
    }

    /// <summary>
    /// Verifies that ExpandWidth with default parameter sets full width expansion.
    /// </summary>
    [Fact]
    public void ExpandWidthWithDefaultParameterSetsFullWidthExpansion()
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.ExpandWidth();

        // Assert
        VerifyFluentReturn(node, result);
        Assert.True(GetStyleProperty<bool>(node, "ExpandWidth"));
        Assert.Equal(1.0f, GetStyleProperty<float>(node, "ExpandWidthPercentage"));
    }

    /// <summary>
    /// Verifies that ExpandWidth does not set properties during render stage.
    /// </summary>
    [Fact]
    public void ExpandWidthWithRenderStageDoesNotSetProperties()
    {
        // Arrange
        var node = CreateNodeWithRenderStage();

        // Act
        var result = node.ExpandWidth(0.5f);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.False(GetStyleProperty<bool>(node, "ExpandWidth"));
    }

    #endregion

    #region ExpandHeight Tests

    /// <summary>
    /// Verifies that ExpandHeight sets height expansion with the given percentage during build stage.
    /// </summary>
    [Theory]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(1.0f)]
    public void ExpandHeightWithBuildStageSetsHeightExpansion(float percentage)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.ExpandHeight(percentage);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.True(GetStyleProperty<bool>(node, "ExpandHeight"));
        Assert.Equal(percentage, GetStyleProperty<float>(node, "ExpandHeightPercentage"));
    }

    /// <summary>
    /// Verifies that ExpandHeight with default parameter sets full height expansion.
    /// </summary>
    [Fact]
    public void ExpandHeightWithDefaultParameterSetsFullHeightExpansion()
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.ExpandHeight();

        // Assert
        VerifyFluentReturn(node, result);
        Assert.True(GetStyleProperty<bool>(node, "ExpandHeight"));
        Assert.Equal(1.0f, GetStyleProperty<float>(node, "ExpandHeightPercentage"));
    }

    /// <summary>
    /// Verifies that ExpandHeight does not set properties during render stage.
    /// </summary>
    [Fact]
    public void ExpandHeightWithRenderStageDoesNotSetProperties()
    {
        // Arrange
        var node = CreateNodeWithRenderStage();

        // Act
        var result = node.ExpandHeight(0.5f);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.False(GetStyleProperty<bool>(node, "ExpandHeight"));
    }

    #endregion

    #region Gap Tests

    /// <summary>
    /// Verifies that Gap sets the gap value during build stage.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(5f)]
    [InlineData(10f)]
    [InlineData(20f)]
    [InlineData(50f)]
    public void GapWithBuildStageSetsGapValue(float gap)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.Gap(gap);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.Equal(gap, GetStyleProperty<float>(node, "Gap"));
    }

    /// <summary>
    /// Verifies that Gap does not set the property during render stage.
    /// </summary>
    [Fact]
    public void GapWithRenderStageDoesNotSetProperty()
    {
        // Arrange
        var node = CreateNodeWithRenderStage();

        // Act
        var result = node.Gap(15f);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.Equal(0f, GetStyleProperty<float>(node, "Gap")); // Default value
    }

    #endregion

    #region Margin Tests

    /// <summary>
    /// Verifies that Margin with a single value sets all margins equally during build stage.
    /// </summary>
    [Theory]
    [InlineData(5f)]
    [InlineData(10f)]
    [InlineData(15f)]
    [InlineData(0f)]
    public void MarginWithSingleValueSetsAllMargins(float margin)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.Margin(margin);

        // Assert
        VerifyFluentReturn(node, result);
        AssertMarginValues(node, margin, margin, margin, margin);
        Assert.False(GetStyleProperty<bool>(node, "HasSpecificMargins"));
    }

    /// <summary>
    /// Verifies that Margin with horizontal and vertical values sets specific margins during build stage.
    /// </summary>
    [Theory]
    [InlineData(10f, 5f)]
    [InlineData(0f, 15f)]
    [InlineData(20f, 0f)]
    [InlineData(8f, 12f)]
    public void MarginWithHorizontalVerticalSetsSpecificMargins(float horizontal, float vertical)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.Margin(horizontal, vertical);

        // Assert
        VerifyFluentReturn(node, result);
        AssertMarginValues(node, vertical, horizontal, vertical, horizontal);
        Assert.True(GetStyleProperty<bool>(node, "HasSpecificMargins"));
    }

    /// <summary>
    /// Verifies that Margin with four values sets individual margins during build stage.
    /// </summary>
    [Theory]
    [InlineData(1f, 2f, 3f, 4f)]
    [InlineData(0f, 5f, 10f, 15f)]
    [InlineData(20f, 0f, 5f, 10f)]
    public void MarginWithFourValuesSetsIndividualMargins(float top, float right, float bottom, float left)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.Margin(top, right, bottom, left);

        // Assert
        VerifyFluentReturn(node, result);
        AssertMarginValues(node, top, right, bottom, left);
        Assert.True(GetStyleProperty<bool>(node, "HasSpecificMargins"));
    }

    /// <summary>
    /// Verifies that Margin does not set properties during render stage.
    /// </summary>
    [Fact]
    public void MarginWithRenderStageDoesNotSetProperties()
    {
        // Arrange
        var node = CreateNodeWithRenderStage();

        // Act
        var result = node.Margin(10f);

        // Assert
        VerifyFluentReturn(node, result);
        AssertMarginValues(node, 0f, 0f, 0f, 0f); // Default values
    }

    #endregion

    #region Padding Tests

    /// <summary>
    /// Verifies that Padding with a single value sets all paddings equally during build stage.
    /// </summary>
    [Theory]
    [InlineData(5f)]
    [InlineData(10f)]
    [InlineData(15f)]
    [InlineData(0f)]
    public void PaddingWithSingleValueSetsAllPaddings(float padding)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.Padding(padding);

        // Assert
        VerifyFluentReturn(node, result);
        AssertPaddingValues(node, padding, padding, padding, padding);
    }

    /// <summary>
    /// Verifies that Padding with horizontal and vertical values sets specific paddings during build stage.
    /// </summary>
    [Theory]
    [InlineData(10f, 5f)]
    [InlineData(0f, 15f)]
    [InlineData(20f, 0f)]
    [InlineData(8f, 12f)]
    public void PaddingWithHorizontalVerticalSetsSpecificPaddings(float horizontal, float vertical)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.Padding(horizontal, vertical);

        // Assert
        VerifyFluentReturn(node, result);
        AssertPaddingValues(node, vertical, horizontal, vertical, horizontal);
    }

    /// <summary>
    /// Verifies that Padding with four values sets individual paddings during build stage.
    /// </summary>
    [Theory]
    [InlineData(1f, 2f, 3f, 4f)]
    [InlineData(0f, 5f, 10f, 15f)]
    [InlineData(20f, 0f, 5f, 10f)]
    public void PaddingWithFourValuesSetsIndividualPaddings(float top, float right, float bottom, float left)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.Padding(top, right, bottom, left);

        // Assert
        VerifyFluentReturn(node, result);
        AssertPaddingValues(node, top, right, bottom, left);
    }

    /// <summary>
    /// Verifies that Padding does not set properties during render stage.
    /// </summary>
    [Fact]
    public void PaddingWithRenderStageDoesNotSetProperties()
    {
        // Arrange
        var node = CreateNodeWithRenderStage();

        // Act
        var result = node.Padding(10f);

        // Assert
        VerifyFluentReturn(node, result);
        AssertPaddingValues(node, 0f, 0f, 0f, 0f); // Default values
    }

    #endregion

    #region Alignment Tests

    /// <summary>
    /// Verifies that AlignContent sets the alignment during build stage.
    /// </summary>
    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(1.0f)]
    public void AlignContentWithBuildStageSetsAlignment(float alignment)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.AlignContent(alignment);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.Equal(alignment, GetStyleProperty<float>(node, "AlignContentHorizontal"));
        Assert.Equal(alignment, GetStyleProperty<float>(node, "AlignContentVertical"));
    }

    /// <summary>
    /// Verifies that AlignSelf sets the alignment during build stage.
    /// </summary>
    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(1.0f)]
    public void AlignSelfWithBuildStageSetsAlignment(float alignment)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.AlignSelf(alignment);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.Equal(alignment, GetStyleProperty<float>(node, "AlignSelf"));
    }

    /// <summary>
    /// Verifies that AlignContent with two parameters sets separate horizontal and vertical values during build stage.
    /// </summary>
    [Theory]
    [InlineData(0.0f, 0.5f)]
    [InlineData(0.25f, 0.75f)]
    [InlineData(1.0f, 0.0f)]
    public void AlignContentWithTwoParametersSetsSeparateValues(float horizontal, float vertical)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.AlignContent(horizontal, vertical);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.Equal(horizontal, GetStyleProperty<float>(node, "AlignContentHorizontal"));
        Assert.Equal(vertical, GetStyleProperty<float>(node, "AlignContentVertical"));
    }

    /// <summary>
    /// Verifies that AlignContent does not set the property during render stage.
    /// </summary>
    [Fact]
    public void AlignContentWithRenderStageDoesNotSetProperty()
    {
        // Arrange
        var node = CreateNodeWithRenderStage();

        // Act
        var result = node.AlignContent(0.8f);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.Equal(0.0f, GetStyleProperty<float>(node, "AlignContentHorizontal")); // Default value
        Assert.Equal(0.0f, GetStyleProperty<float>(node, "AlignContentVertical")); // Default value
    }

    /// <summary>
    /// Verifies that AlignContent with two parameters does not set the properties during render stage.
    /// </summary>
    [Fact]
    public void AlignContentTwoParametersWithRenderStageDoesNotSetProperty()
    {
        // Arrange
        var node = CreateNodeWithRenderStage();

        // Act
        var result = node.AlignContent(0.8f, 0.3f);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.Equal(0.0f, GetStyleProperty<float>(node, "AlignContentHorizontal")); // Default value
        Assert.Equal(0.0f, GetStyleProperty<float>(node, "AlignContentVertical")); // Default value
    }

    /// <summary>
    /// Verifies that AlignSelf does not set the property during render stage.
    /// </summary>
    [Fact]
    public void AlignSelfWithRenderStageDoesNotSetProperty()
    {
        // Arrange
        var node = CreateNodeWithRenderStage();

        // Act
        var result = node.AlignSelf(0.8f);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.Equal(0.5f, GetStyleProperty<float>(node, "AlignSelf")); // Default value
    }

    #endregion

    #region Direction Tests

    /// <summary>
    /// Verifies that Direction sets the given direction during build stage.
    /// </summary>
    [Theory]
    [InlineData(Axis.Horizontal)]
    [InlineData(Axis.Vertical)]
    [InlineData(Axis.None)]
    public void DirectionWithBuildStageSetsDirection(Axis direction)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.Direction(direction);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.Equal(direction, GetStyleProperty<Axis>(node, "Direction"));
    }

    /// <summary>
    /// Verifies that Direction does not set the property during render stage.
    /// </summary>
    [Fact]
    public void DirectionWithRenderStageDoesNotSetProperty()
    {
        // Arrange
        var node = CreateNodeWithRenderStage();

        // Act
        var result = node.Direction(Axis.Horizontal);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.Equal(Axis.Vertical, GetStyleProperty<Axis>(node, "Direction")); // Default value
    }

    #endregion

    #region Wrap Tests

    /// <summary>
    /// Verifies that Wrap sets the wrap content flag during build stage.
    /// </summary>
    [Fact]
    public void WrapContentWithBuildStageSetsWrapContent()
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.Wrap(1);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.True(GetStyleProperty<bool>(node, "Wrap"));
    }

    /// <summary>
    /// Verifies that Wrap does not set the property during render stage.
    /// </summary>
    [Fact]
    public void WrapContentWithRenderStageDoesNotSetProperty()
    {
        // Arrange
        var node = CreateNodeWithRenderStage();

        // Act
        var result = node.Wrap(1);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.False(GetStyleProperty<bool>(node, "Wrap")); // Default value
    }

    #endregion

    #region Size Tests

    /// <summary>
    /// Verifies that Width sets the width during build stage.
    /// </summary>
    [Theory]
    [InlineData(50f)]
    [InlineData(100f)]
    [InlineData(200f)]
    [InlineData(0f)]
    public void WidthWithBuildStageSetsWidth(float width)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.Width(width);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.Equal(width, GetStyleProperty<float?>(node, "Width"));
    }

    /// <summary>
    /// Verifies that Height sets the height during build stage.
    /// </summary>
    [Theory]
    [InlineData(50f)]
    [InlineData(100f)]
    [InlineData(200f)]
    [InlineData(0f)]
    public void HeightWithBuildStageSetsHeight(float height)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.Height(height);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.Equal(height, GetStyleProperty<float?>(node, "Height"));
    }

    /// <summary>
    /// Verifies that Size sets both width and height during build stage.
    /// </summary>
    [Theory]
    [InlineData(50f)]
    [InlineData(100f)]
    [InlineData(200f)]
    public void SizeWithBuildStageSetsBothWidthAndHeight(float size)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.Size(size);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.Equal(size, GetStyleProperty<float?>(node, "Width"));
        Assert.Equal(size, GetStyleProperty<float?>(node, "Height"));
    }

    /// <summary>
    /// Verifies that Width does not set the property during render stage.
    /// </summary>
    [Fact]
    public void WidthWithRenderStageDoesNotSetProperty()
    {
        // Arrange
        var node = CreateNodeWithRenderStage();

        // Act
        var result = node.Width(100f);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.Null(GetStyleProperty<float?>(node, "Width"));
    }

    /// <summary>
    /// Verifies that Height does not set the property during render stage.
    /// </summary>
    [Fact]
    public void HeightWithRenderStageDoesNotSetProperty()
    {
        // Arrange
        var node = CreateNodeWithRenderStage();

        // Act
        var result = node.Height(100f);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.Null(GetStyleProperty<float?>(node, "Height"));
    }

    #endregion

    #region Position Tests

    /// <summary>
    /// Verifies that Left sets the X position during build stage.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(50f)]
    [InlineData(100f)]
    [InlineData(-10f)]
    public void LeftWithBuildStageSetsXPosition(float value)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.Left(value);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.Equal(value, node.Rect.X);
    }

    /// <summary>
    /// Verifies that Top sets the Y position during build stage.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(50f)]
    [InlineData(100f)]
    [InlineData(-10f)]
    public void TopWithBuildStageSetsYPosition(float value)
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node.Top(value);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.Equal(value, node.Rect.Y);
    }

    /// <summary>
    /// Verifies that Left does not set the X position during render stage.
    /// </summary>
    [Fact]
    public void LeftWithRenderStageDoesNotSetProperty()
    {
        // Arrange
        var node = CreateNodeWithRenderStage();
        var originalX = node.Rect.X;

        // Act
        var result = node.Left(100f);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.Equal(originalX, node.Rect.X);
    }

    /// <summary>
    /// Verifies that Top does not set the Y position during render stage.
    /// </summary>
    [Fact]
    public void TopWithRenderStageDoesNotSetProperty()
    {
        // Arrange
        var node = CreateNodeWithRenderStage();
        var originalY = node.Rect.Y;

        // Act
        var result = node.Top(100f);

        // Assert
        VerifyFluentReturn(node, result);
        Assert.Equal(originalY, node.Rect.Y);
    }

    #endregion

    #region Fluent Interface Chain Tests

    /// <summary>
    /// Verifies that a fluent chain of multiple commands applies correctly during build stage.
    /// </summary>
    [Fact]
    public void FluentChainWithMultipleCommandsWorksCorrectly()
    {
        // Arrange
        var node = CreateNodeWithBuildStage();

        // Act
        var result = node
            .Expand(0.8f, 0.6f)
            .Margin(10f)
            .Padding(5f, 8f)
            .Direction(Axis.Horizontal)
            .Gap(15f)
            .Width(200f)
            .Height(100f)
            .AlignContent(0.25f)
            .AlignSelf(0.75f)
            .Wrap(1)
            .Left(50f)
            .Top(30f);

        // Assert
        VerifyFluentReturn(node, result);

        // Verify all properties were set correctly
        Assert.True(GetStyleProperty<bool>(node, "IsExpanded"));
        Assert.Equal(0.8f, GetStyleProperty<float>(node, "ExpandWidthPercentage"));
        Assert.Equal(0.6f, GetStyleProperty<float>(node, "ExpandHeightPercentage"));
        AssertMarginValues(node, 10f, 10f, 10f, 10f);
        AssertPaddingValues(node, 8f, 5f, 8f, 5f);
        Assert.Equal(Axis.Horizontal, GetStyleProperty<Axis>(node, "Direction"));
        Assert.Equal(15f, GetStyleProperty<float>(node, "Gap"));
        Assert.Equal(200f, GetStyleProperty<float?>(node, "Width"));
        Assert.Equal(100f, GetStyleProperty<float?>(node, "Height"));
        Assert.Equal(0.25f, GetStyleProperty<float>(node, "AlignContentHorizontal"));
        Assert.Equal(0.25f, GetStyleProperty<float>(node, "AlignContentVertical"));
        Assert.Equal(0.75f, GetStyleProperty<float>(node, "AlignSelf"));
        Assert.True(GetStyleProperty<bool>(node, "Wrap"));
        Assert.Equal(50f, node.Rect.X);
        Assert.Equal(30f, node.Rect.Y);
    }

    /// <summary>
    /// Verifies that a fluent chain of commands does not set any properties during render stage.
    /// </summary>
    [Fact]
    public void FluentChainWithRenderStageDoesNotSetAnyProperties()
    {
        // Arrange
        var node = CreateNodeWithRenderStage();

        // Act
        var result = node
            .Expand(0.8f, 0.6f)
            .Margin(10f)
            .Padding(5f)
            .Direction(Axis.Horizontal)
            .Gap(15f)
            .Width(200f)
            .Height(100f);

        // Assert
        VerifyFluentReturn(node, result);

        // Verify no properties were changed (all should have default values)
        Assert.False(GetStyleProperty<bool>(node, "IsExpanded"));
        Assert.Equal(1.0f, GetStyleProperty<float>(node, "ExpandWidthPercentage"));
        Assert.Equal(1.0f, GetStyleProperty<float>(node, "ExpandHeightPercentage"));
        AssertMarginValues(node, 0f, 0f, 0f, 0f);
        AssertPaddingValues(node, 0f, 0f, 0f, 0f);
        Assert.Equal(Axis.Vertical, GetStyleProperty<Axis>(node, "Direction"));
        Assert.Equal(0f, GetStyleProperty<float>(node, "Gap"));
        Assert.Null(GetStyleProperty<float?>(node, "Width"));
        Assert.Null(GetStyleProperty<float?>(node, "Height"));
    }

    #endregion
}
