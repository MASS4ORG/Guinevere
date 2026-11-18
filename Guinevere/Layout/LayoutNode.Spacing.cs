namespace Guinevere;

/// <summary>
/// Resolution of padding, margins, gaps and alignment from <see cref="Size"/> values, and the distribution of free
/// space along a flow between expanding gaps, expanding margins and <see cref="SpacingTypes"/>.
/// </summary>
public partial class LayoutNode
{
    /// <summary>The parent's content box size, which this node's padding and margins resolve against.</summary>
    Vector2 _spacingBasis;

    // Padding and margins resolved for the current basis; layout reads them many times per child.
    float _paddingTop, _paddingRight, _paddingBottom, _paddingLeft;
    float _marginTop, _marginRight, _marginBottom, _marginLeft;
    float _marginTopWeight, _marginRightWeight, _marginBottomWeight, _marginLeftWeight;

    /// <summary>Whether padding, margins and gap need no basis, so intrinsic sizes can be cached bottom-up.</summary>
    bool _spacingIsPixels = true;

    /// <summary>Resolved top padding in pixels.</summary>
    public float PaddingTopPixels => _paddingTop;

    /// <summary>Resolved right padding in pixels.</summary>
    public float PaddingRightPixels => _paddingRight;

    /// <summary>Resolved bottom padding in pixels.</summary>
    public float PaddingBottomPixels => _paddingBottom;

    /// <summary>Resolved left padding in pixels.</summary>
    public float PaddingLeftPixels => _paddingLeft;

    /// <summary>Resolved top margin in pixels; an expand weight adds free space when placed.</summary>
    public float MarginTopPixels => _marginTop;

    /// <summary>Resolved right margin in pixels; an expand weight adds free space when placed.</summary>
    public float MarginRightPixels => _marginRight;

    /// <summary>Resolved bottom margin in pixels; an expand weight adds free space when placed.</summary>
    public float MarginBottomPixels => _marginBottom;

    /// <summary>Resolved left margin in pixels; an expand weight adds free space when placed.</summary>
    public float MarginLeftPixels => _marginLeft;

    float HorizontalPadding => _paddingLeft + _paddingRight;

    float VerticalPadding => _paddingTop + _paddingBottom;

    float HorizontalMargin => _marginLeft + _marginRight;

    float VerticalMargin => _marginTop + _marginBottom;

    /// <summary>Re-reads the spacing style; call after padding, margins or gap change.</summary>
    void RefreshSpacing()
    {
        ref readonly var style = ref Style;
        _spacingIsPixels = style.SpacingIsPixels;
        if (_spacingIsPixels)
        {
            var padding = style.PaddingPixels;
            var margin = style.MarginPixels;
            (_paddingTop, _paddingRight, _paddingBottom, _paddingLeft) = (padding.X, padding.Y, padding.Z, padding.W);
            (_marginTop, _marginRight, _marginBottom, _marginLeft) = (margin.X, margin.Y, margin.Z, margin.W);
            _marginTopWeight = _marginRightWeight = _marginBottomWeight = _marginLeftWeight = 0f;
            return;
        }

        _marginTopWeight = style.MarginTop.ExpandContribution;
        _marginRightWeight = style.MarginRight.ExpandContribution;
        _marginBottomWeight = style.MarginBottom.ExpandContribution;
        _marginLeftWeight = style.MarginLeft.ExpandContribution;
        ResolveSpacing();
    }

    /// <summary>Sets the content box padding and margins resolve against, re-resolving relative ones.</summary>
    void SetSpacingBasis(Vector2 basis)
    {
        _spacingBasis = basis;
        if (!_spacingIsPixels)
        {
            ResolveSpacing();
        }
    }

    void ResolveSpacing()
    {
        ref readonly var style = ref Style;
        _paddingTop = ResolveEdge(style.PaddingTop, horizontal: false);
        _paddingRight = ResolveEdge(style.PaddingRight, horizontal: true);
        _paddingBottom = ResolveEdge(style.PaddingBottom, horizontal: false);
        _paddingLeft = ResolveEdge(style.PaddingLeft, horizontal: true);
        _marginTop = ResolveEdge(style.MarginTop, horizontal: false);
        _marginRight = ResolveEdge(style.MarginRight, horizontal: true);
        _marginBottom = ResolveEdge(style.MarginBottom, horizontal: false);
        _marginLeft = ResolveEdge(style.MarginLeft, horizontal: true);
    }

    float ResolveEdge(in Size value, bool horizontal)
    {
        if (value.IsPixelsOnly)
        {
            return value.PixelsContribution;
        }

        var along = horizontal ? _spacingBasis.X : _spacingBasis.Y;
        var across = horizontal ? _spacingBasis.Y : _spacingBasis.X;
        return value.ResolveBasis(along, across) + ResolveReferences(value, horizontal);
    }

    /// <summary>The fixed part of one gap for a content box; its expand weight is shared during placement.</summary>
    /// <param name="along">Content size along the flow.</param>
    /// <param name="across">Content size across the flow.</param>
    float GapPixels(float along, float across) => Style.GapIsPixels
        ? Style.GapPixels
        : Style.Gap.ResolveBasis(along, across) + ResolveReferences(Style.Gap, Style.Direction == Axis.Horizontal);

    /// <summary>Gives every child the content box its padding and margins resolve against.</summary>
    void SetChildSpacingBasis(Vector2 contentSize)
    {
        foreach (var child in ChildNodes)
        {
            child.SetSpacingBasis(contentSize);
        }
    }

    /// <summary>An alignment offset: its percentage of the free space plus its pixel offset.</summary>
    float ResolveAlignment(in Size alignment, float free, bool horizontal) =>
        alignment.PixelsContribution + alignment.PercentageContribution * free
        + (alignment.References.IsEmpty ? 0f : ResolveReferences(alignment, horizontal));

    /// <summary>How free space along a flow is placed before the first child, per gap and per expand weight.</summary>
    /// <param name="Leading">Offset of the first child.</param>
    /// <param name="Between">Space added to every gap.</param>
    /// <param name="PerWeight">Space each unit of margin expand weight receives.</param>
    readonly record struct FlowDistribution(float Leading, float Between, float PerWeight);

    /// <summary>
    /// Distributes free space along the flow. Expanding sizes take it first; otherwise expanding gaps, expanding
    /// margins and <see cref="SpacingTypes"/> share it; whatever remains is placed by the content alignment.
    /// </summary>
    FlowDistribution DistributeFlow(float free, bool horizontal, bool sizesExpand)
    {
        free = Math.Max(0f, free);
        var (leading, between, trailing) = SpacingWeights(Style.Spacing);
        between += Style.Gap.ExpandContribution;
        var weights = leading + trailing + between * (FlowChildren.Count - 1);
        foreach (var child in FlowChildren)
        {
            weights += horizontal
                ? child._marginLeftWeight + child._marginRightWeight
                : child._marginTopWeight + child._marginBottomWeight;
        }

        if (sizesExpand || weights <= 0f || free <= 0f)
        {
            var alignment = Style.Spacing switch
            {
                SpacingTypes.Start => Guinevere.Size.Percentage(0f),
                SpacingTypes.Center => Guinevere.Size.Percentage(0.5f),
                SpacingTypes.End => Guinevere.Size.Percentage(1f),
                _ => Style.GetAlignContentForDirection(horizontal ? Axis.Horizontal : Axis.Vertical),
            };
            return new FlowDistribution(ResolveAlignment(alignment, free, horizontal), 0f, 0f);
        }

        var perWeight = free / weights;
        return new FlowDistribution(leading * perWeight, between * perWeight, perWeight);
    }

    static (float Leading, float Between, float Trailing) SpacingWeights(SpacingTypes spacing) => spacing switch
    {
        SpacingTypes.SpaceBetween => (0f, 1f, 0f),
        SpacingTypes.SpaceAround => (0.5f, 1f, 0.5f),
        SpacingTypes.SpaceEvenly => (1f, 1f, 1f),
        _ => (0f, 0f, 0f),
    };

    /// <summary>
    /// A child's offset in the free space across the flow: expanding margins split it, otherwise the child's own
    /// alignment or this node's content alignment places it.
    /// </summary>
    float CrossOffset(LayoutNode child, float free, bool horizontal)
    {
        free = Math.Max(0f, free);
        var leading = horizontal ? child._marginLeftWeight : child._marginTopWeight;
        var trailing = horizontal ? child._marginRightWeight : child._marginBottomWeight;
        if (leading + trailing > 0f)
        {
            return free * leading / (leading + trailing);
        }

        return child.Style.HasAlignSelf
            ? ResolveAlignment(child.Style.AlignSelf!.Value, free, horizontal)
            : ResolveAlignment(horizontal ? Style.AlignContentHorizontal : Style.AlignContentVertical, free,
                horizontal);
    }

    /// <summary>The leading expand weight of a child's margin along a flow.</summary>
    static float LeadingMarginWeight(LayoutNode child, bool horizontal) =>
        horizontal ? child._marginLeftWeight : child._marginTopWeight;

    /// <summary>The trailing expand weight of a child's margin along a flow.</summary>
    static float TrailingMarginWeight(LayoutNode child, bool horizontal) =>
        horizontal ? child._marginRightWeight : child._marginBottomWeight;
}
