namespace Guinevere;

/// <summary>
/// Wrapped horizontal flow: children keep their natural size and break onto a new line when the next one (plus gap)
/// would overflow the content width. Line height is the tallest child on the line; lines stack with the same gap.
/// </summary>
public partial class LayoutNode
{
    void LayoutChildrenHorizontallyWrapped(Rect contentRect)
    {
        var gap = GapPixels(contentRect.W, contentRect.H);
        var currentY = contentRect.Y;
        var start = 0;
        while (start < FlowChildren.Count)
        {
            var end = FindWrapLineEnd(start, contentRect.W, gap);
            var lineHeight = 0f;
            for (var i = start; i < end; i++)
            {
                lineHeight = Math.Max(lineHeight, NaturalChildHeight(FlowChildren[i], contentRect.W));
            }

            var currentX = contentRect.X;
            for (var i = start; i < end; i++)
            {
                var child = FlowChildren[i];
                currentX += child.MarginLeftPixels;

                var w = NaturalChildWidth(child, contentRect.W);
                var h = child.Style.HasHeight ? child.OwnHeight(lineHeight, w) : lineHeight;
                h = child.ClampResolvedHeight(h, lineHeight, w, lineHeight);

                var offset = CrossOffset(child, lineHeight - h, horizontal: false);
                child.Place(new Rect(currentX, currentY + child.MarginTopPixels + offset, w, h));
                if (child.ChildNodes.Count > 0)
                {
                    child.LayoutChildren();
                }

                child.ApplyScrollOffset();
                currentX += w + child.MarginRightPixels + gap;
            }

            currentY += lineHeight + gap;
            start = end;
        }
    }

    int FindWrapLineEnd(int start, float availableWidth, float gap)
    {
        var lineWidth = 0f;
        for (var i = start; i < FlowChildren.Count; i++)
        {
            var child = FlowChildren[i];
            var w = NaturalChildWidth(child, availableWidth) + child.HorizontalMargin;
            var withGap = i == start ? w : lineWidth + gap + w;
            if (i > start && withGap > availableWidth)
            {
                return i;
            }

            lineWidth = withGap;
        }

        return FlowChildren.Count;
    }

    static float NaturalChildWidth(LayoutNode child, float availableWidth)
    {
        var perpendicular = Math.Max(0f, child.Rect.H);
        var width = child.Style.HasWidth
            ? child.NaturalWidth(availableWidth, perpendicular)
            : Math.Max(child.CalculateContentWidth(availableWidth), 10f);
        return child.ClampResolvedWidth(width, availableWidth, perpendicular, availableWidth);
    }

    static float NaturalChildHeight(LayoutNode child, float availableWidth)
    {
        var perpendicular = Math.Max(0f, child.Rect.W);
        var height = child.Style.HasHeight
            ? child.NaturalHeight(availableWidth, perpendicular, availableWidth)
            : Math.Max(child.CalculateContentHeight(availableWidth, availableWidth), 10f);
        return child.ClampResolvedHeight(height, availableWidth, perpendicular, availableWidth);
    }

    /// <summary>Cross-axis size of a wrapped horizontal container: stacked line heights plus gaps.</summary>
    float CalculateWrappedContentHeight(float availableWidth)
    {
        var gap = GapPixels(availableWidth, 0f);
        var total = 0f;
        var lineCount = 0;
        var start = 0;
        while (start < FlowChildren.Count)
        {
            var end = FindWrapLineEnd(start, availableWidth, gap);
            var lineHeight = 0f;
            for (var i = start; i < end; i++)
            {
                lineHeight = Math.Max(lineHeight, NaturalChildHeight(FlowChildren[i], availableWidth));
            }

            total += lineHeight;
            lineCount++;
            start = end;
        }

        return total + Math.Max(0, lineCount - 1) * gap;
    }
}
