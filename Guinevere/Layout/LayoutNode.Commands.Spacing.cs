namespace Guinevere;

/// <summary>
/// Fluent spacing, alignment and flow commands. Gaps, margins and padding take <see cref="Size"/> values; an expand
/// weight on a gap or margin shares the free space along the flow. Alignment given as a plain number is a fraction
/// of the free space (0 start, 0.5 center, 1 end); a <see cref="Size"/> adds pixel offsets to that fraction.
/// </summary>
public partial class LayoutNode
{
    /// <summary>Sets the space between adjacent children.</summary>
    /// <param name="gap">The gap; percentages use this node's content box along the flow.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode Gap(Size gap)
    {
        if (BeginCommand())
        {
            Style.Gap = gap;
            RefreshSpacing();
        }

        return this;
    }

    /// <summary>Distributes free space along the flow between the children.</summary>
    /// <param name="spacing">The distribution; start, center and end align the children as a group.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode Spacing(SpacingTypes spacing)
    {
        if (BeginCommand())
        {
            Style.Spacing = spacing;
        }

        return this;
    }

    /// <summary>Sets the same margin on every side.</summary>
    /// <param name="value">The margin; percentages use the parent's content box on each side's axis.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode Margin(Size value)
    {
        if (SetMargins(value, value, value, value)._gui.Pass == Pass.Pass1Build)
        {
            Style.HasSpecificMargins = false;
        }

        return this;
    }

    /// <summary>Sets the horizontal and vertical margins.</summary>
    /// <param name="horizontal">The left and right margin.</param>
    /// <param name="vertical">The top and bottom margin.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode Margin(Size horizontal, Size vertical) => SetMargins(vertical, horizontal, vertical, horizontal);

    /// <summary>Sets every margin.</summary>
    /// <param name="top">The top margin.</param>
    /// <param name="right">The right margin.</param>
    /// <param name="bottom">The bottom margin.</param>
    /// <param name="left">The left margin.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode Margin(Size top, Size right, Size bottom, Size left) => SetMargins(top, right, bottom, left);

    /// <summary>Sets the top margin.</summary>
    /// <param name="value">The margin.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode MarginTop(Size value) =>
        SetMargins(value, Style.MarginRight, Style.MarginBottom, Style.MarginLeft);

    /// <summary>Sets the right margin.</summary>
    /// <param name="value">The margin.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode MarginRight(Size value) =>
        SetMargins(Style.MarginTop, value, Style.MarginBottom, Style.MarginLeft);

    /// <summary>Sets the bottom margin.</summary>
    /// <param name="value">The margin.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode MarginBottom(Size value) =>
        SetMargins(Style.MarginTop, Style.MarginRight, value, Style.MarginLeft);

    /// <summary>Sets the left margin.</summary>
    /// <param name="value">The margin.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode MarginLeft(Size value) =>
        SetMargins(Style.MarginTop, Style.MarginRight, Style.MarginBottom, value);

    LayoutNode SetMargins(Size top, Size right, Size bottom, Size left)
    {
        if (BeginCommand())
        {
            Style.MarginTop = top;
            Style.MarginRight = right;
            Style.MarginBottom = bottom;
            Style.MarginLeft = left;
            Style.HasSpecificMargins = true;
            RefreshSpacing();
        }

        return this;
    }

    /// <summary>Sets the same padding on every side.</summary>
    /// <param name="value">The padding; percentages use the parent's content box on each side's axis.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode Padding(Size value) => SetPadding(value, value, value, value);

    /// <summary>Sets the horizontal and vertical padding.</summary>
    /// <param name="horizontal">The left and right padding.</param>
    /// <param name="vertical">The top and bottom padding.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode Padding(Size horizontal, Size vertical) => SetPadding(vertical, horizontal, vertical, horizontal);

    /// <summary>Sets every padding.</summary>
    /// <param name="top">The top padding.</param>
    /// <param name="right">The right padding.</param>
    /// <param name="bottom">The bottom padding.</param>
    /// <param name="left">The left padding.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode Padding(Size top, Size right, Size bottom, Size left) => SetPadding(top, right, bottom, left);

    /// <summary>Sets the top and bottom padding.</summary>
    /// <param name="value">The padding.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode PaddingY(Size value) => SetPadding(value, Style.PaddingRight, value, Style.PaddingLeft);

    /// <summary>Sets the left and right padding.</summary>
    /// <param name="value">The padding.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode PaddingX(Size value) => SetPadding(Style.PaddingTop, value, Style.PaddingBottom, value);

    /// <summary>Sets the top padding.</summary>
    /// <param name="value">The padding.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode PaddingTop(Size value) =>
        SetPadding(value, Style.PaddingRight, Style.PaddingBottom, Style.PaddingLeft);

    /// <summary>Sets the right padding.</summary>
    /// <param name="value">The padding.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode PaddingRight(Size value) =>
        SetPadding(Style.PaddingTop, value, Style.PaddingBottom, Style.PaddingLeft);

    /// <summary>Sets the bottom padding.</summary>
    /// <param name="value">The padding.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode PaddingBottom(Size value) =>
        SetPadding(Style.PaddingTop, Style.PaddingRight, value, Style.PaddingLeft);

    /// <summary>Sets the left padding.</summary>
    /// <param name="value">The padding.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode PaddingLeft(Size value) =>
        SetPadding(Style.PaddingTop, Style.PaddingRight, Style.PaddingBottom, value);

    LayoutNode SetPadding(Size top, Size right, Size bottom, Size left)
    {
        if (BeginCommand())
        {
            Style.PaddingTop = top;
            Style.PaddingRight = right;
            Style.PaddingBottom = bottom;
            Style.PaddingLeft = left;
            RefreshSpacing();
        }

        return this;
    }

    /// <summary>Places the children in the free space on both axes.</summary>
    /// <param name="alignment">Fraction of the free space: 0 start, 0.5 center, 1 end.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode AlignContent(float alignment) => AlignContent(alignment, alignment);

    /// <summary>Places the children in the free space on each axis.</summary>
    /// <param name="horizontal">Horizontal fraction of the free space.</param>
    /// <param name="vertical">Vertical fraction of the free space.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode AlignContent(float horizontal, float vertical) =>
        AlignContent(Guinevere.Size.Percentage(horizontal), Guinevere.Size.Percentage(vertical));

    /// <summary>Places the children in the free space on each axis with percentage and pixel terms.</summary>
    /// <param name="horizontal">Horizontal placement; percentages are of the free space.</param>
    /// <param name="vertical">Vertical placement; percentages are of the free space.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode AlignContent(Size horizontal, Size vertical)
    {
        if (BeginCommand())
        {
            Style.AlignContentHorizontal = horizontal;
            Style.AlignContentVertical = vertical;
        }

        return this;
    }

    /// <summary>Places the children horizontally in the free space.</summary>
    /// <param name="horizontal">Fraction of the free space: 0 left, 0.5 center, 1 right.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode AlignContentX(float horizontal) => AlignContentX(Guinevere.Size.Percentage(horizontal));

    /// <summary>Places the children horizontally with percentage and pixel terms.</summary>
    /// <param name="horizontal">Placement; percentages are of the free space.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode AlignContentX(Size horizontal)
    {
        if (BeginCommand())
        {
            Style.AlignContentHorizontal = horizontal;
        }

        return this;
    }

    /// <summary>Places the children vertically in the free space.</summary>
    /// <param name="vertical">Fraction of the free space: 0 top, 0.5 middle, 1 bottom.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode AlignContentY(float vertical) => AlignContentY(Guinevere.Size.Percentage(vertical));

    /// <summary>Places the children vertically with percentage and pixel terms.</summary>
    /// <param name="vertical">Placement; percentages are of the free space.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode AlignContentY(Size vertical)
    {
        if (BeginCommand())
        {
            Style.AlignContentVertical = vertical;
        }

        return this;
    }

    /// <summary>Centers the children on both axes.</summary>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode AlignChildrenCenter() => AlignContent(0.5f);

    /// <summary>Places this node across its parent's flow, overriding the parent's content alignment.</summary>
    /// <param name="alignment">Fraction of the free space: 0 start, 0.5 center, 1 end.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode AlignSelf(float alignment) => AlignSelf(Guinevere.Size.Percentage(alignment));

    /// <summary>Places this node across its parent's flow; null follows the parent's content alignment.</summary>
    /// <param name="alignment">Placement; percentages are of the free space.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode AlignSelf(Size? alignment)
    {
        if (BeginCommand())
        {
            Style.AlignSelf = alignment;
        }

        return this;
    }

    /// <summary>Sets the axis children flow along.</summary>
    /// <param name="direction">The flow axis.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode Direction(Axis direction)
    {
        if (BeginCommand())
        {
            Style.Direction = direction;
        }

        return this;
    }

    /// <summary>Alias of <see cref="Direction"/>.</summary>
    /// <param name="direction">The flow axis.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode FlowDir(Axis direction) => Direction(direction);

    /// <summary>Breaks a horizontal flow onto new lines when children overflow the content width.</summary>
    /// <param name="qty">Reserved for a maximum per line; currently unused.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode Wrap(int qty)
    {
        if (BeginCommand())
        {
            Style.Wrap = true;
        }

        return this;
    }
}
