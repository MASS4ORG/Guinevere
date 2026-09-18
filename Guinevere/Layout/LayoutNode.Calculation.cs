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
        if (_parent == null)
            InitializeRootLayout();
        else
            InitializeChildLayout();
    }

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
        var scrollOffset = Vector2.Zero;

        // Look for scroll offset from parent containers. An absolute node is anchored to a box
        // rather than carried by the flow, so the walk stops at the first absolute ancestor.
        if (Style.IsAbsolute) return;

        var currentScope = _parent?.Scope;
        while (currentScope != null)
        {
            if (currentScope.Node.Style.IsAbsolute) break;

            // Only scopes that hold an offset themselves: Get() inherits from ancestors, so counting
            // it at every level shifted deep content once per level instead of once.
            if (currentScope.HasLocal<LayoutNodeScopeLocalScrollOffset>())
                scrollOffset += currentScope.Get<LayoutNodeScopeLocalScrollOffset>().Value;

            currentScope = currentScope.Node.Parent?.Scope;
        }

        if (scrollOffset != Vector2.Zero)
            // Apply the scroll offset to the node's position
            _rect = new Rect(
                _rect.X - scrollOffset.X,
                _rect.Y - scrollOffset.Y,
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
        var width = Style.ExpandWidth || Style.IsExpanded
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
        return Style.ClampWidth(width);
    }

    float CalculateHeight(float availableHeight)
    {
        var height = Style.ExpandHeight || Style.IsExpanded
            ? availableHeight * Style.ExpandHeightPercentage
            : Style.HeightPercent >= 0f
                ? availableHeight * Style.HeightPercent
                : Style.Height >= 0
                    ? Style.Height
                    : CalculateContentHeight(availableHeight);
        return Style.ClampHeight(height);
    }

    float CalculateContentWidth(float availableWidth)
    {
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
        return FlowChildren
            .Select(child => GetChildTotalWidth(child, availableWidth))
            .Sum() + totalGap;
    }

    float CalculateVerticalContentWidth(float availableWidth)
    {
        return FlowChildren
            .Select(child => GetChildTotalWidth(child, availableWidth))
            .DefaultIfEmpty(10f)
            .Max();
    }

    float GetChildTotalWidth(LayoutNode child, float availableWidth)
    {
        var marginWidth = child.Style.MarginLeft + child.Style.MarginRight;
        var contentWidth = child.Style.Width >= 0
            ? child.Style.Width
            : Math.Max(child.CalculateContentWidth(availableWidth), 10f);

        return marginWidth + contentWidth;
    }

    float CalculateContentHeight(float availableHeight, float outerWidthForWrap = -1f)
    {
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
        return FlowChildren
            .Select(child => GetChildTotalHeight(child, availableHeight))
            .Sum() + totalGap;
    }

    float CalculateHorizontalContentHeight(float availableHeight)
    {
        return FlowChildren
            .Select(child => GetChildTotalHeight(child, availableHeight))
            .DefaultIfEmpty(10f)
            .Max();
    }

    float GetChildTotalHeight(LayoutNode child, float availableHeight)
    {
        var marginHeight = child.Style.MarginTop + child.Style.MarginBottom;
        var contentHeight = child.Style.Height >= 0
            ? child.Style.Height
            : Math.Max(child.CalculateContentHeight(availableHeight,
                Math.Max(0f, _resolvedWidthForHeightPass - Style.PaddingLeft - Style.PaddingRight)
                - child.Style.MarginLeft - child.Style.MarginRight), 10f);

        return marginHeight + contentHeight;
    }

    void LayoutChildren()
    {
        if (ChildNodes.Count == 0) return;

        var contentRect = InnerRect;

        // Resolve percentage sizes against this node's content box into concrete pixels, so the
        // rest of the flow treats them like any explicitly-sized child.
        foreach (var child in FlowChildren)
        {
            if (child.Style.WidthPercent >= 0f)
                child.Style.Width = contentRect.W * child.Style.WidthPercent;
            if (child.Style.HeightPercent >= 0f)
                child.Style.Height = contentRect.H * child.Style.HeightPercent;
        }

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

            if (child.Style.WidthPercent >= 0f) child.Style.Width = box.W * child.Style.WidthPercent;
            if (child.Style.HeightPercent >= 0f) child.Style.Height = box.H * child.Style.HeightPercent;

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
        var childDimensions = CalculateVerticalChildDimensions(layoutContext);
        PositionChildrenVertically(contentRect, childDimensions, layoutContext);
    }

    VerticalLayoutContext CreateVerticalLayoutContext(Rect contentRect)
    {
        var totalGap = (FlowChildren.Count - 1) * Style.Gap;
        var availableHeight = contentRect.H - totalGap;
        var expandingChildren = FlowChildren.Where(c => c.Style.ExpandHeight || c.Style.IsExpanded).ToList();

        return new VerticalLayoutContext
        {
            AvailableHeight = availableHeight,
            ContentWidth = contentRect.W,
            TotalGap = totalGap,
            ExpandingChildren = expandingChildren,
            TotalExpandPercentage =
                CalculateTotalExpandPercentage(expandingChildren, c => c.Style.ExpandHeightPercentage)
        };
    }

    ChildDimensions[] CalculateVerticalChildDimensions(VerticalLayoutContext context)
    {
        const float defaultChildHeight = 30f;
        var fixedHeight = CalculateFixedHeight(context);
        var remainingHeight = Math.Max(0, context.AvailableHeight - fixedHeight);
        var dimensions = new ChildDimensions[FlowChildren.Count];

        for (var i = 0; i < FlowChildren.Count; i++)
        {
            var child = FlowChildren[i];
            var marginHeight = child.Style.MarginTop + child.Style.MarginBottom;

            var childHeight = CalculateChildHeight(child, context, remainingHeight, defaultChildHeight);
            // The 10px floor keeps an unsized child visible; a child that asked for a size gets it,
            // so a 6px splitter stays 6px.
            if (child.Style.Height < 0)
                childHeight = Math.Max(childHeight, context.AvailableHeight - marginHeight > 0 ? 10f : 0f);
            childHeight = child.Style.ClampHeight(childHeight);

            dimensions[i] = new ChildDimensions { Height = childHeight };
        }

        return dimensions;
    }

    float CalculateFixedHeight(VerticalLayoutContext context)
    {
        return FlowChildren
            .Select(child => GetChildFixedHeight(child, context))
            .Sum();
    }

    float GetChildFixedHeight(LayoutNode child, VerticalLayoutContext context)
    {
        var marginHeight = child.Style.MarginTop + child.Style.MarginBottom;

        if (child.Style.ExpandHeight || child.Style.IsExpanded)
            return marginHeight;

        var contentHeight = child.Style.Height >= 0
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
        if (context.ExpandingChildren.Count == 1 && FlowChildren.Count == 1)
        {
            var availableHeightForChild = Math.Max(0,
                context.AvailableHeight - child.Style.MarginTop - child.Style.MarginBottom);
            return availableHeightForChild * child.Style.ExpandHeightPercentage;
        }

        if (context.ExpandingChildren.Count > 0)
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
        var totalActualHeight = childDimensions.Sum(d => d.Height) +
                                FlowChildren.Sum(c => c.Style.MarginTop + c.Style.MarginBottom) +
                                context.TotalGap;

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

        var childWidth = child.Style.ExpandWidth || child.Style.IsExpanded
            ? availableChildWidth * child.Style.ExpandWidthPercentage
            : child.Style.Width >= 0
                ? child.Style.Width
                : Math.Max(availableChildWidth, 0);
        childWidth = child.Style.ClampWidth(childWidth);

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
        PositionChildrenHorizontally(contentRect, childDimensions, layoutContext);
    }

    /// <summary>
    /// Flows children left-to-right, breaking to a new line when the next child (plus gap) would
    /// overflow the content width. Line height is the tallest child on that line; lines stack with
    /// the same gap. Wrapped children keep their natural size — no expand on the cross axis.
    /// </summary>
    void LayoutChildrenHorizontallyWrapped(Rect contentRect)
    {
        var lines = BuildWrapLines(contentRect.W);
        var currentY = contentRect.Y;

        foreach (var line in lines)
        {
            var lineHeight = 0f;
            foreach (var i in line)
                lineHeight = Math.Max(lineHeight, NaturalChildHeight(FlowChildren[i], contentRect.W));

            var currentX = contentRect.X;
            foreach (var i in line)
            {
                var child = FlowChildren[i];
                currentX += child.Style.MarginLeft;

                var w = NaturalChildWidth(child, contentRect.W);
                var h = child.Style.Height >= 0 ? child.Style.Height : lineHeight;
                h = child.Style.ClampHeight(h);

                var alignOffsetY = Math.Max(0f, lineHeight - h) * Style.AlignContentVertical;
                child._rect = new Rect(currentX, currentY + child.Style.MarginTop + alignOffsetY, w, h);

                if (child.ChildNodes.Count > 0) child.LayoutChildren();
                child.ApplyScrollOffset();

                currentX += w + child.Style.MarginRight + Style.Gap;
            }

            currentY += lineHeight + Style.Gap;
        }
    }

    List<List<int>> BuildWrapLines(float availableWidth)
    {
        var lines = new List<List<int>>();
        var line = new List<int>();
        var lineWidth = 0f;

        for (var i = 0; i < FlowChildren.Count; i++)
        {
            var child = FlowChildren[i];
            var w = NaturalChildWidth(child, availableWidth) + child.Style.MarginLeft + child.Style.MarginRight;
            var withGap = line.Count == 0 ? w : lineWidth + Style.Gap + w;

            if (line.Count > 0 && withGap > availableWidth)
            {
                lines.Add(line);
                line = [];
                lineWidth = 0f;
            }

            lineWidth = line.Count == 0 ? w : lineWidth + Style.Gap + w;
            line.Add(i);
        }

        if (line.Count > 0) lines.Add(line);
        return lines;
    }

    static float NaturalChildWidth(LayoutNode child, float availableWidth) =>
        child.Style.Width >= 0
            ? child.Style.Width
            : child.Style.WidthPercent >= 0f
                ? availableWidth * child.Style.WidthPercent
                : Math.Max(child.CalculateContentWidth(availableWidth), 10f);

    static float NaturalChildHeight(LayoutNode child, float availableWidth) =>
        child.Style.Height >= 0
            ? child.Style.Height
            : Math.Max(child.CalculateContentHeight(availableWidth, availableWidth), 10f);

    /// <summary>Cross-axis size of a wrapped horizontal container: stacked line heights plus gaps.</summary>
    float CalculateWrappedContentHeight(float availableWidth)
    {
        var lines = BuildWrapLines(availableWidth);
        if (lines.Count == 0) return 0f;

        var total = (lines.Count - 1) * Style.Gap;
        foreach (var line in lines)
        {
            var lineHeight = 0f;
            foreach (var i in line)
                lineHeight = Math.Max(lineHeight, NaturalChildHeight(FlowChildren[i], availableWidth));
            total += lineHeight;
        }

        return total;
    }

    HorizontalLayoutContext CreateHorizontalLayoutContext(Rect contentRect)
    {
        var totalGap = (FlowChildren.Count - 1) * Style.Gap;
        var availableWidth = contentRect.W - totalGap;
        var expandingChildren = FlowChildren.Where(c => c.Style.ExpandWidth || c.Style.IsExpanded).ToList();

        return new HorizontalLayoutContext
        {
            AvailableWidth = availableWidth,
            TotalGap = totalGap,
            ExpandingChildren = expandingChildren,
            TotalExpandPercentage =
                CalculateTotalExpandPercentage(expandingChildren, c => c.Style.ExpandWidthPercentage)
        };
    }

    ChildDimensions[] CalculateHorizontalChildDimensions(HorizontalLayoutContext context)
    {
        const float defaultChildWidth = 80f;
        var fixedWidth = CalculateFixedWidth(context);
        var remainingWidth = Math.Max(0, context.AvailableWidth - fixedWidth);
        var dimensions = new ChildDimensions[FlowChildren.Count];

        for (var i = 0; i < FlowChildren.Count; i++)
        {
            var child = FlowChildren[i];
            var childWidth = CalculateChildWidth(child, context, remainingWidth, defaultChildWidth);
            var availableChildWidthForMin = Math.Max(0,
                context.AvailableWidth - child.Style.MarginLeft - child.Style.MarginRight);
            if (child.Style.Width < 0)
                childWidth = Math.Max(childWidth, availableChildWidthForMin > 0 ? 10f : 0f);
            childWidth = child.Style.ClampWidth(childWidth);

            dimensions[i] = new ChildDimensions { Width = childWidth };
        }

        return dimensions;
    }

    float CalculateFixedWidth(HorizontalLayoutContext context)
    {
        return FlowChildren
            .Select(child => GetChildFixedWidth(child, context))
            .Sum();
    }

    float GetChildFixedWidth(LayoutNode child, HorizontalLayoutContext context)
    {
        var marginWidth = child.Style.MarginLeft + child.Style.MarginRight;

        if (child.Style.ExpandWidth || child.Style.IsExpanded)
            return marginWidth;

        var contentWidth = child.Style.Width >= 0
            ? child.Style.Width
            : child.CalculateContentWidth(context.AvailableWidth);

        return marginWidth + contentWidth;
    }

    float CalculateChildWidth(LayoutNode child, HorizontalLayoutContext context, float remainingWidth,
        float defaultWidth)
    {
        if (child.Style.ExpandWidth || child.Style.IsExpanded)
            return CalculateExpandingChildWidth(child, context, remainingWidth, defaultWidth);

        if (child.Style.Width >= 0)
            return child.Style.Width;

        return child.CalculateContentWidth(context.AvailableWidth);
    }

    float CalculateExpandingChildWidth(LayoutNode child, HorizontalLayoutContext context, float remainingWidth,
        float defaultWidth)
    {
        if (context.ExpandingChildren.Count == 1 && FlowChildren.Count == 1)
        {
            var availableWidthForChild =
                Math.Max(0, context.AvailableWidth - child.Style.MarginLeft - child.Style.MarginRight);
            return availableWidthForChild * child.Style.ExpandWidthPercentage;
        }

        if (context.ExpandingChildren.Count > 0)
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
        var totalActualWidth = childDimensions.Sum(d => d.Width) +
                               FlowChildren.Sum(c => c.Style.MarginLeft + c.Style.MarginRight) +
                               context.TotalGap;

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
        var childHeight = child.Style.Height >= 0
            ? child.Style.Height
            : Math.Max(0, availableChildHeight);
        childHeight = child.Style.ClampHeight(childHeight);

        var extraSpaceY = Math.Max(0, contentRect.H - childHeight - child.Style.MarginTop - child.Style.MarginBottom);
        var alignmentOffsetY = extraSpaceY * Style.AlignContentVertical;

        return new Rect(
            currentX,
            contentRect.Y + child.Style.MarginTop + alignmentOffsetY,
            childWidth,
            childHeight
        );
    }

    float CalculateTotalExpandPercentage<T>(List<T> expandingChildren, Func<T, float> percentageSelector)
    {
        var total = expandingChildren.Sum(percentageSelector);
        return total <= 0 && expandingChildren.Count > 0 ? expandingChildren.Count : total;
    }

    class VerticalLayoutContext
    {
        public float AvailableHeight { get; set; }
        public float ContentWidth { get; set; }
        public float TotalGap { get; set; }
        public List<LayoutNode> ExpandingChildren { get; set; } = [];
        public float TotalExpandPercentage { get; set; }
    }

    class HorizontalLayoutContext
    {
        public float AvailableWidth { get; set; }
        public float TotalGap { get; set; }
        public List<LayoutNode> ExpandingChildren { get; set; } = [];
        public float TotalExpandPercentage { get; set; }
    }

    struct ChildDimensions
    {
        public float Width { get; set; }
        public float Height { get; set; }
    }
}
