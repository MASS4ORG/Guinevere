namespace Guinevere;

public partial class Gui
{
    private readonly Dictionary<string, ScrollState> _scrollStates = new();

    /// <summary>
    /// Enables horizontal scrolling for the current node.
    /// </summary>
    /// <param name="foregroundColor">Color of the scrollbar thumb</param>
    /// <param name="backgroundColor">Color of the scrollbar track</param>
    /// <returns>The current node for chaining</returns>
    public LayoutNode ScrollX(Color? foregroundColor = null, Color? backgroundColor = null)
    {
        return ScrollContainer(scrollX: true, scrollY: false, foregroundColor, backgroundColor);
    }

    /// <summary>
    /// Enables vertical scrolling for the current node.
    /// </summary>
    /// <param name="foregroundColor">Color of the scrollbar thumb</param>
    /// <param name="backgroundColor">Color of the scrollbar track</param>
    /// <returns>The current noden for chaining</returns>
    public LayoutNode ScrollY(Color? foregroundColor = null, Color? backgroundColor = null)
    {
        return ScrollContainer(scrollX: false, scrollY: true, foregroundColor, backgroundColor);
    }

    /// <summary>
    /// Enables both horizontal and vertical scrolling for the current node.
    /// </summary>
    /// <param name="foregroundColor">Color of the scrollbar thumb</param>
    /// <param name="backgroundColor">Color of the scrollbar track</param>
    /// <returns>The current node for chaining</returns>
    public LayoutNode Scroll(Color? foregroundColor = null, Color? backgroundColor = null)
    {
        return ScrollContainer(scrollX: true, scrollY: true, foregroundColor, backgroundColor);
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
            {
                SetClipped(true, CurrentNode.Scope);
            }
            return;
        }

        if (Pass != Pass.Pass2Render) return;

        // For scrollable containers, we need to clip to the viewport bounds
        // not the scrolled content bounds
        var scrollState = GetScrollState(CurrentNode.Id);
        if (scrollState != null && (scrollState.IsScrollingX || scrollState.IsScrollingY))
        {
            // Ensure the node's layout is finalized before clipping
            var clipRect = CurrentNode.InnerRect;

            // Only apply clipping if the rectangle has valid dimensions
            if (clipRect.W > 0 && clipRect.H > 0)
            {
                SetClipped(true, CurrentNode.Scope);
                AddDraw(new ClipOperation(clipRect));
            }
        }
        else
        {
            // Non-scrollable content still needs basic clipping
            var clipRect = CurrentNode.InnerRect;
            if (clipRect.W > 0 && clipRect.H > 0)
            {
                AddDraw(new ClipOperation(clipRect));
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

        // Update local scroll offset in node scope
        SetLocalScrollOffset(scrollState.ScrollOffset, node.Scope);

        if (Pass == Pass.Pass2Render)
        {
            // Handle input and draw scrollbars
            HandleScrollInput(node, scrollState);

            if (scrollX && scrollState.ShowScrollbarX)
                DrawScrollbar(node, scrollState, Axis.Horizontal, foregroundColor, backgroundColor);
            if (scrollY && scrollState.ShowScrollbarY)
                DrawScrollbar(node, scrollState, Axis.Vertical, foregroundColor, backgroundColor);

            // Apply clipping after all input handling and layout is complete
            ClipContent();
        }
        else if (Pass == Pass.Pass1Build)
        {
            // During build pass, just mark that this node will need clipping
            SetClipped(true, CurrentNode.Scope);
        }

        return node;
    }

    private ScrollState GetOrCreateScrollState(string nodeId)
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

    private void HandleScrollInput(LayoutNode node, ScrollState scrollState)
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


        // Handle mouse wheel scrolling only when mouse is over the node
        var screenRect = new Rect(nodeRect.X, nodeRect.Y, nodeRect.W, nodeRect.H);
        if (screenRect.Contains(mousePos))
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

    private void UpdateContentSize(LayoutNode node, ScrollState scrollState)
    {
        var maxX = 0f;
        var maxY = 0f;
        var minX = float.MaxValue;
        var minY = float.MaxValue;

        if (node.Children.Count > 0)
        {
            foreach (var child in node.Children)
            {
                // Calculate absolute bounds of all children
                minX = Math.Min(minX, child.Rect.X);
                minY = Math.Min(minY, child.Rect.Y);
                maxX = Math.Max(maxX, child.Rect.X + child.Rect.W);
                maxY = Math.Max(maxY, child.Rect.Y + child.Rect.H);
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

    private void HandleScrollbarDragging(LayoutNode node, ScrollState scrollState, Vector2 mousePos)
    {
        var nodeRect = node.InnerRect;

        // Update hover states
        scrollState.IsVerticalScrollbarHovered = scrollState.ShowScrollbarY &&
                                                 scrollState.IsPointOverVerticalThumb(mousePos, nodeRect);
        scrollState.IsHorizontalScrollbarHovered = scrollState.ShowScrollbarX &&
                                                   scrollState.IsPointOverHorizontalThumb(mousePos, nodeRect);

        // Handle vertical scrollbar interaction
        if (scrollState.ShowScrollbarY)
        {
            if (Input.IsMouseButtonPressed(MouseButton.Left) &&
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
            if (Input.IsMouseButtonPressed(MouseButton.Left) &&
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

    private void DrawScrollbar(LayoutNode node, ScrollState scrollState, Axis axis, Color? foregroundColor,
        Color? backgroundColor)
    {
        var shouldShow = axis == Axis.Vertical ? scrollState.ShowScrollbarY : scrollState.ShowScrollbarX;
        if (!shouldShow) return;

        var nodeRect = node.InnerRect;
        var (track, thumb) = axis == Axis.Vertical
            ? scrollState.CalculateVerticalScrollbar(nodeRect)
            : scrollState.CalculateHorizontalScrollbar(nodeRect);

        var bgColor = backgroundColor ?? Color.FromArgb(180, 60, 60, 60);
        var isDragging = axis == Axis.Vertical ? scrollState.IsDraggingScrollbarY : scrollState.IsDraggingScrollbarX;
        var isHovered = axis == Axis.Vertical
            ? scrollState.IsVerticalScrollbarHovered
            : scrollState.IsHorizontalScrollbarHovered;

        // Use different colors based on interaction state
        var fgColor = foregroundColor ?? (isDragging ? Color.FromArgb(255, 160, 160, 160) :
            isHovered ? Color.FromArgb(240, 140, 140, 140) :
            Color.FromArgb(220, 120, 120, 120));

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
    private Vector2 GetScrollOffset(string nodeId)
    {
        var scrollState = GetScrollState(nodeId);
        return scrollState?.ScrollOffset ?? Vector2.Zero;
    }

    /// <summary>
    /// Sets the scroll offset for the specified node.
    /// </summary>
    /// <param name="nodeId">The node ID to set scroll offset for</param>
    /// <param name="offset">The scroll offset to set</param>
    private void SetScrollOffset(string nodeId, Vector2 offset)
    {
        var scrollState = GetOrCreateScrollState(nodeId);
        scrollState.ScrollOffset = offset;
        scrollState.ClampScrollPosition();

        // Update local scroll offset in node scope if the node exists
        if (RootNode != null)
        {
            var node = FindNodeById(RootNode, nodeId);
            if (node?.Scope != null)
            {
                SetLocalScrollOffset(scrollState.ScrollOffset, node.Scope);
            }
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
    private LayoutNode? FindNodeById(LayoutNode root, string nodeId)
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
