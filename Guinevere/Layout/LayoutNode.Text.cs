namespace Guinevere;

/// <summary>Fluent text layout options inherited by text drawn in this node and its descendants.</summary>
public partial class LayoutNode
{
    /// <summary>Sets how text in this node and its descendants wraps.</summary>
    public LayoutNode TextWrap(TextWrapMode mode) => WithTextLayout(InheritedTextLayout with { WrapMode = mode });

    /// <summary>Sets the line box height, as a multiple of the font size, for text in this subtree.</summary>
    public LayoutNode LineHeight(float multiple)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(multiple);
        return WithTextLayout(InheritedTextLayout with { LineHeight = multiple });
    }

    /// <summary>Limits text in this subtree to <paramref name="count"/> lines, zero for none, ending in a marker.</summary>
    public LayoutNode MaxLines(int count, string? ellipsis = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var options = InheritedTextLayout;
        return WithTextLayout(options with { MaxLines = count, Ellipsis = ellipsis ?? options.Ellipsis });
    }

    TextLayoutOptions InheritedTextLayout => Scope.Get<LayoutNodeScopeTextLayout>().Value;

    LayoutNode WithTextLayout(TextLayoutOptions options)
    {
        Scope.Set(new LayoutNodeScopeTextLayout { Value = options });
        return this;
    }
}
