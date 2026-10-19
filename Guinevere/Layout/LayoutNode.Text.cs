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

    /// <summary>Adds space after every character in this subtree, in ems of the character's font size.</summary>
    public LayoutNode LetterSpacing(float ems) => WithTextLayout(InheritedTextLayout with { LetterSpacing = ems });

    /// <summary>Adds space to every space character in this subtree, in ems of its font size.</summary>
    public LayoutNode WordSpacing(float ems) => WithTextLayout(InheritedTextLayout with { WordSpacing = ems });

    /// <summary>Sets tab stops: explicit positions in logical pixels first, then every <paramref name="size"/> spaces.</summary>
    public LayoutNode TabStops(float size, params float[] stops)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        ArgumentNullException.ThrowIfNull(stops);
        return WithTextLayout(InheritedTextLayout with { TabSize = size, TabStops = [.. stops.Order()] });
    }

    /// <summary>Indents each paragraph's first line, and the lines after it, by logical pixels.</summary>
    public LayoutNode TextIndent(float firstLine, float hanging = 0f) =>
        WithTextLayout(InheritedTextLayout with { FirstLineIndent = firstLine, HangingIndent = hanging });

    /// <summary>Places lines in this subtree; a justified paragraph's last line uses <paramref name="lastLine"/>.</summary>
    public LayoutNode TextAlign(TextAlignment alignment, TextAlignment lastLine = TextAlignment.Start) =>
        WithTextLayout(InheritedTextLayout with { Alignment = alignment, LastLineAlignment = lastLine });

    /// <summary>Draws text in this subtree in another letter case without changing its source.</summary>
    public LayoutNode TextCase(TextCase textCase) =>
        WithTextLayout(InheritedTextLayout with { Case = textCase });

    /// <summary>Turns font pair kerning on or off for text in this subtree.</summary>
    public LayoutNode Kerning(bool enabled = true) => WithTextLayout(InheritedTextLayout with { Kerning = enabled });

    /// <summary>Draws <paramref name="placeholder"/> for characters no font has, or null for the font's own box.</summary>
    public LayoutNode MissingGlyph(string? placeholder) =>
        WithTextLayout(InheritedTextLayout with { MissingGlyph = placeholder });

    TextLayoutOptions InheritedTextLayout => Scope.Get<LayoutNodeScopeTextLayout>().Value;

    LayoutNode WithTextLayout(TextLayoutOptions options)
    {
        Scope.Set(new LayoutNodeScopeTextLayout { Value = options });
        return this;
    }
}
