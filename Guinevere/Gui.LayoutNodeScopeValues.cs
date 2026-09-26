namespace Guinevere;

public partial class Gui
{
    /// <summary>
    /// Sets the text color for the current node and its children.
    /// The color is automatically restored when exiting the node scope.
    /// </summary>
    public void SetTextColor(Color color, LayoutNodeScope? scope = null)
    {
        scope ??= CurrentNodeScope;
        scope.Set(new LayoutNodeScopeTextColor { Value = color });
    }

    /// <summary>
    /// Sets the text size for the current node and its children.
    /// The size is automatically restored when exiting the node scope.
    /// </summary>
    public void SetTextSize(float size, LayoutNodeScope? scope = null)
    {
        scope ??= CurrentNodeScope;
        scope.Set(new LayoutNodeScopeTextSize { Value = size });
    }

    /// <summary>
    /// Sets the text font for the current node and its children using the Font wrapper.
    /// The font is automatically restored when exiting the node scope.
    /// </summary>
    public void SetTextFont(Font font, LayoutNodeScope? scope = null)
    {
        scope ??= CurrentNodeScope;
        scope.Set(new LayoutNodeScopeTextFont { Value = font });
    }

    /// <summary>Sets inherited text layout options for this scope and its children.</summary>
    public void SetTextLayout(TextLayoutOptions layout, LayoutNodeScope? scope = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        scope ??= CurrentNodeScope;
        scope.Set(new LayoutNodeScopeTextLayout { Value = layout });
    }

    /// <summary>
    /// Sets the legacy icon font for explicit glyphs and text fallback in this scope.
    /// The font is automatically restored when exiting the node scope.
    /// </summary>
    public void SetIconFont(Font font, LayoutNodeScope? scope = null)
    {
        scope ??= CurrentNodeScope;
        scope.Set(new LayoutNodeScopeIconFont { Value = font });
        scope.Set(new LayoutNodeScopeWidgetIconFont { Value = font });
    }

    /// <summary>Sets only the emoji and unsupported-character fallback font.</summary>
    public void SetEmojiFont(Font font, LayoutNodeScope? scope = null)
    {
        scope ??= CurrentNodeScope;
        scope.Set(new LayoutNodeScopeIconFont { Value = font });
    }

    /// <summary>Sets the UI icon font for this scope and its children.</summary>
    public void SetWidgetIconFont(Font font, LayoutNodeScope? scope = null)
    {
        scope ??= CurrentNodeScope;
        scope.Set(new LayoutNodeScopeWidgetIconFont { Value = font });
    }

    /// <summary>
    /// Sets the Z-index for the current node and its children.
    /// The Z-index is automatically restored when exiting the node scope.
    /// </summary>
    public void SetZIndex(int index, LayoutNodeScope? scope = null)
    {
        scope ??= CurrentNodeScope;
        scope.Set(new LayoutNodeScopeZIndex { Value = index });
    }

    /// <summary>
    /// Sets the scroll container ID for the current scope, marking it as a scrollable container.
    /// </summary>
    public void SetScrollContainer(string containerId, LayoutNodeScope? scope = null)
    {
        scope ??= CurrentNodeScope;
        scope.Set(new LayoutNodeScopeScrollContainerId { Value = containerId });
    }

    /// <summary>
    /// Sets the cumulative scroll offset for the current scope.
    /// </summary>
    public void SetCumulativeScrollOffset(Vector2 offset, LayoutNodeScope? scope = null)
    {
        scope ??= CurrentNodeScope;
        scope.Set(new LayoutNodeScopeCumulativeScrollOffset { Value = offset });
    }

    /// <summary>
    /// Marks the current scope as a scrollable container.
    /// </summary>
    public void SetIsScrollContainer(bool isScrollContainer, LayoutNodeScope? scope = null)
    {
        scope ??= CurrentNodeScope;
        scope.Set(new LayoutNodeScopeIsScrollContainer { Value = isScrollContainer });
    }

    /// <summary>
    /// Sets the local scroll offset for the current scope.
    /// </summary>
    public void SetLocalScrollOffset(Vector2 offset, LayoutNodeScope? scope = null)
    {
        scope ??= CurrentNodeScope;
        scope.Set(new LayoutNodeScopeLocalScrollOffset { Value = offset });
        scope.Node.InvalidateLayout();
    }

    /// <summary>
    /// Sets the clipping state for the current scope.
    /// </summary>
    public void SetClipped(bool isClipped, LayoutNodeScope? scope = null)
    {
        scope ??= CurrentNodeScope;
        scope.Set(new LayoutNodeScopeIsClipped { Value = isClipped });
    }

    /// <summary>Lets an overlay draw outside scrollable or clipped layout ancestors.</summary>
    public void SetEscapesAncestorClips(bool escapes = true, LayoutNodeScope? scope = null)
    {
        scope ??= CurrentNodeScope;
        scope.Set(new LayoutNodeScopeEscapesAncestorClips { Value = escapes });
    }
}
