using System.Globalization;

namespace Guinevere;

enum StyleTokenKind { Number, Word, Color, Opaque, Open, Close, Comma, Operator, Negate }

enum StyleNumberUnit { None, Percent, Em }

/// <summary>One lexeme of a declaration value; <see cref="Start"/>/<see cref="End"/> index the value text.</summary>
readonly record struct StyleToken(StyleTokenKind Kind, int Start, int End, bool SpaceBefore,
    double Number = 0d, StyleNumberUnit Unit = StyleNumberUnit.None);

/// <summary>
/// Splits a <c>.pss</c> value into tokens. Strings, <c>url(…)</c>, <c>$tokens</c> and <c>@constants</c> stay opaque, so
/// their text is never evaluated.
/// </summary>
static class StyleExpressionLexer
{
    public static List<StyleToken> Tokenize(string text)
    {
        var tokens = new List<StyleToken>();
        var i = 0;
        while (i < text.Length)
        {
            var space = SkipSpace(text, ref i);
            if (i >= text.Length) break;
            var token = Next(text, i, space, tokens.Count == 0 ? null : tokens[^1]);
            tokens.Add(token);
            i = token.End;
        }
        return tokens;
    }

    static bool SkipSpace(string text, ref int i)
    {
        var start = i;
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        return i > start;
    }

    static StyleToken Next(string text, int i, bool space, StyleToken? previous)
    {
        if (VerbatimEnd(text, i) is { } verbatim) return new(StyleTokenKind.Opaque, i, verbatim, space);
        if (Punctuation(text[i]) is { } kind) return new(kind, i, i + 1, space);
        return text[i] switch
        {
            '#' => HashToken(text, i, space),
            '-' => Dash(text, i, space, previous),
            _ => Plain(text, i, space),
        };
    }

    static StyleToken Plain(string text, int i, bool space)
    {
        if (StartsNumber(text, i)) return NumberToken(text, i, space);
        return IsWordStart(text[i])
            ? new(StyleTokenKind.Word, i, WordEnd(text, i), space)
            : new(StyleTokenKind.Opaque, i, OpaqueEnd(text, i), space);
    }

    /// <summary>End of a string, <c>url(…)</c>, <c>$token</c> or <c>@constant</c> starting at <paramref name="i"/>.</summary>
    static int? VerbatimEnd(string text, int i)
    {
        if (text[i] is '"' or '\'') return StringEnd(text, i);
        if (IsUrl(text, i)) return UrlEnd(text, i);
        return text[i] is '$' or '@' ? WordEnd(text, i + 1) : null;
    }

    static StyleTokenKind? Punctuation(char c) => c switch
    {
        '(' => StyleTokenKind.Open,
        ')' => StyleTokenKind.Close,
        ',' => StyleTokenKind.Comma,
        '*' or '/' or '+' => StyleTokenKind.Operator,
        _ => null,
    };

    /// <summary>
    /// A dash after a separator starts a negative number, a word, or a negation such as <c>-$x</c>; otherwise it
    /// subtracts.
    /// </summary>
    static StyleToken Dash(string text, int i, bool space, StyleToken? previous)
    {
        var next = i + 1 < text.Length ? text[i + 1] : ' ';
        if (next == '-') return new(StyleTokenKind.Opaque, i, WordEnd(text, i + 2), space);
        if (!IsLeading(previous, space, next)) return new(StyleTokenKind.Operator, i, i + 1, space);
        if (StartsNumber(text, i + 1)) return NumberToken(text, i, space);
        return IsWordStart(next)
            ? new(StyleTokenKind.Word, i, WordEnd(text, i + 1), space)
            : new(StyleTokenKind.Negate, i, i + 1, space);
    }

    static bool IsLeading(StyleToken? previous, bool space, char next) =>
        previous is not { } before || before.Kind is StyleTokenKind.Open or StyleTokenKind.Comma
            or StyleTokenKind.Operator || space && !char.IsWhiteSpace(next);

    static StyleToken NumberToken(string text, int start, bool space)
    {
        var end = start + (text[start] == '-' ? 1 : 0);
        while (end < text.Length && (char.IsAsciiDigit(text[end]) || text[end] == '.')) end++;
        var number = double.Parse(text.AsSpan(start, end - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        if (end < text.Length && text[end] == '%')
            return new(StyleTokenKind.Number, start, end + 1, space, number, StyleNumberUnit.Percent);
        var unitEnd = WordEnd(text, end);
        return Unit(text.AsSpan(end, unitEnd - end)) is { } unit
            ? new(StyleTokenKind.Number, start, unitEnd, space, number, unit)
            : new(StyleTokenKind.Word, start, unitEnd, space);
    }

    /// <summary>Lengths are pixels, <c>em</c> or unitless; <c>deg</c> marks hues. Any other suffix makes a word.</summary>
    static StyleNumberUnit? Unit(ReadOnlySpan<char> suffix) => suffix switch
    {
        "" or "px" or "deg" => StyleNumberUnit.None,
        "em" => StyleNumberUnit.Em,
        _ => null,
    };

    static StyleToken HashToken(string text, int i, bool space)
    {
        var end = WordEnd(text, i + 1);
        var kind = Color.TryParseHex(text[i..end], out _) ? StyleTokenKind.Color : StyleTokenKind.Word;
        return new(kind, i, end, space);
    }

    static bool StartsNumber(string text, int i) =>
        i < text.Length && (char.IsAsciiDigit(text[i])
                            || text[i] == '.' && i + 1 < text.Length && char.IsAsciiDigit(text[i + 1]));

    static bool IsWordStart(char c) => char.IsLetter(c) || c == '_';

    static int WordEnd(string text, int i)
    {
        while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] is '-' or '_')) i++;
        return i;
    }

    static int OpaqueEnd(string text, int i)
    {
        do i++;
        while (i < text.Length && !char.IsWhiteSpace(text[i]) && Punctuation(text[i]) is null);
        return i;
    }

    static bool IsUrl(string text, int i) =>
        text.AsSpan(i).StartsWith("url(", StringComparison.OrdinalIgnoreCase)
        && (i == 0 || !char.IsLetterOrDigit(text[i - 1]));

    static int UrlEnd(string text, int i)
    {
        var open = i + 4;
        while (open < text.Length && char.IsWhiteSpace(text[open])) open++;
        var from = open < text.Length && text[open] is '"' or '\'' ? StringEnd(text, open) : open;
        var close = text.IndexOf(')', from);
        return close < 0 ? text.Length : close + 1;
    }

    static int StringEnd(string text, int start)
    {
        for (var i = start + 1; i < text.Length; i++)
        {
            if (text[i] == '\\') i++;
            else if (text[i] == text[start]) return i + 1;
        }
        return text.Length;
    }
}
