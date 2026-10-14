namespace Guinevere;

/// <summary>
/// Represents the state of a scrollable container, including scroll position, content dimensions, and scrollbar state.
/// </summary>
public class ScrollState
{
    /// <summary>
    /// Gets or sets the current scroll position in pixels.
    /// </summary>
    public Vector2 ScrollOffset { get; set; } = Vector2.Zero;

    /// <summary>
    /// Gets or sets the total size of the scrollable content.
    /// </summary>
    public Vector2 ContentSize { get; set; } = Vector2.Zero;

    /// <summary>
    /// Gets or sets the size of the viewport (the visible area).
    /// </summary>
    public Vector2 ViewportSize { get; set; } = Vector2.Zero;

    /// <summary>
    /// Gets or sets whether horizontal scrolling is enabled.
    /// </summary>
    public bool IsScrollingX { get; set; }

    /// <summary>
    /// Gets or sets whether vertical scrolling is enabled.
    /// </summary>
    public bool IsScrollingY { get; set; }

    /// <summary>
    /// Gets or sets whether the horizontal scrollbar is being dragged.
    /// </summary>
    public bool IsDraggingScrollbarX { get; set; }

    /// <summary>
    /// Gets or sets whether the vertical scrollbar is being dragged.
    /// </summary>
    public bool IsDraggingScrollbarY { get; set; }

    /// <summary>
    /// Gets or sets whether the vertical scrollbar is currently hovered.
    /// </summary>
    public bool IsVerticalScrollbarHovered { get; set; }

    /// <summary>
    /// Gets or sets whether the horizontal scrollbar is currently hovered.
    /// </summary>
    public bool IsHorizontalScrollbarHovered { get; set; }

    /// <summary>
    /// Gets or sets the thickness of the scrollbar in pixels.
    /// </summary>
    public float ScrollbarThickness { get; set; } = 12f;

    /// <summary>
    /// Gets or sets whether the horizontal scrollbar should be shown.
    /// </summary>
    public bool ShowScrollbarX { get; set; }

    /// <summary>
    /// Gets or sets whether the vertical scrollbar should be shown.
    /// </summary>
    public bool ShowScrollbarY { get; set; }

    /// <summary>
    /// Gets or sets the mouse position when scrollbar dragging started.
    /// </summary>
    public Vector2 DragStartMousePos { get; set; } = Vector2.Zero;

    /// <summary>
    /// Gets or sets the scroll offset when scrollbar dragging started.
    /// </summary>
    public Vector2 DragStartScrollOffset { get; set; } = Vector2.Zero;

    /// <summary>
    /// Minimum size for scrollbar thumbs in pixels.
    /// </summary>
    public const float ScrollbarMinThumbSize = 20f;

    /// <summary>
    /// Padding around scrollbar tracks in pixels.
    /// </summary>
    public const float ScrollbarPadding = 2f;

    /// <summary>
    /// Gets the maximum scroll position.
    /// </summary>
    public Vector2 MaxScroll => new(
        Math.Max(0, ContentSize.X - ViewportSize.X),
        Math.Max(0, ContentSize.Y - ViewportSize.Y)
    );

    /// <summary>
    /// Clamps the scroll position to valid values.
    /// </summary>
    public void ClampScrollPosition()
    {
        var max = MaxScroll;
        ScrollOffset = new Vector2(
            Math.Max(0, Math.Min(ScrollOffset.X, max.X)),
            Math.Max(0, Math.Min(ScrollOffset.Y, max.Y))
        );
    }

    /// <summary>
    /// Determines if horizontal scrolling is needed.
    /// </summary>
    public bool NeedsHorizontalScroll => ContentSize.X > ViewportSize.X && IsScrollingX;

    /// <summary>
    /// Determines if vertical scrolling is needed.
    /// </summary>
    public bool NeedsVerticalScroll => ContentSize.Y > ViewportSize.Y && IsScrollingY;

    /// <summary>
    /// Calculates the vertical scrollbar dimensions and thumb position.
    /// </summary>
    public (Rect track, Rect thumb) CalculateVerticalScrollbar(Rect containerRect)
    {
        var trackX = containerRect.X + containerRect.W - ScrollbarThickness;
        var trackY = containerRect.Y;
        var trackHeight = containerRect.H - (ShowScrollbarX ? ScrollbarThickness : 0);

        var track = new Rect(trackX, trackY, ScrollbarThickness, trackHeight);

        var thumbHeight = Math.Max(ScrollbarMinThumbSize, ViewportSize.Y / ContentSize.Y * trackHeight);
        var thumbY = trackY;
        if (MaxScroll.Y > 0)
        {
            thumbY += ScrollOffset.Y / MaxScroll.Y * (trackHeight - thumbHeight);
        }

        var thumb = new Rect(trackX + ScrollbarPadding, thumbY, ScrollbarThickness - 2 * ScrollbarPadding,
            thumbHeight);

        return (track, thumb);
    }

    /// <summary>
    /// Calculates the horizontal scrollbar dimensions and thumb position.
    /// </summary>
    public (Rect track, Rect thumb) CalculateHorizontalScrollbar(Rect containerRect)
    {
        var trackX = containerRect.X;
        var trackY = containerRect.Y + containerRect.H - ScrollbarThickness;
        var trackWidth = containerRect.W - (ShowScrollbarY ? ScrollbarThickness : 0);

        var track = new Rect(trackX, trackY, trackWidth, ScrollbarThickness);

        var thumbWidth = Math.Max(ScrollbarMinThumbSize, ViewportSize.X / ContentSize.X * trackWidth);
        var thumbX = trackX;
        if (MaxScroll.X > 0)
        {
            thumbX += ScrollOffset.X / MaxScroll.X * (trackWidth - thumbWidth);
        }

        var thumb = new Rect(thumbX, trackY + ScrollbarPadding, thumbWidth,
            ScrollbarThickness - 2 * ScrollbarPadding);

        return (track, thumb);
    }

    /// <summary>
    /// Checks if a point is over the vertical scrollbar thumb.
    /// </summary>
    public bool IsPointOverVerticalThumb(Vector2 point, Rect containerRect)
    {
        if (!NeedsVerticalScroll)
        {
            return false;
        }

        var (_, thumb) = CalculateVerticalScrollbar(containerRect);
        return thumb.Contains(point);
    }

    /// <summary>
    /// Checks if a point is over the horizontal scrollbar thumb.
    /// </summary>
    public bool IsPointOverHorizontalThumb(Vector2 point, Rect containerRect)
    {
        if (!NeedsHorizontalScroll)
        {
            return false;
        }

        var (_, thumb) = CalculateHorizontalScrollbar(containerRect);
        return thumb.Contains(point);
    }

    /// <summary>
    /// Handles vertical scrollbar drag operations.
    /// </summary>
    public void HandleVerticalScrollbarDrag(Vector2 mousePos, Rect containerRect)
    {
        if (!IsDraggingScrollbarY)
        {
            return;
        }

        var (track, _) = CalculateVerticalScrollbar(containerRect);
        var thumbHeight = Math.Max(ScrollbarMinThumbSize, ViewportSize.Y / ContentSize.Y * track.H);

        var dragDelta = mousePos.Y - DragStartMousePos.Y;
        var scrollableHeight = track.H - thumbHeight;

        if (scrollableHeight > 0)
        {
            var scrollRatio = dragDelta / scrollableHeight;
            ScrollOffset = new Vector2(
                ScrollOffset.X,
                DragStartScrollOffset.Y + scrollRatio * MaxScroll.Y
            );
            ClampScrollPosition();
        }
    }

    /// <summary>
    /// Handles horizontal scrollbar drag operations.
    /// </summary>
    public void HandleHorizontalScrollbarDrag(Vector2 mousePos, Rect containerRect)
    {
        if (!IsDraggingScrollbarX)
        {
            return;
        }

        var (track, _) = CalculateHorizontalScrollbar(containerRect);
        var thumbWidth = Math.Max(ScrollbarMinThumbSize, ViewportSize.X / ContentSize.X * track.W);

        var dragDelta = mousePos.X - DragStartMousePos.X;
        var scrollableWidth = track.W - thumbWidth;

        if (scrollableWidth > 0)
        {
            var scrollRatio = dragDelta / scrollableWidth;
            ScrollOffset = new Vector2(
                DragStartScrollOffset.X + scrollRatio * MaxScroll.X,
                ScrollOffset.Y
            );
            ClampScrollPosition();
        }
    }
}
