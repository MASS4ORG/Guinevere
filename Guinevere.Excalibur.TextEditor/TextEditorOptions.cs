namespace Guinevere;

/// <summary>How a text editor lays out; its colors come from the sheets' <c>editor-*</c> and <c>syntax-*</c> rules.</summary>
public sealed record TextEditorOptions
{
    /// <summary>The defaults.</summary>
    public static TextEditorOptions Default { get; } = new();

    /// <summary>Logical font size, or zero for the scope's.</summary>
    public float FontSize { get; init; }

    /// <summary>The font, or null for the code font role and then the scope's font.</summary>
    public Font? Font { get; init; }

    /// <summary>Line height as a multiple of the font size.</summary>
    public float LineHeight { get; init; } = 1.45f;

    /// <summary>Whether the gutter shows line numbers.</summary>
    public bool LineNumbers { get; init; } = true;

    /// <summary>Whether the gutter shows fold markers.</summary>
    public bool FoldMarkers { get; init; } = true;

    /// <summary>Whether lines holding a caret are highlighted.</summary>
    public bool HighlightCurrentLine { get; init; } = true;
}

/// <summary>What a text editor reports for the frame.</summary>
/// <param name="Node">The editor's node.</param>
/// <param name="Changed">Whether the text changed this frame.</param>
public readonly record struct TextEditorResult(LayoutNode Node, bool Changed);
