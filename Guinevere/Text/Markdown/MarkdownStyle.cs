namespace Guinevere;

/// <summary>
/// Fonts, sizes, colors and spacing a Markdown document is drawn with. Changing the style restyles a document without
/// parsing it again; unset colors derive from the scope's text color.
/// </summary>
public sealed record MarkdownStyle
{
    static readonly float[] DefaultHeadingScale = [1.7f, 1.45f, 1.25f, 1.1f, 1f, 0.95f];

    /// <summary>Body font, or null for the scope's.</summary>
    public Font? Font { get; init; }
    /// <summary>Font for code, or null for the <see cref="FontRole.Code"/> role, else the body font.</summary>
    public Font? CodeFont { get; init; }
    /// <summary>Logical body size, or zero for the scope's.</summary>
    public float FontSize { get; init; }
    /// <summary>Line height as a multiple of the font size.</summary>
    public float LineHeight { get; init; } = 1.45f;
    /// <summary>Body color, or null for the scope's text color.</summary>
    public Color? Text { get; init; }
    /// <summary>List markers, quotes and unresolved image text, or null for a faded body color.</summary>
    public Color? Muted { get; init; }
    /// <summary>Links.</summary>
    public Color Link { get; init; } = Color.FromArgb(255, 110, 170, 255);
    /// <summary>Behind code blocks, or null for a faint body color.</summary>
    public Color? CodeBackground { get; init; }
    /// <summary>Behind inline code, or null for a faint body color.</summary>
    public Color? InlineCodeBackground { get; init; }
    /// <summary>Quote bars and rules, or null for a faded body color.</summary>
    public Color? Rule { get; init; }
    /// <summary>Heading sizes as multiples of the body size, for levels 1 to 6.</summary>
    public IReadOnlyList<float> HeadingScale { get; init; } = DefaultHeadingScale;
    /// <summary>Space between blocks, in ems.</summary>
    public float BlockSpacing { get; init; } = 0.6f;
    /// <summary>Indent of each list level and width of its marker, in ems.</summary>
    public float ListIndent { get; init; } = 1.4f;
    /// <summary>Indent of each quote level, in ems.</summary>
    public float QuoteIndent { get; init; } = 1f;
    /// <summary>Padding inside code blocks, in ems.</summary>
    public float CodePadding { get; init; } = 0.6f;
    /// <summary>How code block lines wrap; <see cref="TextWrapMode.None"/> keeps them whole.</summary>
    public TextWrapMode CodeWrap { get; init; } = TextWrapMode.WordThenCharacter;
    /// <summary>Colors of highlighted code.</summary>
    public SyntaxTheme Syntax { get; init; } = SyntaxTheme.Dark;
    /// <summary>Picks a highlighter for a fenced block's language; null leaves code plain.</summary>
    public Func<string?, ISyntaxHighlighter?> Highlighter { get; init; } = SyntaxHighlighting.ForLanguage;

    /// <summary>The style with unset colors filled from <paramref name="text"/>.</summary>
    internal MarkdownColors Colors(Color text)
    {
        var body = Text ?? text;
        return new MarkdownColors(body, Muted ?? new Color(body, 0.65f), Link, CodeBackground ?? new Color(body, 0.07f),
            InlineCodeBackground ?? new Color(body, 0.12f), Rule ?? new Color(body, 0.25f));
    }
}

/// <summary>The resolved colors of a <see cref="MarkdownStyle"/>.</summary>
readonly record struct MarkdownColors(Color Text, Color Muted, Color Link, Color CodeBackground, Color InlineCode,
    Color Rule);
