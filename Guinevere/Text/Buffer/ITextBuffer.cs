namespace Guinevere;

/// <summary>
/// Immutable text with line access. An edit returns a new buffer and leaves this one intact, so a version can be
/// handed to background work or kept for undo without copying. Lines end with <c>\n</c> only.
/// </summary>
public interface ITextBuffer
{
    /// <summary>Number of UTF-16 code units.</summary>
    int Length { get; }

    /// <summary>Number of lines; an empty buffer has one empty line.</summary>
    int LineCount { get; }

    /// <summary>The code unit at an offset.</summary>
    char this[int offset] { get; }

    /// <summary>Offset of the first character of a zero-based line.</summary>
    int LineStart(int line);

    /// <summary>Length of a line without its line break.</summary>
    int LineLength(int line);

    /// <summary>The zero-based line holding an offset; the offset of a line break belongs to its line.</summary>
    int LineOf(int offset);

    /// <summary>A copy of a range.</summary>
    string GetText(int start, int length);

    /// <summary>A new buffer with <paramref name="length"/> code units at <paramref name="start"/> replaced.</summary>
    /// <param name="start">First offset to replace.</param>
    /// <param name="length">Number of code units removed.</param>
    /// <param name="text">Inserted text; <c>\r\n</c> and <c>\r</c> become <c>\n</c>.</param>
    ITextBuffer Replace(int start, int length, string text);

    /// <summary>A line's text without its line break.</summary>
    string Line(int line) => GetText(LineStart(line), LineLength(line));

    /// <summary>The whole text.</summary>
    string Text => GetText(0, Length);
}

/// <summary>Line-break handling shared by buffer implementations.</summary>
public static class TextBufferText
{
    /// <summary>The text with <c>\r\n</c> and lone <c>\r</c> turned into <c>\n</c>.</summary>
    public static string NormalizeLineBreaks(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Contains('\r') ? text.Replace("\r\n", "\n").Replace('\r', '\n') : text;
    }

    /// <summary>The line break a text mostly uses, so a saved document keeps its original style.</summary>
    public static string DetectLineBreak(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var crlf = 0;
        var lf = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n')
            {
                continue;
            }

            if (i > 0 && text[i - 1] == '\r')
            {
                crlf++;
            }
            else
            {
                lf++;
            }
        }

        return crlf > lf ? "\r\n" : "\n";
    }
}
