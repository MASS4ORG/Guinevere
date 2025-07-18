namespace Guinevere;

public partial class LayoutNode
{
    /// <summary>
    /// Configures the layout node to expand its dimensions proportionally based on the specified width and height percentages.
    /// </summary>
    /// <param name="widthPercentage">The proportion of width to expand, where 1.0 represents full width expansion. Defaults to 1.0.</param>
    /// <param name="heightPercentage">The proportion of height to expand, where 1.0 represents full height expansion. Defaults to 1.0.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode Expand(float widthPercentage = 1.0f, float heightPercentage = 1.0f)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        Style.IsExpanded = true;
        Style.ExpandWidthPercentage = widthPercentage;
        Style.ExpandHeightPercentage = heightPercentage;
        return this;
    }

    /// <summary>
    /// Configures the layout node to expand its width proportionally based on the specified percentage.
    /// </summary>
    /// <param name="percentage">The proportion of width to expand, where 1.0 represents full width expansion. Defaults to 1.0.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode ExpandWidth(float percentage = 1.0f)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        Style.ExpandWidth = true;
        Style.ExpandWidthPercentage = percentage;
        return this;
    }

    /// <summary>
    /// Configures the layout node to expand its height proportionally based on the specified percentage.
    /// </summary>
    /// <param name="percentage">The proportion of height to expand, where 1.0 represents full height expansion. Defaults to 1.0.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode ExpandHeight(float percentage = 1.0f)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        Style.ExpandHeight = true;
        Style.ExpandHeightPercentage = percentage;
        return this;
    }

    /// <summary>
    /// Sets the spacing between child elements for the current layout node.
    /// </summary>
    /// <param name="gap">The amount of spacing to apply between child elements.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, allowing for method chaining.</returns>
    public LayoutNode Gap(float gap)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        Style.Gap = gap;
        return this;
    }

    /// <summary>
    /// Sets a uniform margin for all sides of the layout node using the specified margin value.
    /// </summary>
    /// <param name="value">The margin value to be applied to all sides of the layout node.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, allowing for method chaining.</returns>
    public LayoutNode Margin(float value)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        MarginTop(value)
            .MarginRight(value)
            .MarginBottom(value)
            .MarginLeft(value);
        Style.HasSpecificMargins = false;
        return this;
    }

    /// <summary>
    /// Sets the margin for the layout node, specifying horizontal and vertical spacing.
    /// </summary>
    /// <param name="horizontal">The horizontal margin applied to both the left and right sides of the node.</param>
    /// <param name="vertical">The vertical margin applied to both the top and bottom sides of the node.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode Margin(float horizontal, float vertical)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        MarginTop(vertical)
            .MarginRight(horizontal)
            .MarginBottom(vertical)
            .MarginLeft(horizontal);
        Style.HasSpecificMargins = true;
        return this;
    }

    /// <summary>
    /// Sets the margin values for the layout node, allowing customization of spacing around the node on all sides.
    /// </summary>
    /// <param name="top">The margin value to set for the top side.</param>
    /// <param name="right">The margin value to set for the right side.</param>
    /// <param name="bottom">The margin value to set for the bottom side.</param>
    /// <param name="left">The margin value to set for the left side.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode Margin(float top, float right, float bottom, float left)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        MarginTop(top)
            .MarginRight(right)
            .MarginBottom(bottom)
            .MarginLeft(left);
        Style.HasSpecificMargins = true;
        return this;
    }

    /// <summary>
    /// Sets the top margin of the layout node to the specified value.
    /// </summary>
    /// <param name="value">The top margin value to apply, in pixels.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode MarginTop(float value)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        Style.MarginTop = value;
        Style.HasSpecificMargins = true;
        return this;
    }

    /// <summary>
    /// Sets the right margin of the layout node.
    /// </summary>
    /// <param name="value">The size of the right margin in pixels.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode MarginRight(float value)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        Style.MarginRight = value;
        Style.HasSpecificMargins = true;
        return this;
    }

    /// <summary>
    /// Sets the bottom margin of the layout node to the specified value.
    /// </summary>
    /// <param name="value">The margin value to apply to the bottom of the layout node, measured in pixels.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode MarginBottom(float value)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        Style.MarginBottom = value;
        Style.HasSpecificMargins = true;
        return this;
    }

    /// <summary>
    /// Sets the left margin of the layout node to the specified value.
    /// </summary>
    /// <param name="value">The value to set as the left margin in pixels.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode MarginLeft(float value)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        Style.MarginLeft = value;
        Style.HasSpecificMargins = true;
        return this;
    }

    /// <summary>
    /// Sets the padding for all sides of the layout node to the specified value.
    /// </summary>
    /// <param name="value">The value to be applied as padding to the top, right, bottom, and left sides of the node.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, allowing for method chaining.</returns>
    public LayoutNode Padding(float value)
    {
        return PaddingTop(value)
            .PaddingRight(value)
            .PaddingBottom(value)
            .PaddingLeft(value);
    }

    /// <summary>
    /// Sets the padding for the layout node using the specified horizontal and vertical values.
    /// </summary>
    /// <param name="horizontal">The padding to apply to the left and right sides of the layout node.</param>
    /// <param name="vertical">The padding to apply to the top and bottom sides of the layout node.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode Padding(float horizontal, float vertical)
    {
        return PaddingTop(vertical)
            .PaddingRight(horizontal)
            .PaddingBottom(vertical)
            .PaddingLeft(horizontal);
    }

    /// <summary>
    /// Sets the padding for the layout node using individual values for each side.
    /// </summary>
    /// <param name="top">The padding value for the top side of the layout node.</param>
    /// <param name="right">The padding value for the right side of the layout node.</param>
    /// <param name="bottom">The padding value for the bottom side of the layout node.</param>
    /// <param name="left">The padding value for the left side of the layout node.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, allowing for method chaining.</returns>
    public LayoutNode Padding(float top, float right, float bottom, float left)
    {
        return PaddingTop(top)
            .PaddingRight(right)
            .PaddingBottom(bottom)
            .PaddingLeft(left);
    }

    /// <summary>
    /// Sets the vertical padding for the layout node by applying the specified value
    /// equally to both the top and bottom padding.
    /// </summary>
    /// <param name="value">The amount of padding to apply to both the top and bottom edges of the layout node.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode PaddingY(float value)
    {
        return PaddingTop(value).PaddingBottom(value);
    }

    /// <summary>
    /// Configures the horizontal padding of the layout node by applying the specified value to both the left and right sides.
    /// </summary>
    /// <param name="value">The amount of horizontal padding to apply, in logical units.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode PaddingX(float value)
    {
        return PaddingRight(value).PaddingLeft(value);
    }

    /// <summary>
    /// Sets the top padding value for the layout node.
    /// </summary>
    /// <param name="value">The value to set as the top padding.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, allowing for method chaining.</returns>
    public LayoutNode PaddingTop(float value)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        Style.PaddingTop = value;
        return this;
    }

    /// <summary>
    /// Sets the padding for the right side of the layout node.
    /// </summary>
    /// <param name="value">The padding value to apply to the right side, measured in units.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode PaddingRight(float value)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        Style.PaddingRight = value;
        return this;
    }

    /// <summary>
    /// Sets the bottom padding of the layout node to the specified value.
    /// </summary>
    /// <param name="value">The value to set for the bottom padding.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode PaddingBottom(float value)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        Style.PaddingBottom = value;
        return this;
    }

    /// <summary>
    /// Sets the left padding of the layout node to the specified value.
    /// </summary>
    /// <param name="value">The amount of padding to apply on the left side, measured in units.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode PaddingLeft(float value)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        Style.PaddingLeft = value;
        return this;
    }

    /// <summary>
    /// Sets the alignment for both horizontal and vertical content of the layout node.
    /// </summary>
    /// <param name="alignment">The alignment value to apply to the content. Range is typically between 0.0 and 1.0.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode AlignContent(float alignment)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        Style.AlignContentHorizontal = alignment;
        Style.AlignContentVertical = alignment;
        return this;
    }

    /// <summary>
    /// Configures the alignment of content within the layout node using specified horizontal and vertical alignment factors.
    /// </summary>
    /// <param name="horizontal">The horizontal alignment factor, where 0.0 aligns to the start, and 1.0 aligns to the end.</param>
    /// <param name="vertical">The vertical alignment factor, where 0.0 aligns to the top, and 1.0 aligns to the bottom.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode AlignContent(float horizontal, float vertical)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        Style.AlignContentHorizontal = horizontal;
        Style.AlignContentVertical = vertical;
        return this;
    }

    /// <summary>
    /// Sets the horizontal alignment of content within the layout node.
    /// </summary>
    /// <param name="horizontal">The horizontal alignment value, where 0.0 aligns the content to the left, 0.5 centers the content, and 1.0 aligns the content to the right.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode ContentAlignX(float horizontal)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        Style.AlignContentHorizontal = horizontal;
        return this;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="vertical"></param>
    /// <returns></returns>
    public LayoutNode ContentAlignY(float vertical)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        Style.AlignContentVertical = vertical;
        return this;
    }

    /// <summary>
    /// Sets the alignment behavior for the layout node relative to its parent container.
    /// </summary>
    /// <param name="alignment">The alignment value to apply. Typically ranges from 0.0 to 1.0, where 0.0 represents the start and 1.0 represents the end of the container. Intermediate values determine proportional alignment.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode AlignSelf(float alignment)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        Style.AlignSelf = alignment;
        return this;
    }

    /// <summary>
    /// Sets the primary axis direction for the layout node, determining how child nodes should be arranged.
    /// </summary>
    /// <param name="direction">The axis direction to set. Possible values include <see cref="Axis.None"/>, <see cref="Axis.Horizontal"/>, or <see cref="Axis.Vertical"/>.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode Direction(Axis direction)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        Style.Direction = direction;
        return this;
    }

    /// <summary>
    /// Enables wrapping behavior for the layout node, allowing content to wrap within its container.
    /// </summary>
    /// <param name="qty">An integer parameter that represents additional configuration for the wrap behavior.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode Wrap(int qty)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        Style.Wrap = true;
        return this;
    }

    /// <summary>
    /// Sets the width of the layout node.
    /// </summary>
    /// <param name="width">The width value to set for the layout node.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode Width(float width)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        Style.Width = width;
        return this;
    }

    /// <summary>
    /// Sets the height of the layout node to the specified value.
    /// </summary>
    /// <param name="height">The height value to be set for the layout node.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode Height(float height)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        Style.Height = height;
        return this;
    }

    /// <summary>
    /// Sets the left position of the layout node.
    /// </summary>
    /// <param name="value">The new X-coordinate value defining the left position of the layout node.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode Left(float value)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        _rect = _rect with { X = value };
        return this;
    }

    /// <summary>
    /// Sets the top position of the layout node to the specified value.
    /// </summary>
    /// <param name="value">The Y-coordinate value to set the top position of the layout node.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode Top(float value)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        _rect = _rect with { Y = value };
        return this;
    }

    /// <summary>
    /// Configures the layout node to set both its width and height to the specified value.
    /// </summary>
    /// <param name="value">The size value to be applied equally to both width and height.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode Size(float value)
    {
        if (_gui.Pass != Pass.Pass1Build) return this;

        Style.Height = value;
        Style.Width = value;
        return this;
    }

    /// <summary>
    /// Retrieves the interactable element associated with the current layout node.
    /// The returned interactable element can be used to handle user interactions such as hover and hold events.
    /// </summary>
    /// <returns>An instance of <see cref="InteractableElement"/> representing the interactable properties of the layout node.</returns>
    public InteractableElement GetInteractable()
    {
        return _gui.GetInteractable(this);
    }
}
