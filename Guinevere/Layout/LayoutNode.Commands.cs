namespace Guinevere;

/// <summary>
/// Fluent sizing, constraint and positioning commands. They apply during the build pass only and take
/// <see cref="Size"/> values, so plain numbers are pixels and any term can be mixed in.
/// </summary>
public partial class LayoutNode
{
    /// <summary>Whether this is the build pass; if so, marks layout dirty so the caller can apply its change.</summary>
    bool BeginCommand()
    {
        if (_gui.Pass != Pass.Pass1Build)
        {
            return false;
        }

        InvalidateLayout();
        return true;
    }

    internal void ApplyWidth(Size? value)
    {
        InvalidateLayout();
        Style.Width = value;
    }

    internal void ApplyHeight(Size? value)
    {
        InvalidateLayout();
        Style.Height = value;
    }

    /// <summary>Shares the remaining space on both axes by weight.</summary>
    /// <param name="widthWeight">Expand weight of the width.</param>
    /// <param name="heightWeight">Expand weight of the height.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode Expand(float widthWeight = 1f, float heightWeight = 1f)
    {
        if (BeginCommand())
        {
            Style.Width = Guinevere.Size.Expand(widthWeight);
            Style.Height = Guinevere.Size.Expand(heightWeight);
        }

        return this;
    }

    /// <summary>Shares the remaining width by weight.</summary>
    /// <param name="weight">Expand weight of the width.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode ExpandWidth(float weight = 1f) => Width(Guinevere.Size.Expand(weight));

    /// <summary>Shares the remaining height by weight.</summary>
    /// <param name="weight">Expand weight of the height.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode ExpandHeight(float weight = 1f) => Height(Guinevere.Size.Expand(weight));

    /// <summary>Sets the width; null sizes to content along the parent's flow and stretches across it.</summary>
    /// <param name="width">The width.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode Width(Size? width)
    {
        if (BeginCommand())
        {
            Style.Width = width;
        }

        return this;
    }

    /// <summary>Sets the height; null sizes to content along the parent's flow and stretches across it.</summary>
    /// <param name="height">The height.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode Height(Size? height)
    {
        if (BeginCommand())
        {
            Style.Height = height;
        }

        return this;
    }

    /// <summary>Sets the width and the height to the same size.</summary>
    /// <param name="value">The size of both axes.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode Size(Size value)
    {
        if (BeginCommand())
        {
            Style.Width = value;
            Style.Height = value;
        }

        return this;
    }

    /// <summary>Constrains the resolved width; null leaves a side unbounded.</summary>
    /// <param name="min">Lower bound.</param>
    /// <param name="max">Upper bound.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode WidthConstraint(Size? min = null, Size? max = null)
    {
        if (BeginCommand())
        {
            Style.MinWidth = min;
            Style.MaxWidth = max;
        }

        return this;
    }

    /// <summary>Constrains the resolved height; null leaves a side unbounded.</summary>
    /// <param name="min">Lower bound.</param>
    /// <param name="max">Upper bound.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode HeightConstraint(Size? min = null, Size? max = null)
    {
        if (BeginCommand())
        {
            Style.MinHeight = min;
            Style.MaxHeight = max;
        }

        return this;
    }

    /// <summary>Sets the lower bound of the width, or null for none.</summary>
    /// <param name="value">Lower bound.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode MinWidth(Size? value)
    {
        if (BeginCommand())
        {
            Style.MinWidth = value;
        }

        return this;
    }

    /// <summary>Sets the upper bound of the width, or null for none.</summary>
    /// <param name="value">Upper bound.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode MaxWidth(Size? value)
    {
        if (BeginCommand())
        {
            Style.MaxWidth = value;
        }

        return this;
    }

    /// <summary>Sets the lower bound of the height, or null for none.</summary>
    /// <param name="value">Lower bound.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode MinHeight(Size? value)
    {
        if (BeginCommand())
        {
            Style.MinHeight = value;
        }

        return this;
    }

    /// <summary>Sets the upper bound of the height, or null for none.</summary>
    /// <param name="value">Upper bound.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode MaxHeight(Size? value)
    {
        if (BeginCommand())
        {
            Style.MaxHeight = value;
        }

        return this;
    }

    /// <summary>Takes the node out of the flow at a horizontal offset, keeping its vertical offset.</summary>
    /// <param name="value">Offset from the left of the parent's content box.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode Left(Size value)
    {
        if (BeginCommand())
        {
            _rect = _rect with { X = value.PixelsContribution };
            Style.AbsoluteX = value;
            MarkAbsolute(Style.AbsoluteOrigin);
        }

        return this;
    }

    /// <summary>Takes the node out of the flow at a vertical offset, keeping its horizontal offset.</summary>
    /// <param name="value">Offset from the top of the parent's content box.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode Top(Size value)
    {
        if (BeginCommand())
        {
            _rect = _rect with { Y = value.PixelsContribution };
            Style.AbsoluteY = value;
            MarkAbsolute(Style.AbsoluteOrigin);
        }

        return this;
    }

    /// <summary>
    /// Takes this node out of its parent's flow and places it at <paramref name="x"/>, <paramref name="y"/> from the
    /// top-left of the parent's content box; percentages use that box.
    /// </summary>
    /// <param name="x">Horizontal offset.</param>
    /// <param name="y">Vertical offset.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode Absolute(Size x, Size y) => PlaceAbsolute(x, y, AbsoluteOrigin.Parent);

    /// <summary>
    /// Takes this node out of its parent's flow and places it at a screen position, wherever the node sits in the
    /// tree; percentages use the screen. Overlays positioned from the cursor or another node's rect want this.
    /// </summary>
    /// <param name="x">Screen-space horizontal offset.</param>
    /// <param name="y">Screen-space vertical offset.</param>
    /// <returns>This node, for chaining.</returns>
    public LayoutNode AbsoluteScreen(Size x, Size y) => PlaceAbsolute(x, y, AbsoluteOrigin.Screen);

    LayoutNode PlaceAbsolute(Size x, Size y, AbsoluteOrigin origin)
    {
        if (_gui.Pass != Pass.Pass1Build)
        {
            return this;
        }

        _rect = _rect with { X = x.PixelsContribution, Y = y.PixelsContribution };
        Style.AbsoluteX = x;
        Style.AbsoluteY = y;
        MarkAbsolute(origin);
        return this;
    }

    /// <summary>
    /// Makes this node swallow pointer input for anything drawn beneath it: while the cursor is
    /// inside its rect, <see cref="InteractableElement.OnHover"/> reports false for every node that
    /// is not this node or one of its descendants. Ties are broken by z-index, so a floating panel
    /// raised with <see cref="Gui.SetZIndex"/> wins over one left at the default.
    /// </summary>
    /// <param name="value">Whether the node blocks input. Defaults to true.</param>
    /// <returns>The current instance of <see cref="LayoutNode"/>, enabling method chaining.</returns>
    public LayoutNode BlockInput(bool value = true)
    {
        if (_gui.Pass != Pass.Pass1Build)
        {
            return this;
        }

        Style.BlocksInput = value;
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
