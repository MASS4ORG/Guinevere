using System.Buffers;
using System.Runtime.CompilerServices;

namespace Guinevere;

/// <summary>
/// Placement of children along vertical, horizontal and wrapped flows, and of absolute children. Sizes along the flow
/// are resolved first; expanding sizes share what fixed sizes leave, then free space goes to expanding gaps and
/// margins, <see cref="SpacingTypes"/> and finally the content alignment.
/// </summary>
public partial class LayoutNode
{
    /// <summary>Flows up to this many children size on the stack; longer ones rent a pooled buffer.</summary>
    const int StackFlowLimit = 32;

    /// <summary>The space one flow offers its children.</summary>
    /// <param name="Available">Space along the flow left after the fixed part of the gaps.</param>
    /// <param name="Cross">Space across the flow.</param>
    /// <param name="Gap">The fixed part of one gap.</param>
    /// <param name="ExpandingCount">Children whose size along the flow expands.</param>
    /// <param name="TotalExpand">Sum of those children's expand weights.</param>
    readonly record struct FlowContext(float Available, float Cross, float Gap, int ExpandingCount, float TotalExpand)
    {
        /// <summary>The fixed part of all gaps.</summary>
        public float TotalGap(int count) => (count - 1) * Gap;
    }

    void LayoutChildren()
    {
        if (ChildNodes.Count == 0)
        {
            return;
        }

        var contentRect = InnerRect;
        SetChildSpacingBasis(contentRect.Size);
        if (Style.Direction == Axis.Vertical)
        {
            LayoutChildrenVertically(contentRect);
        }
        else
        {
            LayoutChildrenHorizontally(contentRect);
        }

        PositionAbsoluteChildren(contentRect);
    }

    /// <summary>
    /// Places the children that opted out of the flow. Their size and offset resolve against the box they are
    /// positioned in — the parent's content box, or the screen for <see cref="AbsoluteOrigin.Screen"/> — so
    /// expansion and percentages keep working on an overlay.
    /// </summary>
    void PositionAbsoluteChildren(Rect contentRect)
    {
        if (_absoluteChildCount != 0)
        {
            PlaceAbsoluteChildren(contentRect);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void PlaceAbsoluteChildren(Rect contentRect)
    {
        foreach (var child in ChildNodes)
        {
            if (!child.Style.IsAbsolute)
            {
                continue;
            }

            var isScreen = child.Style.AbsoluteOrigin == AbsoluteOrigin.Screen;
            var box = isScreen ? _gui.BoundsFor(child.Scope) : contentRect;
            child.SetSpacingBasis(box.Size);

            var width = child.CalculateWidth(box.W);
            child._resolvedWidthForHeightPass = width;
            var height = child.CalculateHeight(box.H);
            var origin = isScreen ? Vector2.Zero : new Vector2(box.X, box.Y);
            child.Place(new Rect(
                origin.X + child.ResolveEdge(child.Style.AbsoluteX, horizontal: true),
                origin.Y + child.ResolveEdge(child.Style.AbsoluteY, horizontal: false),
                width,
                height));

            if (child.ChildNodes.Count > 0)
            {
                child.LayoutChildren();
            }
        }
    }

    FlowContext CreateFlowContext(float along, float across, bool horizontal)
    {
        var gap = GapPixels(along, across);
        var expandingCount = 0;
        var totalExpand = 0f;
        foreach (var child in FlowChildren)
        {
            var weight = horizontal ? child.Style.WidthExpandWeight : child.Style.HeightExpandWeight;
            if (weight != 0f)
            {
                expandingCount++;
                totalExpand += weight;
            }
        }

        return new FlowContext(along - (FlowChildren.Count - 1) * gap, across, gap, expandingCount,
            totalExpand <= 0f && expandingCount > 0 ? expandingCount : totalExpand);
    }

    /// <summary>The space one unit of expand weight receives; a lone expanding child scales all of it.</summary>
    float ExpandShare(FlowContext context, float remaining) =>
        context.ExpandingCount == 1 && FlowChildren.Count == 1 ? remaining : remaining / context.TotalExpand;

    void LayoutChildrenVertically(Rect contentRect)
    {
        if (FlowChildren.Count == 0)
        {
            return;
        }

        var context = CreateFlowContext(contentRect.H, contentRect.W, horizontal: false);
        var count = FlowChildren.Count;
        var rented = count > StackFlowLimit ? ArrayPool<float>.Shared.Rent(count) : null;
        var heights = rented is null ? stackalloc float[count] : rented.AsSpan(0, count);
        CalculateVerticalChildHeights(context, heights);
        PositionChildrenVertically(contentRect, heights, context);
        if (rented is not null)
        {
            ArrayPool<float>.Shared.Return(rented);
        }
    }

    void CalculateVerticalChildHeights(FlowContext context, Span<float> heights)
    {
        var fixedHeight = 0f;
        foreach (var child in FlowChildren)
        {
            fixedHeight += GetChildFixedHeight(child, context);
        }

        var remaining = Math.Max(0f, context.Available - fixedHeight);
        var share = context.ExpandingCount > 0 ? ExpandShare(context, remaining) : 0f;
        for (var i = 0; i < FlowChildren.Count; i++)
        {
            var child = FlowChildren[i];
            var height = CalculateChildHeight(child, context, share);
            if (child.Style.HeightNeedsFloor)
            {
                height = Math.Max(height, context.Available - child.VerticalMargin > 0f ? 10f : 0f);
            }

            heights[i] = child.ClampResolvedHeight(height, context.Available, context.Cross, remaining);
        }
    }

    static float GetChildFixedHeight(LayoutNode child, FlowContext context) => child.VerticalMargin
        + (child.Style.HasHeight
            ? child.OwnHeight(context.Available, context.Cross)
            : HasSettledLeafHeight(child)
                ? child.Rect.H
                : child.CalculateContentHeight(context.Available, context.Cross - child.HorizontalMargin));

    static float CalculateChildHeight(LayoutNode child, FlowContext context, float share) =>
        child.Style.HasHeight
            ? child.OwnHeight(context.Available, context.Cross, child.Style.HeightExpandWeight != 0f ? share : 0f)
            : HasSettledLeafHeight(child)
                ? child.Rect.H
                : child.CalculateContentHeight(context.Available, context.Cross - child.HorizontalMargin);

    /// <summary>
    /// Whether an unsized leaf keeps the height it already has; measured content is excluded because its height
    /// depends on the width it is laid out at.
    /// </summary>
    static bool HasSettledLeafHeight(LayoutNode child) =>
        child.ChildNodes.Count == 0 && child.ContentMeasure is null && child.Rect.H > 0;

    void PositionChildrenVertically(Rect contentRect, ReadOnlySpan<float> heights, FlowContext context)
    {
        var used = context.TotalGap(FlowChildren.Count);
        for (var i = 0; i < FlowChildren.Count; i++)
        {
            used += heights[i] + FlowChildren[i].VerticalMargin;
        }

        var distribution = DistributeFlow(contentRect.H - used, horizontal: false, context.ExpandingCount > 0);
        var currentY = contentRect.Y + distribution.Leading;
        for (var i = 0; i < FlowChildren.Count; i++)
        {
            var child = FlowChildren[i];
            currentY += child.MarginTopPixels + LeadingMarginWeight(child, false) * distribution.PerWeight;
            child.Place(CalculateVerticalChildRect(child, contentRect, currentY, heights[i]));
            if (child.ChildNodes.Count > 0)
            {
                child.LayoutChildren();
            }

            child.ApplyScrollOffset();
            currentY += heights[i] + child.MarginBottomPixels
                        + TrailingMarginWeight(child, false) * distribution.PerWeight
                        + context.Gap + distribution.Between;
        }
    }

    Rect CalculateVerticalChildRect(LayoutNode child, Rect contentRect, float currentY, float childHeight)
    {
        var margin = child.HorizontalMargin;
        var available = Math.Max(0f, contentRect.W - margin);
        var childWidth = child.Style.HasWidth ? child.OwnWidth(available, childHeight, available) : available;
        childWidth = child.ClampResolvedWidth(childWidth, available, childHeight, available);

        var offset = CrossOffset(child, contentRect.W - childWidth - margin, horizontal: true);
        return new Rect(contentRect.X + child.MarginLeftPixels + offset, currentY, childWidth, childHeight);
    }

    void LayoutChildrenHorizontally(Rect contentRect)
    {
        if (FlowChildren.Count == 0)
        {
            return;
        }

        if (Style.Wrap)
        {
            LayoutChildrenHorizontallyWrapped(contentRect);
            return;
        }

        var context = CreateFlowContext(contentRect.W, contentRect.H, horizontal: true);
        var count = FlowChildren.Count;
        var rented = count > StackFlowLimit ? ArrayPool<float>.Shared.Rent(count) : null;
        var widths = rented is null ? stackalloc float[count] : rented.AsSpan(0, count);
        CalculateHorizontalChildWidths(context, widths);
        PositionChildrenHorizontally(contentRect, widths, context);
        if (rented is not null)
        {
            ArrayPool<float>.Shared.Return(rented);
        }
    }

    void CalculateHorizontalChildWidths(FlowContext context, Span<float> widths)
    {
        var fixedWidth = 0f;
        foreach (var child in FlowChildren)
        {
            fixedWidth += GetChildFixedWidth(child, context);
        }

        var remaining = Math.Max(0f, context.Available - fixedWidth);
        var share = context.ExpandingCount > 0 ? ExpandShare(context, remaining) : 0f;
        for (var i = 0; i < FlowChildren.Count; i++)
        {
            var child = FlowChildren[i];
            var perpendicular = Math.Max(0f, child.Rect.H);
            var width = child.Style.HasWidth
                ? child.OwnWidth(context.Available, perpendicular, child.Style.WidthExpandWeight != 0f ? share : 0f)
                : child.CalculateContentWidth(context.Available);
            if (child.Style.WidthNeedsFloor)
            {
                width = Math.Max(width, context.Available - child.HorizontalMargin > 0f ? 10f : 0f);
            }

            widths[i] = child.ClampResolvedWidth(width, context.Available, perpendicular, remaining);
        }
    }

    static float GetChildFixedWidth(LayoutNode child, FlowContext context) => child.HorizontalMargin
        + (child.Style.HasWidth
            ? child.OwnWidth(context.Available, Math.Max(0f, child.Rect.H))
            : child.CalculateContentWidth(context.Available));

    void PositionChildrenHorizontally(Rect contentRect, ReadOnlySpan<float> widths, FlowContext context)
    {
        var used = context.TotalGap(FlowChildren.Count);
        for (var i = 0; i < FlowChildren.Count; i++)
        {
            used += widths[i] + FlowChildren[i].HorizontalMargin;
        }

        var distribution = DistributeFlow(contentRect.W - used, horizontal: true, context.ExpandingCount > 0);
        var currentX = contentRect.X + distribution.Leading;
        for (var i = 0; i < FlowChildren.Count; i++)
        {
            var child = FlowChildren[i];
            currentX += child.MarginLeftPixels + LeadingMarginWeight(child, true) * distribution.PerWeight;
            child.Place(CalculateHorizontalChildRect(child, contentRect, currentX, widths[i]));
            if (child.ChildNodes.Count > 0)
            {
                child.LayoutChildren();
            }

            child.ApplyScrollOffset();
            currentX += widths[i] + child.MarginRightPixels
                        + TrailingMarginWeight(child, true) * distribution.PerWeight
                        + context.Gap + distribution.Between;
        }
    }

    Rect CalculateHorizontalChildRect(LayoutNode child, Rect contentRect, float currentX, float childWidth)
    {
        var margin = child.VerticalMargin;
        var available = contentRect.H - margin;
        var childHeight = child.Style.HasHeight
            ? child.OwnHeight(available, childWidth, available)
            : Math.Max(0f, available);
        childHeight = child.ClampResolvedHeight(childHeight, available, childWidth, available);

        var offset = CrossOffset(child, contentRect.H - childHeight - margin, horizontal: false);
        return new Rect(currentX, contentRect.Y + child.MarginTopPixels + offset, childWidth, childHeight);
    }
}
