namespace Guinevere;

/// <summary>
/// Character-level helpers for the <c>.pss</c> parser. Every transform keeps the text length and newlines, so
/// offsets into the processed text are offsets into the source.
/// </summary>
static class StyleSheetLexer
{
    /// <summary>Replaces <c>/* */</c> and <c>//</c> comments with spaces, leaving strings and <c>url()</c> intact.</summary>
    public static string BlankComments(string text)
    {
        char[]? buffer = null;
        for (var i = 0; i < text.Length;) i = BlankNext(text, i, ref buffer);
        return buffer is null ? text : new string(buffer);
    }

    /// <summary>Skips a string or <c>url()</c>, blanks a comment, or steps one character; returns the next index.</summary>
    static int BlankNext(string text, int i, ref char[]? buffer)
    {
        if (IsQuote(text[i])) return SkipString(text, i);
        if (IsUrlStart(text, i)) return SkipUrl(text, i);
        var end = CommentEnd(text, i);
        if (end < 0) return i + 1;
        buffer ??= text.ToCharArray();
        Blank(buffer, i, end);
        return end;
    }

    /// <summary>End of the comment starting at <paramref name="i"/>, or -1 when none starts there.</summary>
    static int CommentEnd(string text, int i)
    {
        if (text[i] != '/' || i + 1 >= text.Length) return -1;
        return text[i + 1] switch
        {
            '*' => BlockCommentEnd(text, i),
            '/' => LineEnd(text, i),
            _ => -1,
        };
    }

    /// <summary>Replaces <c>[start, end)</c> with spaces, keeping line breaks.</summary>
    public static void Blank(char[] buffer, int start, int end)
    {
        for (var j = start; j < end; j++)
            if (buffer[j] is not ('\n' or '\r')) buffer[j] = ' ';
    }

    /// <summary>
    /// Index of the first <c>;</c>, <c>{</c> or <c>}</c> at parenthesis depth 0 outside strings, or -1.
    /// </summary>
    public static int FindBoundary(string text, int start)
    {
        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (IsQuote(c)) i = SkipString(text, i) - 1;
            else if (depth == 0 && IsBoundary(c)) return i;
            else depth = Math.Max(0, depth + ParenthesisDelta(c));
        }
        return -1;
    }

    static bool IsQuote(char c) => c is '"' or '\'';

    static bool IsBoundary(char c) => c is ';' or '{' or '}';

    static int ParenthesisDelta(char c) => c switch
    {
        '(' => 1,
        ')' => -1,
        _ => 0,
    };

    /// <summary>Index just past the <c>}</c> matching the <c>{</c> at <paramref name="open"/>, or -1.</summary>
    public static int SkipBlock(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            var c = text[i];
            if (IsQuote(c)) i = SkipString(text, i) - 1;
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return i + 1;
        }
        return -1;
    }

    /// <summary>1-based line and column of <paramref name="offset"/>.</summary>
    public static (int Line, int Column) Position(string text, int offset)
    {
        var line = 1;
        var column = 1;
        for (var j = 0; j < Math.Min(offset, text.Length); j++)
            if (text[j] == '\n') { line++; column = 1; }
            else column++;
        return (line, column);
    }

    /// <summary>Whether <paramref name="c"/> can appear in a <c>.pss</c> identifier.</summary>
    public static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '-';

    static int SkipString(string text, int start)
    {
        var quote = text[start];
        for (var i = start + 1; i < text.Length; i++)
        {
            if (text[i] == '\\') i++;
            else if (text[i] == quote) return i + 1;
            else if (text[i] == '\n') return i;
        }
        return text.Length;
    }

    static bool IsUrlStart(string text, int i) =>
        text.AsSpan(i).StartsWith("url(", StringComparison.OrdinalIgnoreCase)
        && (i == 0 || !IsIdentifierChar(text[i - 1]));

    static int SkipUrl(string text, int start)
    {
        var i = start + 4;
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        if (i < text.Length && text[i] is '"' or '\'') return i;
        var close = text.IndexOf(')', i);
        return close < 0 ? text.Length : close + 1;
    }

    static int BlockCommentEnd(string text, int start)
    {
        var end = text.IndexOf("*/", start + 2, StringComparison.Ordinal);
        return end < 0 ? text.Length : end + 2;
    }

    static int LineEnd(string text, int start)
    {
        var end = text.IndexOf('\n', start);
        return end < 0 ? text.Length : end;
    }
}
