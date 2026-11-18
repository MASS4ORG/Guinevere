namespace Guinevere;

/// <summary>The slice of a long list one frame builds, and the space that stands in for the rest.</summary>
/// <param name="First">Index of the first built row.</param>
/// <param name="Last">Index one past the last built row.</param>
/// <param name="PadTop">Height of the rows skipped above the window.</param>
/// <param name="PadBottom">Height of the rows skipped below the window.</param>
readonly record struct RowWindow(int First, int Last, float PadTop, float PadBottom);

/// <summary>What a scrolling node measured: its viewport and how far its content is scrolled.</summary>
/// <param name="Width">Viewport width inside the node's padding.</param>
/// <param name="Height">Viewport height.</param>
/// <param name="X">Horizontal scroll offset.</param>
/// <param name="Y">Vertical scroll offset.</param>
readonly record struct ScrollMeasure(float Width, float Height, float X, float Y);

/// <summary>
/// The machinery virtualised lists share: which rows to build, the spacers that keep the scrollbar exact,
/// and the measurement the next frame builds against. Rows have a fixed height, so a row's position is
/// arithmetic and no layout is needed to decide what to build.
/// </summary>
static class VirtualRows
{
    /// <summary>
    /// The rows to build for a viewport. Both passes of a frame must pick the same rows, and a node's
    /// rect is only resolved in the render pass, so callers feed in what the previous frame measured.
    /// </summary>
    /// <param name="scroll">Scroll offset along the list.</param>
    /// <param name="viewport">Viewport extent along the list.</param>
    /// <param name="stride">Distance from one row's start to the next.</param>
    /// <param name="count">Rows in the whole list.</param>
    /// <param name="overscan">Rows kept built beyond each edge, so a fast scroll shows no gap.</param>
    public static RowWindow Window(float scroll, float viewport, float stride, int count, int overscan)
    {
        var first = Math.Max(0, (int)(scroll / stride) - overscan);
        var take = (int)(viewport / stride) + (overscan * 2) + 1;
        var last = Math.Min(count, first + take);
        return new RowWindow(first, last, first * stride, (count - last) * stride);
    }

    /// <summary>Records the current scrolling node's viewport and offset for the next frame to build against.</summary>
    /// <param name="gui">The GUI whose current node scrolls.</param>
    public static ScrollMeasure Measure(Gui gui)
    {
        var node = gui.CurrentNode;
        var offset = gui.GetScrollState(node.Id)?.ScrollOffset;
        return new ScrollMeasure(node.InnerRect.W, node.Rect.H, offset?.X ?? 0f, offset?.Y ?? 0f);
    }

    /// <summary>An empty node as tall as the rows it stands in for, so the content height stays exact.</summary>
    /// <param name="gui">The GUI to build the node in.</param>
    /// <param name="id">Node identity within the parent.</param>
    /// <param name="height">Height to reserve; nothing is built for zero or less.</param>
    public static void Spacer(Gui gui, string id, float height)
    {
        if (height <= 0)
        {
            return;
        }

        using (gui.Node(-1, height, id).ExpandWidth().Enter())
        {
        }
    }

    /// <summary>An empty node as wide as the columns it stands in for, inside a row.</summary>
    /// <param name="gui">The GUI to build the node in.</param>
    /// <param name="id">Node identity within the parent.</param>
    /// <param name="width">Width to reserve; nothing is built for zero or less.</param>
    /// <param name="height">Height of the row it sits in.</param>
    public static void SpacerX(Gui gui, string id, float width, float height)
    {
        if (width <= 0)
        {
            return;
        }

        using (gui.Node(width, height, id).Enter())
        {
        }
    }
}
