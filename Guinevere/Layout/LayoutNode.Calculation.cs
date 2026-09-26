using System.Buffers;

namespace Guinevere;

/// <summary>
/// LayoutNode partial class containing layout calculation operations
/// </summary>
public partial class LayoutNode
{
    /// <summary>
    /// Calculates and sets the rectangle for this layout node
    /// </summary>
    public void CalculateLayout()
    {
        if (_parent is null)
        {
            var screenRect = _gui.ScreenRect;
            if (_hasLayout && !_layoutDirty && _lastLayoutScreenRect == screenRect) return;

            PrepareIntrinsicMeasurements(Vector2.Zero);
            _lastLayoutScreenRect = screenRect;
        }

        if (_parent == null)
            InitializeRootLayout();
        else
            InitializeChildLayout();

        if (_parent is null)
        {
            _layoutDirty = false;
            _hasLayout = true;
            LayoutVersion++;
        }
    }

    void PrepareIntrinsicMeasurements(Vector2 inheritedScrollOffset)
    {
        _ancestorScrollOffset = Style.IsAbsolute ? Vector2.Zero : inheritedScrollOffset;
        var childScrollOffset = Style.IsAbsolute ? Vector2.Zero : inheritedScrollOffset;
        if (!Style.IsAbsolute && Scope.HasLocal<LayoutNodeScopeLocalScrollOffset>())
            childScrollOffset += Scope.Get<LayoutNodeScopeLocalScrollOffset>().Value;

        foreach (var child in FlowChildren) child.PrepareIntrinsicMeasurements(childScrollOffset);

        var horizontalPadding = Style.PaddingLeft + Style.PaddingRight;
        var verticalPadding = Style.PaddingTop + Style.PaddingBottom;
        _intrinsicWidthValid = !Style.Wrap || Style.Direction != Axis.Horizontal;
        _intrinsicHeightValid = !Style.Wrap;

        if (FlowChildren.Count == 0)
        {
            _intrinsicContentWidth = 10f + horizontalPadding;
            _intrinsicContentHeight = 10f + verticalPadding;
            return;
        }

        var width = Style.Direction == Axis.Horizontal ? (FlowChildren.Count - 1) * Style.Gap : 0f;
        var height = Style.Direction == Axis.Vertical ? (FlowChildren.Count - 1) * Style.Gap : 0f;
        foreach (var child in FlowChildren)
        {
            _intrinsicWidthValid &= ChildWidthIsIntrinsic(child);
            _intrinsicHeightValid &= ChildHeightIsIntrinsic(child);

            var childWidth = child.Style.Width >= 0f ? child.Style.Width : child._intrinsicContentWidth;
            var childHeight = child.Style.Height >= 0f ? child.Style.Height : child._intrinsicContentHeight;
            childWidth += child.Style.MarginLeft + child.Style.MarginRight;
            childHeight += child.Style.MarginTop + child.Style.MarginBottom;

            if (Style.Direction == Axis.Horizontal) width += childWidth;
            else width = Math.Max(width, childWidth);

            if (Style.Direction == Axis.Vertical) height += childHeight;
            else height = Math.Max(height, childHeight);
        }

        _intrinsicContentWidth = width + horizontalPadding;
        _intrinsicContentHeight = height + verticalPadding;
    }

    static bool ChildWidthIsIntrinsic(LayoutNode child)
    {
        ref readonly var style = ref child.Style;
        return !style.ExpandWidth && !style.IsExpanded && style.WidthPercent < 0f
            && !HasNonPixelContribution(style.WidthExpression)
            && style.MinWidthExpression is null && style.MaxWidthExpression is null
            && (style.Width >= 0f || child._intrinsicWidthValid);
    }

    static bool ChildHeightIsIntrinsic(LayoutNode child)
    {
        ref readonly var style = ref child.Style;
        return !style.ExpandHeight && !style.IsExpanded && style.HeightPercent < 0f
            && !HasNonPixelContribution(style.HeightExpression)
            && style.MinHeightExpression is null && style.MaxHeightExpression is null
            && (style.Height >= 0f || child._intrinsicHeightValid);
    }

    static bool HasNonPixelContribution(UnitValue? expression) => expression is { } value
        && (value.PercentageContribution != 0f
            || value.ExpandContribution != 0f
            || value.RatioContribution != 0f
            || value.FitContentContribution != 0f
            || value.FitLargestContribution != 0f);

    void InitializeRootLayout()
    {
        _rect = _gui.ScreenRect;
        if (ChildNodes.Count > 0) LayoutChildren();
    }

    /// <summary>
    /// Applies cumulative scroll offset to this node's position
    /// </summary>
    public void ApplyScrollOffset()
    {
        // Don't apply scroll offset to the scrollable container itself
        // Only apply to child nodes within scrollable containers
        if (Style.IsAbsolute) return;

        if (_ancestorScrollOffset != Vector2.Zero)
            // Apply the scroll offset to the node's position
            _rect = new Rect(
                _rect.X - _ancestorScrollOffset.X,
                _rect.Y - _ancestorScrollOffset.Y,
                _rect.W,
                _rect.H
            );
    }

    // Width is resolved before height; a wrapped horizontal container needs it to know how many
    // lines its children break into when computing its own content height.
    float _resolvedWidthForHeightPass;

    void InitializeChildLayout()
    {
        var parentInner = _parent!.InnerRect;
        var myWidth = CalculateWidth(parentInner.W);
        _resolvedWidthForHeightPass = myWidth;
        var myHeight = CalculateHeight(parentInner.H);
        _rect = new Rect(0, 0, myWidth, myHeight);
    }

    float CalculateWidth(float availableWidth)
    {
        var width = Style.WidthExpression is { } expression
            ? ResolveWidth(expression, availableWidth, Math.Max(0f, _rect.H), availableWidth)
            : Style.ExpandWidth || Style.IsExpanded
            ? availableWidth * Style.ExpandWidthPercentage
            : Style.WidthPercent >= 0f
                ? availableWidth * Style.WidthPercent
                : Style.Width >= 0
                    ? Style.Width
                    // A wrapping row fills the available width (like a block-level flex container),
                    // so its children have a width to wrap against instead of sizing to their sum.
                    : Style.Wrap && Style.Direction == Axis.Horizontal
                        ? availableWidth
                        : CalculateContentWidth(availableWidth);
        return ClampResolvedWidth(width, availableWidth, Math.Max(0f, _rect.H), availableWidth);
    }

    float CalculateHeight(float availableHeight)
    {
        var height = Style.HeightExpression is { } expression
            ? ResolveHeight(expression, availableHeight, Math.Max(0f, _resolvedWidthForHeightPass), availableHeight)
            : Style.ExpandHeight || Style.IsExpanded
            ? availableHeight * Style.ExpandHeightPercentage
            : Style.HeightPercent >= 0f
                ? availableHeight * Style.HeightPercent
                : Style.Height >= 0
                    ? Style.Height
                    : CalculateContentHeight(availableHeight);
        return ClampResolvedHeight(height, availableHeight, Math.Max(0f, _resolvedWidthForHeightPass),
            availableHeight);
    }

    float ClampResolvedWidth(float width, float available, float perpendicular, float expandShare)
    {
        if (Style.MinWidthExpression is null && Style.MaxWidthExpression is null)
            return Style.ClampWidth(width);
        var min = Style.MinWidthExpression is { } minExpression
            ? Math.Max(0f, ResolveWidth(minExpression, available, perpendicular, expandShare)) : Style.MinWidth;
        var max = Style.MaxWidthExpression is { } maxExpression
            ? Math.Max(0f, ResolveWidth(maxExpression, available, perpendicular, expandShare)) : Style.MaxWidth;
        if (!float.IsFinite(min) || !float.IsFinite(max))
            throw new InvalidOperationException("Width constraint resolved to a non-finite value.");
        if (min >= 0f) width = Math.Max(width, min);
        // A minimum wins when the bounds conflict.
        if (max >= 0f) width = Math.Min(width, Math.Max(max, min));
        return width;
    }

    float ClampResolvedHeight(float height, float available, float perpendicular, float expandShare)
    {
        if (Style.MinHeightExpression is null && Style.MaxHeightExpression is null)
            return Style.ClampHeight(height);
        var min = Style.MinHeightExpression is { } minExpression
            ? Math.Max(0f, ResolveHeight(minExpression, available, perpendicular, expandShare)) : Style.MinHeight;
        var max = Style.MaxHeightExpression is { } maxExpression
            ? Math.Max(0f, ResolveHeight(maxExpression, available, perpendicular, expandShare)) : Style.MaxHeight;
        if (!float.IsFinite(min) || !float.IsFinite(max))
            throw new InvalidOperationException("Height constraint resolved to a non-finite value.");
        if (min >= 0f) height = Math.Max(height, min);
        if (max >= 0f) height = Math.Min(height, Math.Max(max, min));
        return height;
    }

    float ResolveWidth(UnitValue value, float available, float perpendicular, float expandShare = 0f) =>
        value.PixelsContribution
        + available * value.PercentageContribution
        + perpendicular * value.RatioContribution
        + CalculateContentWidth(available) * value.FitContentContribution
        + CalculateLargestChildWidth(available) * value.FitLargestContribution
        + expandShare * value.ExpandContribution;

    float ResolveHeight(UnitValue value, float available, float perpendicular, float expandShare = 0f) =>
        value.PixelsContribution
        + available * value.PercentageContribution
        + perpendicular * value.RatioContribution
        + CalculateContentHeight(available, perpendicular) * value.FitContentContribution
        + CalculateLargestChildHeight(available) * value.FitLargestContribution
        + expandShare * value.ExpandContribution;

    float CalculateLargestChildWidth(float availableWidth)
    {
        var largest = 0f;
        foreach (var child in FlowChildren)
            largest = Math.Max(largest, GetChildTotalWidth(child, availableWidth));
        return largest + Style.PaddingLeft + Style.PaddingRight;
    }

    float CalculateLargestChildHeight(float availableHeight)
    {
        var largest = 0f;
        foreach (var child in FlowChildren)
            largest = Math.Max(largest, GetChildTotalHeight(child, availableHeight));
        return largest + Style.PaddingTop + Style.PaddingBottom;
    }

    float CalculateContentWidth(float availableWidth)
    {
        if (_intrinsicWidthValid) return _intrinsicContentWidth;

        var padding = Style.PaddingLeft + Style.PaddingRight;

        if (FlowChildren.Count == 0)
            return (_rect.W > 0 ? _rect.W : 10f) + padding;

        var inner = Math.Max(0f, availableWidth - padding);
        var content = Style.Direction == Axis.Horizontal
            ? CalculateHorizontalContentWidth(inner)
            : CalculateVerticalContentWidth(inner);
        return content + padding;
    }

    float CalculateHorizontalContentWidth(float availableWidth)
    {
        var totalGap = (FlowChildren.Count - 1) * Style.Gap;
        var total = totalGap;
        foreach (var child in FlowChildren) total += GetChildTotalWidth(child, availableWidth);
        return total;
    }

    float CalculateVerticalContentWidth(float availableWidth)
    {
        var largest = 10f;
        foreach (var child in FlowChildren) largest = Math.Max(largest, GetChildTotalWidth(child, availableWidth));
        return largest;
    }

    float GetChildTotalWidth(LayoutNode child, float availableWidth)
    {
        var marginWidth = child.Style.MarginLeft + child.Style.MarginRight;
        var contentWidth = child.Style.WidthExpression is { } expression
            ? child.ResolveWidth(expression, availableWidth, Math.Max(0f, child.Rect.H))
            : child.Style.Width >= 0
            ? child.Style.Width
            : Math.Max(child.CalculateContentWidth(availableWidth), 10f);

        return marginWidth + child.ClampResolvedWidth(contentWidth, availableWidth,
            Math.Max(0f, child.Rect.H), availableWidth);
    }

    float CalculateContentHeight(float availableHeight, float outerWidthForWrap = -1f)
    {
        if (_intrinsicHeightValid) return _intrinsicContentHeight;

        var padding = Style.PaddingTop + Style.PaddingBottom;

        if (FlowChildren.Count == 0)
            return (_rect.H > 0 ? _rect.H : 10f) + padding;

        var inner = Math.Max(0f, availableHeight - padding);
        float content;
        if (Style.Direction == Axis.Vertical)
        {
            content = CalculateVerticalContentHeight(inner);
        }
        else if (Style.Wrap)
        {
            var hpad = Style.PaddingLeft + Style.PaddingRight;
            var outer = outerWidthForWrap >= 0f ? outerWidthForWrap : _resolvedWidthForHeightPass;
            content = CalculateWrappedContentHeight(Math.Max(0f, outer - hpad));
        }
        else
        {
            content = CalculateHorizontalContentHeight(inner);
        }

        return content + padding;
    }

    float CalculateVerticalContentHeight(float availableHeight)
    {
        var totalGap = (FlowChildren.Count - 1) * Style.Gap;
        var total = totalGap;
        foreach (var child in FlowChildren) total += GetChildTotalHeight(child, availableHeight);
        return total;
    }

    float CalculateHorizontalContentHeight(float availableHeight)
    {
        var largest = 10f;
        foreach (var child in FlowChildren) largest = Math.Max(largest, GetChildTotalHeight(child, availableHeight));
        return largest;
    }

    float GetChildTotalHeight(LayoutNode child, float availableHeight)
    {
        var marginHeight = child.Style.MarginTop + child.Style.MarginBottom;
        var contentHeight = child.Style.HeightExpression is { } expression
            ? child.ResolveHeight(expression, availableHeight, Math.Max(0f, child.Rect.W))
            : child.Style.Height >= 0
            ? child.Style.Height
            : Math.Max(child.CalculateContentHeight(availableHeight,
                Math.Max(0f, _resolvedWidthForHeightPass - Style.PaddingLeft - Style.PaddingRight)
                - child.Style.MarginLeft - child.Style.MarginRight), 10f);

        return marginHeight + child.ClampResolvedHeight(contentHeight, availableHeight,
            Math.Max(0f, child.Rect.W), availableHeight);
    }

    void LayoutChildren()
    {
        if (ChildNodes.Count == 0) return;

        var contentRect = InnerRect;

        // Resolve percentage sizes against this node's content box into concrete pixels, so the
        // rest of the flow treats them like any explicitly-sized child.
        if (Style.Direction == Axis.Vertical)
            LayoutChildrenVertically(contentRect);
        else
            LayoutChildrenHorizontally(contentRect);

        PositionAbsoluteChildren(contentRect);
    }

    /// <summary>
    /// Places the children that opted out of the flow. Their size resolves against the box they are
    /// positioned in — the parent's content box, or the screen for <see cref="AbsoluteOrigin.Screen"/>
    /// — so Expand() and the percentage sizes keep working on an overlay.
    /// </summary>
    void PositionAbsoluteChildren(Rect contentRect)
    {
        if (_absoluteChildCount == 0) return;

        foreach (var child in ChildNodes)
        {
            if (!child.Style.IsAbsolute) continue;

            var isScreen = child.Style.AbsoluteOrigin == AbsoluteOrigin.Screen;
            var box = isScreen ? _gui.ScreenRect : contentRect;

            var width = child.CalculateWidth(box.W);
            var height = child.CalculateHeight(box.H);
            var origin = isScreen ? Vector2.Zero : new Vector2(box.X, box.Y);

            child._rect = new Rect(
                origin.X + child.Style.AbsolutePosition.X,
                origin.Y + child.Style.AbsolutePosition.Y,
                width,
                height);

            if (child.ChildNodes.Count > 0) child.LayoutChildren();
        }
    }

    void LayoutChildrenVertically(Rect contentRect)
    {
        if (FlowChildren.Count == 0) return;

        var layoutContext = CreateVerticalLayoutContext(contentRect);
        if (FlowChildren.Count == 1)
        {
            LayoutSingleChildVertically(contentRect, layoutContext);
            return;
        }

        var childDimensions = CalculateVerticalChildDimensions(layoutContext);
        try
        {
            PositionChildrenVertically(contentRect, childDimensions, layoutContext);
        }
        finally
        {
            ArrayPool<ChildDimensions>.Shared.Return(childDimensions);
        }
    }

    void LayoutSingleChildVertically(Rect contentRect, VerticalLayoutContext context)
    {
        var child = FlowChildren[0];
        var marginHeight = child.Style.MarginTop + child.Style.MarginBottom;
        var fixedHeight = GetChildFixedHeight(child, context);
        var remainingHeight = Math.Max(0f, context.AvailableHeight - fixedHeight);
        var childHeight = CalculateChildHeight(child, context, remainingHeight, 30f);
        if (child.Style.Height < 0f)
            childHeight = Math.Max(childHeight, context.AvailableHeight - marginHeight > 0f ? 10f : 0f);
        childHeight = child.ClampResolvedHeight(childHeight, context.AvailableHeight, context.ContentWidth,
            remainingHeight);

        var totalHeight = childHeight + marginHeight;
        var extraSpaceY = Math.Max(0f, context.AvailableHeight - totalHeight);
        var currentY = contentRect.Y + extraSpaceY * Style.AlignContentVertical + child.Style.MarginTop;
        child._rect = CalculateVerticalChildRect(child, contentRect, currentY, childHeight);
        if (child.ChildNodes.Count > 0) child.LayoutChildren();
        child.ApplyScrollOffset();
    }

    VerticalLayoutContext CreateVerticalLayoutContext(Rect contentRect)
    {
        var totalGap = (FlowChildren.Count - 1) * Style.Gap;
        var availableHeight = contentRect.H - totalGap;
        var expandingCount = 0;
        var totalExpand = 0f;
        foreach (var child in FlowChildren)
        {
            if (!child.Style.ExpandHeight && !child.Style.IsExpanded) continue;
            expandingCount++;
            totalExpand += child.Style.ExpandHeightPercentage;
        }

        return new VerticalLayoutContext
        {
            AvailableHeight = availableHeight,
            ContentWidth = contentRect.W,
            TotalGap = totalGap,
            ExpandingCount = expandingCount,
            TotalExpandPercentage = totalExpand <= 0f && expandingCount > 0 ? expandingCount : totalExpand
        };
    }

    ChildDimensions[] CalculateVerticalChildDimensions(VerticalLayoutContext context)
    {
        const float defaultChildHeight = 30f;
        var fixedHeight = CalculateFixedHeight(context);
        var remainingHeight = Math.Max(0, context.AvailableHeight - fixedHeight);
        var dimensions = ArrayPool<ChildDimensions>.Shared.Rent(FlowChildren.Count);

        for (var i = 0; i < FlowChildren.Count; i++)
        {
            var child = FlowChildren[i];
            var marginHeight = child.Style.MarginTop + child.Style.MarginBottom;

            var childHeight = CalculateChildHeight(child, context, remainingHeight, defaultChildHeight);
            // The 10px floor keeps an unsized child visible; a child that asked for a size gets it,
            // so a 6px splitter stays 6px.
            if (child.Style.Height < 0)
                childHeight = Math.Max(childHeight, context.AvailableHeight - marginHeight > 0 ? 10f : 0f);
            childHeight = child.ClampResolvedHeight(childHeight, context.AvailableHeight, context.ContentWidth,
                remainingHeight);

            dimensions[i] = new ChildDimensions { Height = childHeight };
        }

        return dimensions;
    }

    float CalculateFixedHeight(VerticalLayoutContext context)
    {
        var total = 0f;
        foreach (var child in FlowChildren) total += GetChildFixedHeight(child, context);
        return total;
    }

    float GetChildFixedHeight(LayoutNode child, VerticalLayoutContext context)
    {
        var marginHeight = child.Style.MarginTop + child.Style.MarginBottom;

        if (child.Style.HeightExpression is { } expression)
            return marginHeight + child.ResolveHeight(expression, context.AvailableHeight, context.ContentWidth);

        if (child.Style.ExpandHeight || child.Style.IsExpanded)
            return marginHeight;

        var contentHeight = child.Style.HeightPercent >= 0f
                ? context.AvailableHeight * child.Style.HeightPercent
            : child.Style.Height >= 0
            ? child.Style.Height
            : child.ChildNodes.Count == 0 && child.Rect.H > 0
                ? child.Rect.H
                : child.CalculateContentHeight(context.AvailableHeight,
                    context.ContentWidth - child.Style.MarginLeft - child.Style.MarginRight);

        return marginHeight + contentHeight;
    }

    float CalculateChildHeight(LayoutNode child, VerticalLayoutContext context, float remainingHeight,
        float defaultHeight)
    {
        if (child.Style.ExpandHeight || child.Style.IsExpanded)
            return CalculateExpandingChildHeight(child, context, remainingHeight, defaultHeight);

        if (child.Style.HeightExpression is { } expression)
        {
            var share = expression.ExpandContribution != 0f && context.TotalExpandPercentage > 0f
                ? remainingHeight / context.TotalExpandPercentage
                : 0f;
            return child.ResolveHeight(expression, context.AvailableHeight, context.ContentWidth, share);
        }

        if (child.Style.HeightPercent >= 0f)
            return context.AvailableHeight * child.Style.HeightPercent;

        if (child.Style.Height >= 0)
            return child.Style.Height;

        if (child.ChildNodes.Count == 0 && child.Rect.H > 0)
            return child.Rect.H;

        return child.CalculateContentHeight(context.AvailableHeight,
            context.ContentWidth - child.Style.MarginLeft - child.Style.MarginRight);
    }

    float CalculateExpandingChildHeight(LayoutNode child, VerticalLayoutContext context, float remainingHeight,
        float defaultHeight)
    {
        if (context.ExpandingCount == 1 && FlowChildren.Count == 1)
        {
            var availableHeightForChild = Math.Max(0,
                context.AvailableHeight - child.Style.MarginTop - child.Style.MarginBottom);
            return availableHeightForChild * child.Style.ExpandHeightPercentage;
        }

        if (context.ExpandingCount > 0)
        {
            var expandRatio = child.Style.ExpandHeightPercentage / context.TotalExpandPercentage;
            var height = remainingHeight * expandRatio;

            // Apply constraint for horizontal containers
            if (child.Style is { Direction: Axis.Horizontal, Height: < 0, ExpandHeight: false, IsExpanded: false })
                height = Math.Min(height, 120f);

            return height;
        }

        return defaultHeight;
    }

    void PositionChildrenVertically(Rect contentRect, ChildDimensions[] childDimensions,
        VerticalLayoutContext context)
    {
        var totalActualHeight = context.TotalGap;
        for (var i = 0; i < FlowChildren.Count; i++)
            totalActualHeight += childDimensions[i].Height + FlowChildren[i].Style.MarginTop
                                                       + FlowChildren[i].Style.MarginBottom;

        var extraSpaceY = Math.Max(0, context.AvailableHeight - totalActualHeight);
        var alignmentOffsetY = extraSpaceY * Style.AlignContentVertical;
        var currentY = contentRect.Y + alignmentOffsetY;

        for (var i = 0; i < FlowChildren.Count; i++)
        {
            var child = FlowChildren[i];
            currentY += child.Style.MarginTop;

            var childRect = CalculateVerticalChildRect(child, contentRect, currentY, childDimensions[i].Height);
            child._rect = childRect;

            if (child.ChildNodes.Count > 0) child.LayoutChildren();

            // Apply scroll offset after positioning
            child.ApplyScrollOffset();

            currentY += childDimensions[i].Height + child.Style.MarginBottom + Style.Gap;
        }
    }

    Rect CalculateVerticalChildRect(LayoutNode child, Rect contentRect, float currentY, float childHeight)
    {
        var availableChildWidth = Math.Max(0, contentRect.W - child.Style.MarginLeft - child.Style.MarginRight);

        var childWidth = child.Style.WidthExpression is { } expression
            ? child.ResolveWidth(expression, availableChildWidth, childHeight, availableChildWidth)
            : child.Style.WidthPercent >= 0f
                ? availableChildWidth * child.Style.WidthPercent
            : child.Style.ExpandWidth || child.Style.IsExpanded
            ? availableChildWidth * child.Style.ExpandWidthPercentage
            : child.Style.Width >= 0
                ? child.Style.Width
                : Math.Max(availableChildWidth, 0);
        childWidth = child.ClampResolvedWidth(childWidth, availableChildWidth, childHeight, availableChildWidth);

        var extraSpaceX = Math.Max(0, contentRect.W - childWidth - child.Style.MarginLeft - child.Style.MarginRight);
        var alignmentOffsetX = extraSpaceX * Style.AlignContentHorizontal;

        return new Rect(
            contentRect.X + child.Style.MarginLeft + alignmentOffsetX,
            currentY,
            childWidth,
            childHeight
        );
    }

    void LayoutChildrenHorizontally(Rect contentRect)
    {
        if (FlowChildren.Count == 0) return;

        if (Style.Wrap)
        {
            LayoutChildrenHorizontallyWrapped(contentRect);
            return;
        }

        var layoutContext = CreateHorizontalLayoutContext(contentRect);
        var childDimensions = CalculateHorizontalChildDimensions(layoutContext);
        try
        {
            PositionChildrenHorizontally(contentRect, childDimensions, layoutContext);
        }
        finally
        {
            ArrayPool<ChildDimensions>.Shared.Return(childDimensions);
        }
    }

    /// <summary>
    /// Flows children left-to-right, breaking to a new line when the next child (plus gap) would
    /// overflow the content width. Line height is the tallest child on that line; lines stack with
    /// the same gap. Wrapped children keep their natural size — no expand on the cross axis.
    /// </summary>
    void LayoutChildrenHorizontallyWrapped(Rect contentRect)
    {
        var currentY = contentRect.Y;
        var start = 0;
        while (start < FlowChildren.Count)
        {
            var end = FindWrapLineEnd(start, contentRect.W);
            var lineHeight = 0f;
            for (var i = start; i < end; i++)
                lineHeight = Math.Max(lineHeight, NaturalChildHeight(FlowChildren[i], contentRect.W));

            var currentX = contentRect.X;
            for (var i = start; i < end; i++)
            {
                var child = FlowChildren[i];
                currentX += child.Style.MarginLeft;

                var w = NaturalChildWidth(child, contentRect.W);
                var h = child.Style.HeightExpression is { } heightExpression
                    ? child.ResolveHeight(heightExpression, lineHeight, w)
                    : child.Style.Height >= 0 ? child.Style.Height : lineHeight;
                h = child.ClampResolvedHeight(h, lineHeight, w, lineHeight);

                var alignOffsetY = Math.Max(0f, lineHeight - h) * Style.AlignContentVertical;
                child._rect = new Rect(currentX, currentY + child.Style.MarginTop + alignOffsetY, w, h);

                if (child.ChildNodes.Count > 0) child.LayoutChildren();
                child.ApplyScrollOffset();

                currentX += w + child.Style.MarginRight + Style.Gap;
            }

            currentY += lineHeight + Style.Gap;
            start = end;
        }
    }

    int FindWrapLineEnd(int start, float availableWidth)
    {
        var lineWidth = 0f;
        for (var i = start; i < FlowChildren.Count; i++)
        {
            var child = FlowChildren[i];
            var w = NaturalChildWidth(child, availableWidth) + child.Style.MarginLeft + child.Style.MarginRight;
            var withGap = i == start ? w : lineWidth + Style.Gap + w;
            if (i > start && withGap > availableWidth) return i;
            lineWidth = withGap;
        }
        return FlowChildren.Count;
    }

    static float NaturalChildWidth(LayoutNode child, float availableWidth)
    {
        var width = child.Style.WidthExpression is { } expression
            ? child.ResolveWidth(expression, availableWidth, Math.Max(0f, child.Rect.H))
            : child.Style.Width >= 0
            ? child.Style.Width
            : child.Style.WidthPercent >= 0f
                ? availableWidth * child.Style.WidthPercent
                : Math.Max(child.CalculateContentWidth(availableWidth), 10f);
        return child.ClampResolvedWidth(width, availableWidth, Math.Max(0f, child.Rect.H), availableWidth);
    }

    static float NaturalChildHeight(LayoutNode child, float availableWidth)
    {
        var height = child.Style.HeightExpression is { } expression
            ? child.ResolveHeight(expression, availableWidth, Math.Max(0f, child.Rect.W))
            : child.Style.Height >= 0
            ? child.Style.Height
            : Math.Max(child.CalculateContentHeight(availableWidth, availableWidth), 10f);
        return child.ClampResolvedHeight(height, availableWidth, Math.Max(0f, child.Rect.W), availableWidth);
    }

    /// <summary>Cross-axis size of a wrapped horizontal container: stacked line heights plus gaps.</summary>
    float CalculateWrappedContentHeight(float availableWidth)
    {
        var total = 0f;
        var lineCount = 0;
        var start = 0;
        while (start < FlowChildren.Count)
        {
            var end = FindWrapLineEnd(start, availableWidth);
            var lineHeight = 0f;
            for (var i = start; i < end; i++)
                lineHeight = Math.Max(lineHeight, NaturalChildHeight(FlowChildren[i], availableWidth));
            total += lineHeight;
            lineCount++;
            start = end;
        }
        return total + Math.Max(0, lineCount - 1) * Style.Gap;
    }

    HorizontalLayoutContext CreateHorizontalLayoutContext(Rect contentRect)
    {
        var totalGap = (FlowChildren.Count - 1) * Style.Gap;
        var availableWidth = contentRect.W - totalGap;
        var expandingCount = 0;
        var totalExpand = 0f;
        foreach (var child in FlowChildren)
        {
            if (!child.Style.ExpandWidth && !child.Style.IsExpanded) continue;
            expandingCount++;
            totalExpand += child.Style.ExpandWidthPercentage;
        }

        return new HorizontalLayoutContext
        {
            AvailableWidth = availableWidth,
            TotalGap = totalGap,
            ExpandingCount = expandingCount,
            TotalExpandPercentage = totalExpand <= 0f && expandingCount > 0 ? expandingCount : totalExpand
        };
    }

    ChildDimensions[] CalculateHorizontalChildDimensions(HorizontalLayoutContext context)
    {
        const float defaultChildWidth = 80f;
        var fixedWidth = CalculateFixedWidth(context);
        var remainingWidth = Math.Max(0, context.AvailableWidth - fixedWidth);
        var dimensions = ArrayPool<ChildDimensions>.Shared.Rent(FlowChildren.Count);

        for (var i = 0; i < FlowChildren.Count; i++)
        {
            var child = FlowChildren[i];
            var childWidth = CalculateChildWidth(child, context, remainingWidth, defaultChildWidth);
            var availableChildWidthForMin = Math.Max(0,
                context.AvailableWidth - child.Style.MarginLeft - child.Style.MarginRight);
            if (child.Style.Width < 0)
                childWidth = Math.Max(childWidth, availableChildWidthForMin > 0 ? 10f : 0f);
            childWidth = child.ClampResolvedWidth(childWidth, context.AvailableWidth, Math.Max(0f, child.Rect.H),
                remainingWidth);

            dimensions[i] = new ChildDimensions { Width = childWidth };
        }

        return dimensions;
    }

    float CalculateFixedWidth(HorizontalLayoutContext context)
    {
        var total = 0f;
        foreach (var child in FlowChildren) total += GetChildFixedWidth(child, context);
        return total;
    }

    float GetChildFixedWidth(LayoutNode child, HorizontalLayoutContext context)
    {
        var marginWidth = child.Style.MarginLeft + child.Style.MarginRight;

        if (child.Style.WidthExpression is { } expression)
            return marginWidth + child.ResolveWidth(expression, context.AvailableWidth, Math.Max(0f, child.Rect.H));

        if (child.Style.ExpandWidth || child.Style.IsExpanded)
            return marginWidth;

        var contentWidth = child.Style.WidthPercent >= 0f
                ? context.AvailableWidth * child.Style.WidthPercent
            : child.Style.Width >= 0
            ? child.Style.Width
            : child.CalculateContentWidth(context.AvailableWidth);

        return marginWidth + contentWidth;
    }

    float CalculateChildWidth(LayoutNode child, HorizontalLayoutContext context, float remainingWidth,
        float defaultWidth)
    {
        if (child.Style.ExpandWidth || child.Style.IsExpanded)
            return CalculateExpandingChildWidth(child, context, remainingWidth, defaultWidth);

        if (child.Style.WidthExpression is { } expression)
        {
            var share = expression.ExpandContribution != 0f && context.TotalExpandPercentage > 0f
                ? remainingWidth / context.TotalExpandPercentage
                : 0f;
            return child.ResolveWidth(expression, context.AvailableWidth, Math.Max(0f, child.Rect.H), share);
        }

        if (child.Style.WidthPercent >= 0f)
            return context.AvailableWidth * child.Style.WidthPercent;

        if (child.Style.Width >= 0)
            return child.Style.Width;

        return child.CalculateContentWidth(context.AvailableWidth);
    }

    float CalculateExpandingChildWidth(LayoutNode child, HorizontalLayoutContext context, float remainingWidth,
        float defaultWidth)
    {
        if (context.ExpandingCount == 1 && FlowChildren.Count == 1)
        {
            var availableWidthForChild =
                Math.Max(0, context.AvailableWidth - child.Style.MarginLeft - child.Style.MarginRight);
            return availableWidthForChild * child.Style.ExpandWidthPercentage;
        }

        if (context.ExpandingCount > 0)
        {
            var expandRatio = child.Style.ExpandWidthPercentage / context.TotalExpandPercentage;
            var width = remainingWidth * expandRatio;

            // Apply constraint for vertical containers
            if (child.Style is { Direction: Axis.Vertical, Width: < 0, ExpandWidth: false, IsExpanded: false })
                width = Math.Min(width, 240f);

            return width;
        }

        return defaultWidth;
    }

    void PositionChildrenHorizontally(Rect contentRect, ChildDimensions[] childDimensions,
        HorizontalLayoutContext context)
    {
        var totalActualWidth = context.TotalGap;
        for (var i = 0; i < FlowChildren.Count; i++)
            totalActualWidth += childDimensions[i].Width + FlowChildren[i].Style.MarginLeft
                                                     + FlowChildren[i].Style.MarginRight;

        var extraSpaceX = Math.Max(0, context.AvailableWidth - totalActualWidth);
        var alignmentOffsetX = extraSpaceX * Style.AlignContentHorizontal;
        var currentX = contentRect.X + alignmentOffsetX;

        for (var i = 0; i < FlowChildren.Count; i++)
        {
            var child = FlowChildren[i];
            currentX += child.Style.MarginLeft;

            var childRect = CalculateHorizontalChildRect(child, contentRect, currentX, childDimensions[i].Width);
            child._rect = childRect;

            if (child.ChildNodes.Count > 0) child.LayoutChildren();

            // Apply scroll offset after positioning
            child.ApplyScrollOffset();

            currentX += childDimensions[i].Width + child.Style.MarginRight + Style.Gap;
        }
    }

    Rect CalculateHorizontalChildRect(LayoutNode child, Rect contentRect, float currentX, float childWidth)
    {
        var availableChildHeight = contentRect.H - child.Style.MarginTop - child.Style.MarginBottom;
        var childHeight = child.Style.HeightExpression is { } expression
            ? child.ResolveHeight(expression, availableChildHeight, childWidth)
            : child.Style.HeightPercent >= 0f
                ? availableChildHeight * child.Style.HeightPercent
            : child.Style.Height >= 0
            ? child.Style.Height
            : Math.Max(0, availableChildHeight);
        childHeight = child.ClampResolvedHeight(childHeight, availableChildHeight, childWidth, availableChildHeight);

        var extraSpaceY = Math.Max(0, contentRect.H - childHeight - child.Style.MarginTop - child.Style.MarginBottom);
        var alignmentOffsetY = extraSpaceY * Style.AlignContentVertical;

        return new Rect(
            currentX,
            contentRect.Y + child.Style.MarginTop + alignmentOffsetY,
            childWidth,
            childHeight
        );
    }

    struct VerticalLayoutContext
    {
        public float AvailableHeight { get; set; }
        public float ContentWidth { get; set; }
        public float TotalGap { get; set; }
        public int ExpandingCount { get; set; }
        public float TotalExpandPercentage { get; set; }
    }

    struct HorizontalLayoutContext
    {
        public float AvailableWidth { get; set; }
        public float TotalGap { get; set; }
        public int ExpandingCount { get; set; }
        public float TotalExpandPercentage { get; set; }
    }

    struct ChildDimensions
    {
        public float Width { get; set; }
        public float Height { get; set; }
    }
}
