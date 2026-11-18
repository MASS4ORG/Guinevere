namespace Guinevere;

/// <summary>
/// Size resolution and content measurement. A <see cref="Size"/> resolves against the space offered to the node:
/// percentages of it, ratios of the perpendicular size, a share of remaining space for expand weights, the node's
/// content for fit terms and other nodes' rects for references. Placement along flows lives in the flow partial.
/// </summary>
public partial class LayoutNode
{
    /// <summary>
    /// Calculates and sets the rectangle for this layout node
    /// </summary>
    public void CalculateLayout()
    {
        if (_parent is not null)
        {
            InitializeChildLayout();
            return;
        }

        var screenRect = _gui.ScreenRect;
        if (_hasLayout && !_layoutDirty && _lastLayoutScreenRect == screenRect)
        {
            return;
        }

        SetSpacingBasis(screenRect.Size);
        PrepareIntrinsicMeasurements(Vector2.Zero);
        _lastLayoutScreenRect = screenRect;
        RunReferenceAwareLayout();
        _layoutDirty = false;
        _hasLayout = true;
        LayoutVersion++;
    }

    void PrepareIntrinsicMeasurements(Vector2 inheritedScrollOffset)
    {
        RefreshSpacing();
        _ancestorScrollOffset = Style.IsAbsolute ? Vector2.Zero : inheritedScrollOffset;
        var childScrollOffset = Style.IsAbsolute ? Vector2.Zero : inheritedScrollOffset;
        if (Scope.HasLocal<LayoutNodeScopeLocalScrollOffset>())
        {
            childScrollOffset += Scope.Get<LayoutNodeScopeLocalScrollOffset>().Value;
        }

        _hasMeasuredContent = ContentMeasure is not null;
        foreach (var child in ChildNodes)
        {
            child.PrepareIntrinsicMeasurements(childScrollOffset);
            _hasMeasuredContent |= child._hasMeasuredContent;
        }

        MeasureIntrinsicContent();
    }

    /// <summary>Sizes this node from its children's intrinsic sizes, or from its measured content as a leaf.</summary>
    void MeasureIntrinsicContent()
    {
        // Spacing that needs a basis cannot be cached before the parent's content box is known.
        var spacingIsPixels = _spacingIsPixels;
        var horizontalPadding = HorizontalPadding;
        var verticalPadding = VerticalPadding;
        _intrinsicWidthValid = spacingIsPixels && (!Style.Wrap || Style.Direction != Axis.Horizontal);
        _intrinsicHeightValid = spacingIsPixels && !Style.Wrap;

        if (FlowChildren.Count == 0 && ContentMeasure is { } measure)
        {
            var natural = measure(0f);
            _intrinsicContentWidth = natural.X + horizontalPadding;
            _intrinsicContentHeight = natural.Y + verticalPadding;
            _intrinsicWidthValid = false;
            _intrinsicHeightValid = false;
            return;
        }

        if (FlowChildren.Count == 0)
        {
            _intrinsicContentWidth = 10f + horizontalPadding;
            _intrinsicContentHeight = 10f + verticalPadding;
            return;
        }

        AccumulateIntrinsicChildren(horizontalPadding, verticalPadding);
    }

    void AccumulateIntrinsicChildren(float horizontalPadding, float verticalPadding)
    {
        var gap = Style.GapPixels;
        var width = Style.Direction == Axis.Horizontal ? (FlowChildren.Count - 1) * gap : 0f;
        var height = Style.Direction == Axis.Vertical ? (FlowChildren.Count - 1) * gap : 0f;
        foreach (var child in FlowChildren)
        {
            _intrinsicWidthValid &= ChildWidthIsIntrinsic(child);
            _intrinsicHeightValid &= ChildHeightIsIntrinsic(child);

            ref readonly var style = ref child.Style;
            var childWidth = style.WidthIsFixed ? style.FixedWidth : child._intrinsicContentWidth;
            if (style.HasWidthBounds)
            {
                childWidth = ClampPixels(childWidth, style.MinWidthPixels, style.MaxWidthPixels);
            }

            var childHeight = style.HeightIsFixed ? style.FixedHeight : child._intrinsicContentHeight;
            if (style.HasHeightBounds)
            {
                childHeight = ClampPixels(childHeight, style.MinHeightPixels, style.MaxHeightPixels);
            }

            childWidth += child.HorizontalMargin;
            childHeight += child.VerticalMargin;

            width = Style.Direction == Axis.Horizontal ? width + childWidth : Math.Max(width, childWidth);
            height = Style.Direction == Axis.Vertical ? height + childHeight : Math.Max(height, childHeight);
        }

        _intrinsicContentWidth = width + horizontalPadding;
        _intrinsicContentHeight = height + verticalPadding;
    }

    /// <summary>Clamps a size by pixel bounds, a minimum winning when the bounds conflict.</summary>
    static float ClampPixels(float size, float min, float max) => Math.Min(Math.Max(size, min), Math.Max(max, min));

    static bool ChildWidthIsIntrinsic(LayoutNode child)
    {
        ref readonly var style = ref child.Style;
        return (style.HasWidth ? style.WidthIsFixed : child._intrinsicWidthValid)
               && style.WidthBoundsArePixels && child._spacingIsPixels;
    }

    static bool ChildHeightIsIntrinsic(LayoutNode child)
    {
        ref readonly var style = ref child.Style;
        return (style.HasHeight ? style.HeightIsFixed : child._intrinsicHeightValid)
               && style.HeightBoundsArePixels && child._spacingIsPixels;
    }

    void InitializeRootLayout()
    {
        Place(_gui.ScreenRect);
        if (ChildNodes.Count > 0)
        {
            LayoutChildren();
        }
    }

    /// <summary>
    /// Applies cumulative scroll offset to this node's position
    /// </summary>
    public void ApplyScrollOffset()
    {
        // Only nodes inside a scrollable container move; the container itself and absolute nodes stay.
        if (Style.IsAbsolute || _ancestorScrollOffset == Vector2.Zero)
        {
            return;
        }

        _rect = new Rect(_rect.X - _ancestorScrollOffset.X, _rect.Y - _ancestorScrollOffset.Y, _rect.W, _rect.H);
    }

    // Width is resolved before height; a wrapped horizontal container needs it to know how many
    // lines its children break into when computing its own content height.
    float _resolvedWidthForHeightPass;

    // Whether this node or a descendant sizes its height from a width through ContentMeasure.
    bool _hasMeasuredContent;

    void InitializeChildLayout()
    {
        var parentInner = _parent!.InnerRect;
        RefreshSpacing();
        SetSpacingBasis(parentInner.Size);
        var myWidth = CalculateWidth(parentInner.W);
        _resolvedWidthForHeightPass = myWidth;
        var myHeight = CalculateHeight(parentInner.H);
        Place(new Rect(0, 0, myWidth, myHeight));
    }

    float CalculateWidth(float availableWidth)
    {
        var perpendicular = Math.Max(0f, _rect.H);
        var width = Style.HasWidth
            ? OwnWidth(availableWidth, perpendicular, availableWidth)
            // A wrapping row fills the available width (like a block-level flex container),
            // so its children have a width to wrap against instead of sizing to their sum.
            : Style.Wrap && Style.Direction == Axis.Horizontal
                ? availableWidth
                : CalculateContentWidth(availableWidth);
        return ClampResolvedWidth(width, availableWidth, perpendicular, availableWidth);
    }

    float CalculateHeight(float availableHeight)
    {
        var perpendicular = Math.Max(0f, _resolvedWidthForHeightPass);
        var height = Style.HasHeight
            ? OwnHeight(availableHeight, perpendicular, availableHeight)
            : CalculateContentHeight(availableHeight);
        return ClampResolvedHeight(height, availableHeight, perpendicular, availableHeight);
    }

    /// <summary>This node's set width resolved for the space offered; the caller checks a width is set.</summary>
    float OwnWidth(float available, float perpendicular, float expandShare = 0f) => Style.WidthIsFixed
        ? Style.FixedWidth
        : ResolveWidth(Style.Width!.Value, available, perpendicular, expandShare);

    /// <summary>This node's set height resolved for the space offered; the caller checks a height is set.</summary>
    float OwnHeight(float available, float perpendicular, float expandShare = 0f) => Style.HeightIsFixed
        ? Style.FixedHeight
        : ResolveHeight(Style.Height!.Value, available, perpendicular, expandShare);

    float ClampResolvedWidth(float width, float available, float perpendicular, float expandShare)
    {
        if (!Style.HasWidthBounds)
        {
            return width;
        }

        if (Style.WidthBoundsArePixels)
        {
            return ClampPixels(width, Style.MinWidthPixels, Style.MaxWidthPixels);
        }

        var min = Style.MinWidth is { } minValue
            ? Math.Max(0f, ResolveWidth(minValue, available, perpendicular, expandShare)) : 0f;
        var max = Style.MaxWidth is { } maxValue
            ? Math.Max(0f, ResolveWidth(maxValue, available, perpendicular, expandShare)) : float.MaxValue;
        if (!float.IsFinite(min) || !float.IsFinite(max))
        {
            throw new InvalidOperationException($"Width constraint of node '{Id}' resolved to a non-finite value.");
        }

        // A minimum wins when the bounds conflict.
        return Math.Min(Math.Max(width, min), Math.Max(max, min));
    }

    float ClampResolvedHeight(float height, float available, float perpendicular, float expandShare)
    {
        if (!Style.HasHeightBounds)
        {
            return height;
        }

        if (Style.HeightBoundsArePixels)
        {
            return ClampPixels(height, Style.MinHeightPixels, Style.MaxHeightPixels);
        }

        var min = Style.MinHeight is { } minValue
            ? Math.Max(0f, ResolveHeight(minValue, available, perpendicular, expandShare)) : 0f;
        var max = Style.MaxHeight is { } maxValue
            ? Math.Max(0f, ResolveHeight(maxValue, available, perpendicular, expandShare)) : float.MaxValue;
        if (!float.IsFinite(min) || !float.IsFinite(max))
        {
            throw new InvalidOperationException($"Height constraint of node '{Id}' resolved to a non-finite value.");
        }

        return Math.Min(Math.Max(height, min), Math.Max(max, min));
    }

    float ResolveWidth(Size value, float available, float perpendicular, float expandShare = 0f)
    {
        var width = value.ResolveBasis(available, perpendicular, expandShare);
        if (value.FitContentContribution != 0f)
        {
            width += CalculateContentWidth(available) * value.FitContentContribution;
        }

        if (value.FitLargestContribution != 0f)
        {
            width += CalculateLargestChildWidth(available) * value.FitLargestContribution;
        }

        return value.References.IsEmpty ? width : width + ResolveReferences(value, horizontal: true);
    }

    float ResolveHeight(Size value, float available, float perpendicular, float expandShare = 0f)
    {
        var height = value.ResolveBasis(available, perpendicular, expandShare);
        if (value.FitContentContribution != 0f)
        {
            height += CalculateContentHeight(available, perpendicular) * value.FitContentContribution;
        }

        if (value.FitLargestContribution != 0f)
        {
            height += CalculateLargestChildHeight(available) * value.FitLargestContribution;
        }

        return value.References.IsEmpty ? height : height + ResolveReferences(value, horizontal: false);
    }

    /// <summary>The width a node asks for before sharing space; an expand term measures as content.</summary>
    float NaturalWidth(float available, float perpendicular)
    {
        if (Style.WidthIsFixed)
        {
            return Style.FixedWidth;
        }

        var width = OwnWidth(available, perpendicular);
        return Style.WidthExpandWeight == 0f ? width : width + Math.Max(CalculateContentWidth(available), 10f);
    }

    /// <summary>The height a node asks for before sharing space; an expand term measures as content.</summary>
    float NaturalHeight(float available, float perpendicular, float widthForWrap)
    {
        if (Style.HeightIsFixed)
        {
            return Style.FixedHeight;
        }

        var height = OwnHeight(available, perpendicular);
        return Style.HeightExpandWeight == 0f
            ? height
            : height + Math.Max(CalculateContentHeight(available, widthForWrap), 10f);
    }

    float CalculateLargestChildWidth(float availableWidth)
    {
        var largest = 0f;
        foreach (var child in FlowChildren)
        {
            largest = Math.Max(largest, GetChildTotalWidth(child, availableWidth));
        }

        return largest + HorizontalPadding;
    }

    float CalculateLargestChildHeight(float availableHeight)
    {
        var largest = 0f;
        foreach (var child in FlowChildren)
        {
            largest = Math.Max(largest, GetChildTotalHeight(child, availableHeight, _resolvedWidthForHeightPass));
        }

        return largest + VerticalPadding;
    }

    float CalculateContentWidth(float availableWidth)
    {
        if (_intrinsicWidthValid)
        {
            return _intrinsicContentWidth;
        }

        var padding = HorizontalPadding;
        if (FlowChildren.Count == 0)
        {
            return ContentMeasure is { } measure
                ? measure(Math.Max(0f, availableWidth - padding)).X + padding
                : (_rect.W > 0 ? _rect.W : 10f) + padding;
        }

        var inner = Math.Max(0f, availableWidth - padding);
        foreach (var child in FlowChildren)
        {
            child.SetSpacingBasis(child._spacingBasis with { X = inner });
        }

        var content = Style.Direction == Axis.Horizontal
            ? CalculateHorizontalContentWidth(inner)
            : CalculateVerticalContentWidth(inner);
        return content + padding;
    }

    float CalculateHorizontalContentWidth(float availableWidth)
    {
        var total = (FlowChildren.Count - 1) * GapPixels(availableWidth, Math.Max(0f, _rect.H - VerticalPadding));
        foreach (var child in FlowChildren)
        {
            total += GetChildTotalWidth(child, availableWidth);
        }

        return total;
    }

    float CalculateVerticalContentWidth(float availableWidth)
    {
        var largest = 10f;
        foreach (var child in FlowChildren)
        {
            largest = Math.Max(largest, GetChildTotalWidth(child, availableWidth));
        }

        return largest;
    }

    float GetChildTotalWidth(LayoutNode child, float availableWidth)
    {
        var perpendicular = Math.Max(0f, child.Rect.H);
        var contentWidth = child.Style.HasWidth
            ? child.NaturalWidth(availableWidth, perpendicular)
            : Math.Max(child.CalculateContentWidth(availableWidth), 10f);

        return child.HorizontalMargin
               + child.ClampResolvedWidth(contentWidth, availableWidth, perpendicular, availableWidth);
    }

    float CalculateContentHeight(float availableHeight, float outerWidthForWrap = -1f)
    {
        if (_intrinsicHeightValid)
        {
            return _intrinsicContentHeight;
        }

        var padding = VerticalPadding;
        if (FlowChildren.Count == 0)
        {
            return ContentMeasure is { } measure
                ? MeasuredContentHeight(measure, outerWidthForWrap) + padding
                : (_rect.H > 0 ? _rect.H : 10f) + padding;
        }

        var inner = Math.Max(0f, availableHeight - padding);
        foreach (var child in FlowChildren)
        {
            child.SetSpacingBasis(child._spacingBasis with { Y = inner });
        }

        float content;
        if (Style.Direction == Axis.Vertical)
        {
            content = CalculateVerticalContentHeight(inner, WidthForChildren(outerWidthForWrap));
        }
        else if (Style.Wrap)
        {
            var outer = outerWidthForWrap >= 0f ? outerWidthForWrap : _resolvedWidthForHeightPass;
            content = CalculateWrappedContentHeight(Math.Max(0f, outer - HorizontalPadding));
        }
        else
        {
            content = CalculateHorizontalContentHeight(inner, WidthForChildren(outerWidthForWrap));
        }

        return content + padding;
    }

    /// <summary>
    /// The outer width descendants' heights are measured against. Measured content can be asked for its height
    /// before this node's own width pass, so it uses the fixed width or the width the parent offers.
    /// </summary>
    float WidthForChildren(float outerWidthForWrap) => !_hasMeasuredContent ? _resolvedWidthForHeightPass
        : Style.WidthIsFixed ? Style.FixedWidth
        : outerWidthForWrap >= 0f ? outerWidthForWrap
        : _resolvedWidthForHeightPass;

    float MeasuredContentHeight(Func<float, Vector2> measure, float outerWidthForWrap) =>
        measure(Math.Max(0f, WidthForChildren(outerWidthForWrap) - HorizontalPadding)).Y;

    float CalculateVerticalContentHeight(float availableHeight, float width)
    {
        var total = (FlowChildren.Count - 1) * GapPixels(availableHeight, Math.Max(0f, width - HorizontalPadding));
        foreach (var child in FlowChildren)
        {
            total += GetChildTotalHeight(child, availableHeight, width);
        }

        return total;
    }

    float CalculateHorizontalContentHeight(float availableHeight, float width)
    {
        var largest = 10f;
        foreach (var child in FlowChildren)
        {
            largest = Math.Max(largest, GetChildTotalHeight(child, availableHeight, width));
        }

        return largest;
    }

    float GetChildTotalHeight(LayoutNode child, float availableHeight, float width)
    {
        var perpendicular = Math.Max(0f, child.Rect.W);
        var widthForWrap = Math.Max(0f, width - HorizontalPadding) - child.HorizontalMargin;
        var contentHeight = child.Style.HasHeight
            ? child.NaturalHeight(availableHeight, perpendicular, widthForWrap)
            : Math.Max(child.CalculateContentHeight(availableHeight, widthForWrap), 10f);

        return child.VerticalMargin
               + child.ClampResolvedHeight(contentHeight, availableHeight, perpendicular, availableHeight);
    }
}
