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

    private void InitializeRootLayout()
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

        // Look for scroll offset from parent containers
        var currentScope = _parent?.Scope;
        while (currentScope != null)
        {
            var localOffset = currentScope.Get<LayoutNodeScopeLocalScrollOffset>().Value;
            if (localOffset != Vector2.Zero) scrollOffset += localOffset;

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
    private float _resolvedWidthForHeightPass;

    private void InitializeChildLayout()
    {
        var parentInner = _parent!.InnerRect;
        var myWidth = CalculateWidth(parentInner.W);
        _resolvedWidthForHeightPass = myWidth;
        var myHeight = CalculateHeight(parentInner.H);
        _rect = new Rect(0, 0, myWidth, myHeight);
    }

    private float CalculateWidth(float availableWidth)
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

    private float CalculateHeight(float availableHeight)
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

    private float CalculateContentWidth(float availableWidth)
    {
        var padding = Style.PaddingLeft + Style.PaddingRight;

        if (ChildNodes.Count == 0)
            return (_rect.W > 0 ? _rect.W : 10f) + padding;

        var inner = Math.Max(0f, availableWidth - padding);
        var content = Style.Direction == Axis.Horizontal
            ? CalculateHorizontalContentWidth(inner)
            : CalculateVerticalContentWidth(inner);
        return content + padding;
    }

    private float CalculateHorizontalContentWidth(float availableWidth)
    {
        var totalGap = (ChildNodes.Count - 1) * Style.Gap;
        return ChildNodes
            .Select(child => GetChildTotalWidth(child, availableWidth))
            .Sum() + totalGap;
    }

    private float CalculateVerticalContentWidth(float availableWidth)
    {
        return ChildNodes
            .Select(child => GetChildTotalWidth(child, availableWidth))
            .DefaultIfEmpty(10f)
            .Max();
    }

    private float GetChildTotalWidth(LayoutNode child, float availableWidth)
    {
        var marginWidth = child.Style.MarginLeft + child.Style.MarginRight;
        var contentWidth = child.Style.Width >= 0
            ? child.Style.Width
            : Math.Max(child.CalculateContentWidth(availableWidth), 10f);

        return marginWidth + contentWidth;
    }

    private float CalculateContentHeight(float availableHeight, float outerWidthForWrap = -1f)
    {
        var padding = Style.PaddingTop + Style.PaddingBottom;

        if (ChildNodes.Count == 0)
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

    private float CalculateVerticalContentHeight(float availableHeight)
    {
        var totalGap = (ChildNodes.Count - 1) * Style.Gap;
        return ChildNodes
            .Select(child => GetChildTotalHeight(child, availableHeight))
            .Sum() + totalGap;
    }

    private float CalculateHorizontalContentHeight(float availableHeight)
    {
        return ChildNodes
            .Select(child => GetChildTotalHeight(child, availableHeight))
            .DefaultIfEmpty(10f)
            .Max();
    }

    private float GetChildTotalHeight(LayoutNode child, float availableHeight)
    {
        var marginHeight = child.Style.MarginTop + child.Style.MarginBottom;
        var contentHeight = child.Style.Height >= 0
            ? child.Style.Height
            : Math.Max(child.CalculateContentHeight(availableHeight,
                Math.Max(0f, _resolvedWidthForHeightPass - Style.PaddingLeft - Style.PaddingRight)
                - child.Style.MarginLeft - child.Style.MarginRight), 10f);

        return marginHeight + contentHeight;
    }

    private void LayoutChildren()
    {
        if (ChildNodes.Count == 0) return;

        var contentRect = InnerRect;

        // Resolve percentage sizes against this node's content box into concrete pixels, so the
        // rest of the flow treats them like any explicitly-sized child.
        foreach (var child in ChildNodes)
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
    }

    private void LayoutChildrenVertically(Rect contentRect)
    {
        if (ChildNodes.Count == 0) return;

        var layoutContext = CreateVerticalLayoutContext(contentRect);
        var childDimensions = CalculateVerticalChildDimensions(layoutContext);
        PositionChildrenVertically(contentRect, childDimensions, layoutContext);
    }

    private VerticalLayoutContext CreateVerticalLayoutContext(Rect contentRect)
    {
        var totalGap = (ChildNodes.Count - 1) * Style.Gap;
        var availableHeight = contentRect.H - totalGap;
        var expandingChildren = ChildNodes.Where(c => c.Style.ExpandHeight || c.Style.IsExpanded).ToList();

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

    private ChildDimensions[] CalculateVerticalChildDimensions(VerticalLayoutContext context)
    {
        const float defaultChildHeight = 30f;
        var fixedHeight = CalculateFixedHeight(context);
        var remainingHeight = Math.Max(0, context.AvailableHeight - fixedHeight);
        var dimensions = new ChildDimensions[ChildNodes.Count];

        for (var i = 0; i < ChildNodes.Count; i++)
        {
            var child = ChildNodes[i];
            var marginHeight = child.Style.MarginTop + child.Style.MarginBottom;

            var childHeight = CalculateChildHeight(child, context, remainingHeight, defaultChildHeight);
            childHeight = Math.Max(childHeight, context.AvailableHeight - marginHeight > 0 ? 10f : 0f);
            childHeight = child.Style.ClampHeight(childHeight);

            dimensions[i] = new ChildDimensions { Height = childHeight };
        }

        return dimensions;
    }

    private float CalculateFixedHeight(VerticalLayoutContext context)
    {
        return ChildNodes
            .Select(child => GetChildFixedHeight(child, context))
            .Sum();
    }

    private float GetChildFixedHeight(LayoutNode child, VerticalLayoutContext context)
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

    private float CalculateChildHeight(LayoutNode child, VerticalLayoutContext context, float remainingHeight,
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

    private float CalculateExpandingChildHeight(LayoutNode child, VerticalLayoutContext context, float remainingHeight,
        float defaultHeight)
    {
        if (context.ExpandingChildren.Count == 1 && ChildNodes.Count == 1)
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

    private void PositionChildrenVertically(Rect contentRect, ChildDimensions[] childDimensions,
        VerticalLayoutContext context)
    {
        var totalActualHeight = childDimensions.Sum(d => d.Height) +
                                ChildNodes.Sum(c => c.Style.MarginTop + c.Style.MarginBottom) +
                                context.TotalGap;

        var extraSpaceY = Math.Max(0, context.AvailableHeight - totalActualHeight);
        var alignmentOffsetY = extraSpaceY * Style.AlignContentVertical;
        var currentY = contentRect.Y + alignmentOffsetY;

        for (var i = 0; i < ChildNodes.Count; i++)
        {
            var child = ChildNodes[i];
            currentY += child.Style.MarginTop;

            var childRect = CalculateVerticalChildRect(child, contentRect, currentY, childDimensions[i].Height);
            child._rect = childRect;

            if (child.ChildNodes.Count > 0) child.LayoutChildren();

            // Apply scroll offset after positioning
            child.ApplyScrollOffset();

            currentY += childDimensions[i].Height + child.Style.MarginBottom + Style.Gap;
        }
    }

    private Rect CalculateVerticalChildRect(LayoutNode child, Rect contentRect, float currentY, float childHeight)
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

    private void LayoutChildrenHorizontally(Rect contentRect)
    {
        if (ChildNodes.Count == 0) return;

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
    /// overflow the content width. Line height is the tallest child on that line; lines stack on
    /// the cross axis with the same gap. Children keep their natural size — no expand/justify on a
    /// wrapped axis, matching CSS <c>flex-wrap</c> defaults for this engine's scope.
    /// </summary>
    private void LayoutChildrenHorizontallyWrapped(Rect contentRect)
    {
        var lines = BuildWrapLines(contentRect.W);
        var currentY = contentRect.Y;

        foreach (var line in lines)
        {
            var lineHeight = 0f;
            foreach (var i in line)
                lineHeight = Math.Max(lineHeight, NaturalChildHeight(ChildNodes[i], contentRect.W));

            var currentX = contentRect.X;
            foreach (var i in line)
            {
                var child = ChildNodes[i];
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

    private List<List<int>> BuildWrapLines(float availableWidth)
    {
        var lines = new List<List<int>>();
        var line = new List<int>();
        var lineWidth = 0f;

        for (var i = 0; i < ChildNodes.Count; i++)
        {
            var child = ChildNodes[i];
            var w = NaturalChildWidth(child, availableWidth) + child.Style.MarginLeft + child.Style.MarginRight;
            var withGap = line.Count == 0 ? w : lineWidth + Style.Gap + w;

            if (line.Count > 0 && withGap > availableWidth)
            {
                lines.Add(line);
                line = new List<int>();
                lineWidth = 0f;
            }

            lineWidth = line.Count == 0 ? w : lineWidth + Style.Gap + w;
            line.Add(i);
        }

        if (line.Count > 0) lines.Add(line);
        return lines;
    }

    private static float NaturalChildWidth(LayoutNode child, float availableWidth) =>
        child.Style.Width >= 0
            ? child.Style.Width
            : child.Style.WidthPercent >= 0f
                ? availableWidth * child.Style.WidthPercent
                : Math.Max(child.CalculateContentWidth(availableWidth), 10f);

    private static float NaturalChildHeight(LayoutNode child, float availableWidth) =>
        child.Style.Height >= 0
            ? child.Style.Height
            : Math.Max(child.CalculateContentHeight(availableWidth, availableWidth), 10f);

    /// <summary>Cross-axis size of a wrapped horizontal container: stacked line heights plus gaps.</summary>
    private float CalculateWrappedContentHeight(float availableWidth)
    {
        var lines = BuildWrapLines(availableWidth);
        if (lines.Count == 0) return 0f;

        var total = (lines.Count - 1) * Style.Gap;
        foreach (var line in lines)
        {
            var lineHeight = 0f;
            foreach (var i in line)
                lineHeight = Math.Max(lineHeight, NaturalChildHeight(ChildNodes[i], availableWidth));
            total += lineHeight;
        }

        return total;
    }

    private HorizontalLayoutContext CreateHorizontalLayoutContext(Rect contentRect)
    {
        var totalGap = (ChildNodes.Count - 1) * Style.Gap;
        var availableWidth = contentRect.W - totalGap;
        var expandingChildren = ChildNodes.Where(c => c.Style.ExpandWidth || c.Style.IsExpanded).ToList();

        return new HorizontalLayoutContext
        {
            AvailableWidth = availableWidth,
            TotalGap = totalGap,
            ExpandingChildren = expandingChildren,
            TotalExpandPercentage =
                CalculateTotalExpandPercentage(expandingChildren, c => c.Style.ExpandWidthPercentage)
        };
    }

    private ChildDimensions[] CalculateHorizontalChildDimensions(HorizontalLayoutContext context)
    {
        const float defaultChildWidth = 80f;
        var fixedWidth = CalculateFixedWidth(context);
        var remainingWidth = Math.Max(0, context.AvailableWidth - fixedWidth);
        var dimensions = new ChildDimensions[ChildNodes.Count];

        for (var i = 0; i < ChildNodes.Count; i++)
        {
            var child = ChildNodes[i];
            var childWidth = CalculateChildWidth(child, context, remainingWidth, defaultChildWidth);
            var availableChildWidthForMin = Math.Max(0,
                context.AvailableWidth - child.Style.MarginLeft - child.Style.MarginRight);
            childWidth = Math.Max(childWidth, availableChildWidthForMin > 0 ? 10f : 0f);
            childWidth = child.Style.ClampWidth(childWidth);

            dimensions[i] = new ChildDimensions { Width = childWidth };
        }

        return dimensions;
    }

    private float CalculateFixedWidth(HorizontalLayoutContext context)
    {
        return ChildNodes
            .Select(child => GetChildFixedWidth(child, context))
            .Sum();
    }

    private float GetChildFixedWidth(LayoutNode child, HorizontalLayoutContext context)
    {
        var marginWidth = child.Style.MarginLeft + child.Style.MarginRight;

        if (child.Style.ExpandWidth || child.Style.IsExpanded)
            return marginWidth;

        var contentWidth = child.Style.Width >= 0
            ? child.Style.Width
            : child.CalculateContentWidth(context.AvailableWidth);

        return marginWidth + contentWidth;
    }

    private float CalculateChildWidth(LayoutNode child, HorizontalLayoutContext context, float remainingWidth,
        float defaultWidth)
    {
        if (child.Style.ExpandWidth || child.Style.IsExpanded)
            return CalculateExpandingChildWidth(child, context, remainingWidth, defaultWidth);

        if (child.Style.Width >= 0)
            return child.Style.Width;

        return child.CalculateContentWidth(context.AvailableWidth);
    }

    private float CalculateExpandingChildWidth(LayoutNode child, HorizontalLayoutContext context, float remainingWidth,
        float defaultWidth)
    {
        if (context.ExpandingChildren.Count == 1 && ChildNodes.Count == 1)
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

    private void PositionChildrenHorizontally(Rect contentRect, ChildDimensions[] childDimensions,
        HorizontalLayoutContext context)
    {
        var totalActualWidth = childDimensions.Sum(d => d.Width) +
                               ChildNodes.Sum(c => c.Style.MarginLeft + c.Style.MarginRight) +
                               context.TotalGap;

        var extraSpaceX = Math.Max(0, context.AvailableWidth - totalActualWidth);
        var alignmentOffsetX = extraSpaceX * Style.AlignContentHorizontal;
        var currentX = contentRect.X + alignmentOffsetX;

        for (var i = 0; i < ChildNodes.Count; i++)
        {
            var child = ChildNodes[i];
            currentX += child.Style.MarginLeft;

            var childRect = CalculateHorizontalChildRect(child, contentRect, currentX, childDimensions[i].Width);
            child._rect = childRect;

            if (child.ChildNodes.Count > 0) child.LayoutChildren();

            // Apply scroll offset after positioning
            child.ApplyScrollOffset();

            currentX += childDimensions[i].Width + child.Style.MarginRight + Style.Gap;
        }
    }

    private Rect CalculateHorizontalChildRect(LayoutNode child, Rect contentRect, float currentX, float childWidth)
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

    private float CalculateTotalExpandPercentage<T>(List<T> expandingChildren, Func<T, float> percentageSelector)
    {
        var total = expandingChildren.Sum(percentageSelector);
        return total <= 0 && expandingChildren.Count > 0 ? expandingChildren.Count : total;
    }

    private class VerticalLayoutContext
    {
        public float AvailableHeight { get; set; }
        public float ContentWidth { get; set; }
        public float TotalGap { get; set; }
        public List<LayoutNode> ExpandingChildren { get; set; } = new();
        public float TotalExpandPercentage { get; set; }
    }

    private class HorizontalLayoutContext
    {
        public float AvailableWidth { get; set; }
        public float TotalGap { get; set; }
        public List<LayoutNode> ExpandingChildren { get; set; } = new();
        public float TotalExpandPercentage { get; set; }
    }

    private struct ChildDimensions
    {
        public float Width { get; set; }
        public float Height { get; set; }
    }
}
