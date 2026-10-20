namespace Guinevere;

/// <summary>What a highlighted token is, for themes to style.</summary>
public enum SyntaxKind
{
    /// <summary>Text with no special meaning.</summary>
    Plain,
    /// <summary>A reserved word.</summary>
    Keyword,
    /// <summary>A type name.</summary>
    Type,
    /// <summary>A function or method name at a call or declaration.</summary>
    Function,
    /// <summary>A string or character literal.</summary>
    String,
    /// <summary>A numeric literal.</summary>
    Number,
    /// <summary>A comment.</summary>
    Comment,
    /// <summary>An operator.</summary>
    Operator,
    /// <summary>Brackets, separators and terminators.</summary>
    Punctuation,
    /// <summary>A preprocessor directive or decorator.</summary>
    Preprocessor,
    /// <summary>A named constant such as <c>true</c> or <c>null</c>.</summary>
    Constant,
}

/// <summary>A highlighted range of one line.</summary>
/// <param name="Start">Offset of the token in the line.</param>
/// <param name="Length">UTF-16 length of the token.</param>
/// <param name="Kind">What the token is.</param>
public readonly record struct SyntaxToken(int Start, int Length, SyntaxKind Kind);

/// <summary>
/// Tokenizes source code one line at a time. The state returned for a line is passed to the next one, so a
/// multi-line comment or string carries over; an editor re-tokenizes only from an edited line until the state it
/// reaches matches the stored one. State zero is the start of a document.
/// </summary>
public interface ISyntaxHighlighter
{
    /// <summary>Appends the tokens of <paramref name="line"/> and returns the state at its end.</summary>
    /// <param name="line">One line without its line break.</param>
    /// <param name="state">The state at the start of the line.</param>
    /// <param name="tokens">Receives tokens in order; unlisted ranges are <see cref="SyntaxKind.Plain"/>.</param>
    int Tokenize(ReadOnlySpan<char> line, int state, List<SyntaxToken> tokens);
}

/// <summary>Span styles for each token kind; kinds without an entry draw as plain text.</summary>
/// <param name="Styles">The style of each kind.</param>
public sealed record SyntaxTheme(IReadOnlyDictionary<SyntaxKind, TextSpanStyle> Styles)
{
    /// <summary>A theme for dark backgrounds.</summary>
    public static SyntaxTheme Dark { get; } = new(new Dictionary<SyntaxKind, TextSpanStyle>
    {
        [SyntaxKind.Keyword] = TextSpanStyle.Colored(Color.FromArgb(255, 86, 156, 214)),
        [SyntaxKind.Type] = TextSpanStyle.Colored(Color.FromArgb(255, 78, 201, 176)),
        [SyntaxKind.Function] = TextSpanStyle.Colored(Color.FromArgb(255, 220, 220, 170)),
        [SyntaxKind.String] = TextSpanStyle.Colored(Color.FromArgb(255, 206, 145, 120)),
        [SyntaxKind.Number] = TextSpanStyle.Colored(Color.FromArgb(255, 181, 206, 168)),
        [SyntaxKind.Comment] = new TextSpanStyle { Color = Color.FromArgb(255, 106, 153, 85), Italic = true },
        [SyntaxKind.Preprocessor] = TextSpanStyle.Colored(Color.FromArgb(255, 197, 134, 192)),
        [SyntaxKind.Constant] = TextSpanStyle.Colored(Color.FromArgb(255, 86, 156, 214)),
    });

    /// <summary>A theme for light backgrounds.</summary>
    public static SyntaxTheme Light { get; } = new(new Dictionary<SyntaxKind, TextSpanStyle>
    {
        [SyntaxKind.Keyword] = TextSpanStyle.Colored(Color.FromArgb(255, 0, 0, 255)),
        [SyntaxKind.Type] = TextSpanStyle.Colored(Color.FromArgb(255, 38, 127, 153)),
        [SyntaxKind.Function] = TextSpanStyle.Colored(Color.FromArgb(255, 121, 94, 38)),
        [SyntaxKind.String] = TextSpanStyle.Colored(Color.FromArgb(255, 163, 21, 21)),
        [SyntaxKind.Number] = TextSpanStyle.Colored(Color.FromArgb(255, 9, 134, 88)),
        [SyntaxKind.Comment] = new TextSpanStyle { Color = Color.FromArgb(255, 0, 128, 0), Italic = true },
        [SyntaxKind.Preprocessor] = TextSpanStyle.Colored(Color.FromArgb(255, 175, 0, 219)),
        [SyntaxKind.Constant] = TextSpanStyle.Colored(Color.FromArgb(255, 0, 0, 255)),
    });

    /// <summary>The style of a kind, or null for plain text.</summary>
    public TextSpanStyle? StyleOf(SyntaxKind kind) => Styles.TryGetValue(kind, out var style) ? style : null;
}

/// <summary>Built-in highlighters by language name, and conversion of highlighted code to rich text.</summary>
public static class SyntaxHighlighting
{
    static readonly Dictionary<string, ISyntaxHighlighter> Languages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["csharp"] = CLikeSyntax.CSharp,
        ["cs"] = CLikeSyntax.CSharp,
        ["c#"] = CLikeSyntax.CSharp,
        ["javascript"] = CLikeSyntax.JavaScript,
        ["js"] = CLikeSyntax.JavaScript,
        ["typescript"] = CLikeSyntax.JavaScript,
        ["ts"] = CLikeSyntax.JavaScript,
        ["glsl"] = CLikeSyntax.Shader,
        ["hlsl"] = CLikeSyntax.Shader,
        ["shader"] = CLikeSyntax.Shader,
        ["sksl"] = CLikeSyntax.Shader,
        ["json"] = CLikeSyntax.Json,
        ["python"] = CLikeSyntax.Python,
        ["py"] = CLikeSyntax.Python,
    };

    /// <summary>The highlighter for a language name or alias, or null when none is registered.</summary>
    public static ISyntaxHighlighter? ForLanguage(string? language) =>
        language is not null && Languages.TryGetValue(language.Trim(), out var highlighter) ? highlighter : null;

    /// <summary>Registers or replaces the highlighter used for a language name.</summary>
    public static void Register(string language, ISyntaxHighlighter highlighter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentNullException.ThrowIfNull(highlighter);
        Languages[language] = highlighter;
    }

    /// <summary>Highlights a whole snippet into rich text, carrying state from line to line.</summary>
    public static RichText ToRichText(string code, ISyntaxHighlighter? highlighter, SyntaxTheme theme)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(theme);
        var text = new RichText(code);
        if (highlighter is null)
        {
            return text;
        }

        var tokens = new List<SyntaxToken>();
        var state = 0;
        var lineStart = 0;
        while (lineStart <= code.Length)
        {
            var newline = code.IndexOf('\n', lineStart);
            var lineEnd = newline < 0 ? code.Length : newline;
            tokens.Clear();
            state = highlighter.Tokenize(code.AsSpan(lineStart, lineEnd - lineStart), state, tokens);
            foreach (var token in tokens)
            {
                if (theme.StyleOf(token.Kind) is { } style && token.Length > 0)
                {
                    text.Style(lineStart + token.Start, token.Length, style);
                }
            }

            lineStart = lineEnd + 1;
        }

        return text;
    }
}
