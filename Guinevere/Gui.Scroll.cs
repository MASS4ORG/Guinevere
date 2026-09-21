namespace Guinevere;

public partial class Gui
{
    readonly Dictionary<string, ScrollState> _scrollStates = new();

    /// <summary>
    /// Enables horizontal scrolling for the current node.
    /// </summary>
    /// <param name="foregroundColor">Color of the scrollbar thumb</param>
    /// <param name="backgroundColor">Color of the scrollbar track</param>
    /// <returns>The current node for chaining</returns>
    public LayoutNode ScrollX(Color? foregroundColor = null, Color? backgroundColor = null)
    {
        return ScrollContainer(true, false, foregroundColor, backgroundColor);
    }

    /// <summary>
    /// Enables vertical scrolling for the current node.
    /// </summary>
    /// <param name="foregroundColor">Color of the scrollbar thumb</param>
    /// <param name="backgroundColor">Color of the scrollbar track</param>
    /// <returns>The current noden for chaining</returns>
    public LayoutNode ScrollY(Color? foregroundColor = null, Color? backgroundColor = null)
    {
        return ScrollContainer(false, true, foregroundColor, backgroundColor);
    }

    /// <summary>
    /// Enables both horizontal and vertical scrolling for the current node.
    /// </summary>
    /// <param name="foregroundColor">Color of the scrollbar thumb</param>
    /// <param name="backgroundColor">Color of the scrollbar track</param>
    /// <returns>The current node for chaining</returns>
    public LayoutNode Scroll(Color? foregroundColor = null, Color? backgroundColor = null)
    {
        return ScrollContainer(true, true, foregroundColor, backgroundColor);
    }

    /// <summary>
    /// Clips drawing to the current node's bounds.
    /// Proper clipping for scrollable containers.
    /// </summary>
    /// <returns>The current node for chaining</returns>
    public void ClipContent()
    {
        // During build pass, defer clipping until layout is complete
        if (Pass == Pass.Pass1Build)
        {
            // Mark that this node needs clipping, but don't apply it yet
            var buildScrollState = GetScrollState(CurrentNode.Id);
            if (buildScrollState != null && (buildScrollState.IsScrollingX || buildScrollState.IsScrollingY))
                SetClipped(true, CurrentNode.Scope);
            return;
        }

        if (Pass != Pass.Pass2Render) return;

        // For scrollable containers, we need to clip to the viewport bounds
        // not the scrolled content bounds
        var scrollState = GetScrollState(CurrentNode.Id);
        if (scrollState != null && (scrollState.IsScrollingX || scrollState.IsScrollingY))
        {
            // Ensure the node's layout is finalized before clipping
            var clipRect = CurrentNode.Rect;

            // Only apply clipping if the rectangle has valid dimensions
            if (clipRect is { W: > 0, H: > 0 })
            {
                SetClipped(true, CurrentNode.Scope);
                CurrentNode.DrawList.AddClip(clipRect);
            }
        }
        else
        {
            // Non-scrollable content still needs basic clipping
            var clipRect = CurrentNode.Rect;
            if (clipRect is { W: > 0, H: > 0 })
            {
                SetClipped(true, CurrentNode.Scope);
                CurrentNode.DrawList.AddClip(clipRect);
            }
        }
    }

    /// <summary>
    /// Creates a scrollable container with proper clipping and overflow handling.
    /// This is the recommended way to create scrollable areas.
    /// </summary>
    /// <param name="scrollX">Enable horizontal scrolling</param>
    /// <param name="scrollY">Enable vertical scrolling</param>
    /// <param name="foregroundColor">Scrollbar foreground color</param>
    /// <param name="backgroundColor">Scrollbar background color</param>
    /// <returns>The scrollable container node</returns>
    public LayoutNode ScrollContainer(bool scrollX = false, bool scrollY = true,
        Color? foregroundColor = null, Color? backgroundColor = null)
    {
        var node = CurrentNode;
        var scrollState = GetOrCreateScrollState(node.Id);

        if (scrollX) scrollState.IsScrollingX = true;
        if (scrollY) scrollState.IsScrollingY = true;

        // Mark this node as a scroll container
        SetIsScrollContainer(true);

        // Reserve the bar's width so content stops before it rather than running underneath. The
        // decision uses the previous frame's state, which is what both passes of this frame see.
        if (Pass == Pass.Pass1Build)
        {
            if (scrollY && scrollState.ShowScrollbarY)
                node.PaddingRight(node.Style.PaddingRight + scrollState.ScrollbarThickness);
            if (scrollX && scrollState.ShowScrollbarX)
                node.PaddingBottom(node.Style.PaddingBottom + scrollState.ScrollbarThickness);
        }

        // Update local scroll offset in node scope
        SetLocalScrollOffset(scrollState.ScrollOffset, node.Scope);

        if (Pass == Pass.Pass2Render)
        {
            HandleScrollInput(node, scrollState);

            // Clip first: the scrollbars live in their own raised node and must not be clipped away
            // with the content.
            ClipContent();
            DrawScrollbars(node, scrollState, scrollX, scrollY, foregroundColor, backgroundColor);
        }
        else if (Pass == Pass.Pass1Build)
        {
            // During build pass, just mark that this node will need clipping
            SetClipped(true, CurrentNode.Scope);
        }

        return node;
    }

    ScrollState GetOrCreateScrollState(string nodeId)
    {
        if (!_scrollStates.TryGetValue(nodeId, out var state))
        {
            state = new ScrollState();
            _scrollStates[nodeId] = state;
        }

        return state;
    }

    /// <summary>
    /// Gets the scroll state for the specified node ID (public access for ClipOperation).
    /// </summary>
    /// <param name="nodeId">The node ID to get scroll state for</param>
    /// <returns>The scroll state or null if not found</returns>
    public ScrollState? GetScrollState(string nodeId)
    {
        return _scrollStates.TryGetValue(nodeId, out var state) ? state : null;
    }

    void HandleScrollInput(LayoutNode node, ScrollState scrollState)
    {
        var mousePos = Input.MousePosition;
        var nodeRect = node.InnerRect;

        // Update viewport size
        scrollState.ViewportSize = new Vector2(nodeRect.W, nodeRect.H);

        // Calculate content size by examining children
        UpdateContentSize(node, scrollState);

        // Check if scrollbars should be shown
        scrollState.ShowScrollbarX = scrollState.NeedsHorizontalScroll;
        scrollState.ShowScrollbarY = scrollState.NeedsVerticalScroll;


        // Handle mouse wheel scrolling only when the mouse is over the node and nothing with a
        // higher z-index -- a modal dialog's dimmed overlay, say -- is blocking it from here.
        var screenRect = new Rect(nodeRect.X, nodeRect.Y, nodeRect.W, nodeRect.H);
        if (screenRect.Contains(mousePos) && !IsHoverBlocked(node))
        {
            var wheelDelta = Input.MouseWheelDelta;
            if (Math.Abs(wheelDelta) > 0.01f)
            {
                if (Input.IsKeyDown(KeyboardKey.LeftShift) || Input.IsKeyDown(KeyboardKey.RightShift))
                {
                    // Horizontal scroll with shift
                    if (scrollState.IsScrollingX)
                    {
                        scrollState.ScrollOffset = new Vector2(
                            scrollState.ScrollOffset.X - wheelDelta * 50f,
                            scrollState.ScrollOffset.Y);
                        scrollState.ClampScrollPosition();
                        SetLocalScrollOffset(scrollState.ScrollOffset, node.Scope);
                    }
                }
                else
                {
                    // Vertical scroll
                    if (scrollState.IsScrollingY)
                    {
                        scrollState.ScrollOffset = new Vector2(
                            scrollState.ScrollOffset.X,
                            scrollState.ScrollOffset.Y - wheelDelta * 50f);
                        scrollState.ClampScrollPosition();
                        SetLocalScrollOffset(scrollState.ScrollOffset, node.Scope);
                    }
                }
            }
        }

        // Handle scrollbar dragging
        HandleScrollbarDragging(node, scrollState, mousePos);
    }

    void UpdateContentSize(LayoutNode node, ScrollState scrollState)
    {
        var maxX = 0f;
        var maxY = 0f;

        if (node.Children.Count > 0)
        {
            foreach (var child in node.Children)
            {
                // The children's rects carry the current scroll offset (ApplyScrollOffset subtracts
                // it), so measuring them directly makes ContentSize -- and with it the drag ratio and
                // clamp target -- drift frame to frame. Holding the thumb near the bottom then
                // oscillated instead of holding still. Undo the offset to measure the unscrolled extent.
                var x = child.Rect.X + scrollState.ScrollOffset.X;
                var y = child.Rect.Y + scrollState.ScrollOffset.Y;
                maxX = Math.Max(maxX, x + child.Rect.W);
                maxY = Math.Max(maxY, y + child.Rect.H);
            }

            // Calculate content size relative to the container
            var contentWidth = maxX - node.InnerRect.X;
            var contentHeight = maxY - node.InnerRect.Y;

            // Set content size based on actual content (minimum is viewport size)
            scrollState.ContentSize = new Vector2(
                Math.Max(contentWidth, scrollState.ViewportSize.X),
                Math.Max(contentHeight, scrollState.ViewportSize.Y));
        }
        else
        {
            // No children, content size equals viewport size
            scrollState.ContentSize = scrollState.ViewportSize;
        }
    }

    void HandleScrollbarDragging(LayoutNode node, ScrollState scrollState, Vector2 mousePos)
    {
        // The node's own rect, matching where the bar is drawn. The content box is inset by the width
        // the bar reserved, so hit-testing against it would sit the hot area beside the bar.
        var nodeRect = node.Rect;

        // Update hover states
        scrollState.IsVerticalScrollbarHovered = scrollState.ShowScrollbarY &&
                                                 scrollState.IsPointOverVerticalThumb(mousePos, nodeRect);
        scrollState.IsHorizontalScrollbarHovered = scrollState.ShowScrollbarX &&
                                                   scrollState.IsPointOverHorizontalThumb(mousePos, nodeRect);

        // Handle vertical scrollbar interaction
        if (scrollState.ShowScrollbarY)
        {
            if (Input.IsMouseButtonPressed(MouseButton.Left) && !IsHoverBlocked(node) &&
                scrollState.IsPointOverVerticalThumb(mousePos, nodeRect))
            {
                scrollState.IsDraggingScrollbarY = true;
                scrollState.DragStartMousePos = mousePos;
                scrollState.DragStartScrollOffset = scrollState.ScrollOffset;
            }

            if (scrollState.IsDraggingScrollbarY)
            {
                if (Input.IsMouseButtonDown(MouseButton.Left))
                {
                    scrollState.HandleVerticalScrollbarDrag(mousePos, nodeRect);
                    SetLocalScrollOffset(scrollState.ScrollOffset, node.Scope);
                }
                else
                {
                    scrollState.IsDraggingScrollbarY = false;
                }
            }
        }

        // Handle horizontal scrollbar interaction
        if (scrollState.ShowScrollbarX)
        {
            if (Input.IsMouseButtonPressed(MouseButton.Left) && !IsHoverBlocked(node) &&
                scrollState.IsPointOverHorizontalThumb(mousePos, nodeRect))
            {
                scrollState.IsDraggingScrollbarX = true;
                scrollState.DragStartMousePos = mousePos;
                scrollState.DragStartScrollOffset = scrollState.ScrollOffset;
            }

            if (scrollState.IsDraggingScrollbarX)
            {
                if (Input.IsMouseButtonDown(MouseButton.Left))
                {
                    scrollState.HandleHorizontalScrollbarDrag(mousePos, nodeRect);
                    SetLocalScrollOffset(scrollState.ScrollOffset, node.Scope);
                }
                else
                {
                    scrollState.IsDraggingScrollbarX = false;
                }
            }
        }
    }

    /// <summary>
    /// Draws the scrollbars into a raised, out-of-flow node. Drawing them into the container itself
    /// put them under its own content, which paints later in the flat z-ordered pass.
    /// </summary>
    void DrawScrollbars(LayoutNode node, ScrollState scrollState, bool scrollX, bool scrollY,
        Color? foregroundColor, Color? backgroundColor)
    {
        if (!(scrollX && scrollState.ShowScrollbarX) && !(scrollY && scrollState.ShowScrollbarY)) return;

        using (Node(-1, -1, $"{node.Id}/scrollbars").AbsoluteScreen(0, 0).Enter())
        {
            SetZIndex(ScrollbarZIndex);

            if (scrollX && scrollState.ShowScrollbarX)
                DrawScrollbar(node, scrollState, Axis.Horizontal, foregroundColor, backgroundColor);
            if (scrollY && scrollState.ShowScrollbarY)
                DrawScrollbar(node, scrollState, Axis.Vertical, foregroundColor, backgroundColor);
        }
    }

    /// <summary>Where scrollbars draw: above their container's content, below popups and drag ghosts.</summary>
    const int ScrollbarZIndex = 2_000;

    void DrawScrollbar(LayoutNode node, ScrollState scrollState, Axis axis, Color? foregroundColor,
        Color? backgroundColor)
    {
        var shouldShow = axis == Axis.Vertical ? scrollState.ShowScrollbarY : scrollState.ShowScrollbarX;
        if (!shouldShow) return;

        // The container's own rect, not its content box: a scrollbar belongs on the border, and the
        // padding then applies to what is left.
        var nodeRect = node.Rect;
        var (track, thumb) = axis == Axis.Vertical
            ? scrollState.CalculateVerticalScrollbar(nodeRect)
            : scrollState.CalculateHorizontalScrollbar(nodeRect);

        var bgColor = backgroundColor ?? Controls.BaseBackground;
        var isDragging = axis == Axis.Vertical ? scrollState.IsDraggingScrollbarY : scrollState.IsDraggingScrollbarX;
        var isHovered = axis == Axis.Vertical
            ? scrollState.IsVerticalScrollbarHovered
            : scrollState.IsHorizontalScrollbarHovered;

        // Use different colors based on interaction state
        var fgColor = foregroundColor ?? (isDragging ? Controls.TextDim :
            isHovered ? Controls.BorderActive :
            Controls.Border);

        // Draw scrollbar background
        DrawRectFilled(track, bgColor);

        // Draw scrollbar thumb with rounded corners
        var shape = Shape.RoundRect(thumb.X, thumb.Y, thumb.X + thumb.W, thumb.Y + thumb.H, 3f);
        shape.Paint!.Color = fgColor;
        AddDraw(shape);
    }

    /// <summary>
    /// Gets the scroll offset for the specified node.
    /// </summary>
    /// <param name="nodeId">The node ID to get scroll offset for</param>
    /// <returns>The scroll offset as Vector2</returns>
    Vector2 GetScrollOffset(string nodeId)
    {
        var scrollState = GetScrollState(nodeId);
        return scrollState?.ScrollOffset ?? Vector2.Zero;
    }

    /// <summary>
    /// Sets the scroll offset for the specified node.
    /// </summary>
    /// <param name="nodeId">The node ID to set scroll offset for</param>
    /// <param name="offset">The scroll offset to set</param>
    void SetScrollOffset(string nodeId, Vector2 offset)
    {
        var scrollState = GetOrCreateScrollState(nodeId);
        scrollState.ScrollOffset = offset;
        scrollState.ClampScrollPosition();

        // Update local scroll offset in node scope if the node exists
        if (RootNode != null)
        {
            var node = FindNodeById(RootNode, nodeId);
            if (node?.Scope != null) SetLocalScrollOffset(scrollState.ScrollOffset, node.Scope);
        }
    }

    /// <summary>
    /// Scrolls the specified node to the top.
    /// </summary>
    /// <param name="nodeId">The node ID to scroll</param>
    public void ScrollToTop(string nodeId)
    {
        SetScrollOffset(nodeId, new Vector2(GetScrollOffset(nodeId).X, 0));
    }

    /// <summary>
    /// Scrolls the specified node to the bottom.
    /// </summary>
    /// <param name="nodeId">The node ID to scroll</param>
    public void ScrollToBottom(string nodeId)
    {
        var scrollState = GetScrollState(nodeId);
        if (scrollState != null)
        {
            var maxScrollY = Math.Max(0, scrollState.ContentSize.Y - scrollState.ViewportSize.Y);
            SetScrollOffset(nodeId, new Vector2(scrollState.ScrollOffset.X, maxScrollY));
        }
    }

    /// <summary>
    /// Scrolls the specified node to the left.
    /// </summary>
    /// <param name="nodeId">The node ID to scroll</param>
    public void ScrollToLeft(string nodeId)
    {
        SetScrollOffset(nodeId, new Vector2(0, GetScrollOffset(nodeId).Y));
    }

    /// <summary>
    /// Scrolls the specified node to the right.
    /// </summary>
    /// <param name="nodeId">The node ID to scroll</param>
    public void ScrollToRight(string nodeId)
    {
        var scrollState = GetScrollState(nodeId);
        if (scrollState != null)
        {
            var maxScrollX = Math.Max(0, scrollState.ContentSize.X - scrollState.ViewportSize.X);
            SetScrollOffset(nodeId, new Vector2(maxScrollX, scrollState.ScrollOffset.Y));
        }
    }

    /// <summary>
    /// Scrolls the specified node by the given amount.
    /// </summary>
    /// <param name="nodeId">The node ID to scroll</param>
    /// <param name="delta">The amount to scroll by</param>
    public void ScrollBy(string nodeId, Vector2 delta)
    {
        var currentOffset = GetScrollOffset(nodeId);
        SetScrollOffset(nodeId, currentOffset + delta);
    }

    /// <summary>
    /// Gets whether the specified node can scroll in the given direction.
    /// </summary>
    /// <param name="nodeId">The node ID to check</param>
    /// <param name="axis">The axis to check (Horizontal or Vertical)</param>
    /// <returns>True if the node can scroll in the specified direction</returns>
    public bool CanScroll(string nodeId, Axis axis)
    {
        var scrollState = GetScrollState(nodeId);
        if (scrollState == null) return false;

        return axis == Axis.Horizontal
            ? scrollState.NeedsHorizontalScroll
            : scrollState.NeedsVerticalScroll;
    }

    /// <summary>
    /// Gets the scroll percentage for the specified node and axis (0.0 to 1.0).
    /// </summary>
    /// <param name="nodeId">The node ID to get scroll percentage for</param>
    /// <param name="axis">The axis to get percentage for</param>
    /// <returns>The scroll percentage from 0.0 to 1.0</returns>
    public float GetScrollPercentage(string nodeId, Axis axis)
    {
        var scrollState = GetScrollState(nodeId);
        if (scrollState == null) return 0f;

        if (axis == Axis.Horizontal)
        {
            var maxScrollX = Math.Max(0, scrollState.ContentSize.X - scrollState.ViewportSize.X);
            return maxScrollX > 0 ? scrollState.ScrollOffset.X / maxScrollX : 0f;
        }
        else
        {
            var maxScrollY = Math.Max(0, scrollState.ContentSize.Y - scrollState.ViewportSize.Y);
            return maxScrollY > 0 ? scrollState.ScrollOffset.Y / maxScrollY : 0f;
        }
    }

    /// <summary>
    /// Sets the scroll percentage for the specified node and axis (0.0 to 1.0).
    /// </summary>
    /// <param name="nodeId">The node ID to set scroll percentage for</param>
    /// <param name="axis">The axis to set percentage for</param>
    /// <param name="percentage">The scroll percentage from 0.0 to 1.0</param>
    public void SetScrollPercentage(string nodeId, Axis axis, float percentage)
    {
        var scrollState = GetScrollState(nodeId);
        if (scrollState == null) return;

        percentage = Math.Clamp(percentage, 0f, 1f);

        if (axis == Axis.Horizontal)
        {
            var maxScrollX = Math.Max(0, scrollState.ContentSize.X - scrollState.ViewportSize.X);
            var newOffset = new Vector2(maxScrollX * percentage, scrollState.ScrollOffset.Y);
            SetScrollOffset(nodeId, newOffset);
        }
        else
        {
            var maxScrollY = Math.Max(0, scrollState.ContentSize.Y - scrollState.ViewportSize.Y);
            var newOffset = new Vector2(scrollState.ScrollOffset.X, maxScrollY * percentage);
            SetScrollOffset(nodeId, newOffset);
        }
    }

    /// <summary>
    /// Finds a node by ID in the layout tree.
    /// </summary>
    /// <param name="root">The root node to start searching from</param>
    /// <param name="nodeId">The ID to search for</param>
    /// <returns>The node if found, null otherwise</returns>
    LayoutNode? FindNodeById(LayoutNode root, string nodeId)
    {
        if (root.Id == nodeId) return root;

        foreach (var child in root.Children)
        {
            var found = FindNodeById(child, nodeId);
            if (found != null) return found;
        }

        return null;
    }
}
