namespace Guinevere;

/// <summary>The colors of a text editor.</summary>
public sealed record TextEditorTheme
{
    /// <summary>Colors in the style of a dark editor.</summary>
    public static TextEditorTheme Dark { get; } = new();

    /// <summary>Colors in the style of a light editor.</summary>
    public static TextEditorTheme Light { get; } = new()
    {
        Background = Color.FromArgb(255, 255, 255, 255),
        Text = Color.FromArgb(255, 30, 30, 30),
        Gutter = Color.FromArgb(255, 245, 245, 245),
        LineNumber = Color.FromArgb(255, 160, 160, 160),
        ActiveLineNumber = Color.FromArgb(255, 30, 30, 30),
        CurrentLine = Color.FromArgb(255, 240, 244, 250),
        Selection = Color.FromArgb(255, 173, 214, 255),
        Caret = Color.FromArgb(255, 0, 0, 0),
        FindMatch = Color.FromArgb(110, 255, 200, 0),
        BracketMatch = Color.FromArgb(70, 0, 100, 200),
        Popup = Color.FromArgb(255, 250, 250, 250),
        PopupBorder = Color.FromArgb(255, 200, 200, 200),
        PopupSelection = Color.FromArgb(255, 210, 228, 255),
        Scrollbar = Color.FromArgb(70, 100, 100, 100),
        Syntax = SyntaxTheme.Light,
    };

    /// <summary>Behind the text.</summary>
    public Color Background { get; init; } = Color.FromArgb(255, 30, 30, 30);

    /// <summary>Text without a syntax color.</summary>
    public Color Text { get; init; } = Color.FromArgb(255, 212, 212, 212);

    /// <summary>Behind the line numbers.</summary>
    public Color Gutter { get; init; } = Color.FromArgb(255, 30, 30, 30);

    /// <summary>Line numbers.</summary>
    public Color LineNumber { get; init; } = Color.FromArgb(255, 110, 118, 129);

    /// <summary>The number of a line holding a caret.</summary>
    public Color ActiveLineNumber { get; init; } = Color.FromArgb(255, 200, 200, 200);

    /// <summary>Behind a line holding a caret.</summary>
    public Color CurrentLine { get; init; } = Color.FromArgb(255, 40, 40, 44);

    /// <summary>Behind selected text.</summary>
    public Color Selection { get; init; } = Color.FromArgb(255, 38, 79, 120);

    /// <summary>The caret.</summary>
    public Color Caret { get; init; } = Color.FromArgb(255, 174, 175, 173);

    /// <summary>Behind find matches.</summary>
    public Color FindMatch { get; init; } = Color.FromArgb(110, 234, 92, 0);

    /// <summary>Behind the brackets matching the caret.</summary>
    public Color BracketMatch { get; init; } = Color.FromArgb(80, 120, 140, 160);

    /// <summary>Error squiggles.</summary>
    public Color Error { get; init; } = Color.FromArgb(255, 241, 76, 76);

    /// <summary>Warning squiggles.</summary>
    public Color Warning { get; init; } = Color.FromArgb(255, 205, 173, 0);

    /// <summary>Information squiggles.</summary>
    public Color Information { get; init; } = Color.FromArgb(255, 55, 148, 255);

    /// <summary>Hint squiggles.</summary>
    public Color Hint { get; init; } = Color.FromArgb(160, 160, 160, 160);

    /// <summary>Behind completion lists, hover information and the find bar.</summary>
    public Color Popup { get; init; } = Color.FromArgb(255, 37, 37, 38);

    /// <summary>Around popups.</summary>
    public Color PopupBorder { get; init; } = Color.FromArgb(255, 69, 69, 69);

    /// <summary>Behind the highlighted completion.</summary>
    public Color PopupSelection { get; init; } = Color.FromArgb(255, 4, 57, 94);

    /// <summary>The scrollbar thumb.</summary>
    public Color Scrollbar { get; init; } = Color.FromArgb(90, 121, 121, 121);

    /// <summary>Token colors.</summary>
    public SyntaxTheme Syntax { get; init; } = SyntaxTheme.Dark;

    /// <summary>The squiggle color of a severity.</summary>
    public Color ColorOf(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => Error,
        DiagnosticSeverity.Warning => Warning,
        DiagnosticSeverity.Information => Information,
        _ => Hint
    };
}

/// <summary>How a text editor looks.</summary>
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

    /// <summary>The colors.</summary>
    public TextEditorTheme Theme { get; init; } = TextEditorTheme.Dark;
}

/// <summary>What a text editor reports for the frame.</summary>
/// <param name="Node">The editor's node.</param>
/// <param name="Changed">Whether the text changed this frame.</param>
public readonly record struct TextEditorResult(LayoutNode Node, bool Changed);
