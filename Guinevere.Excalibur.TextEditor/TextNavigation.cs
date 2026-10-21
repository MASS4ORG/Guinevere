using System.Globalization;

namespace Guinevere;

/// <summary>
/// Caret arithmetic over a buffer: text-element steps preserve grapheme clusters, words follow identifier and
/// punctuation runs, and columns count tabs as stops.
/// </summary>
public static class TextNavigation
{
    /// <summary>Whether a character belongs to an identifier.</summary>
    public static bool IsWordChar(char value) => char.IsLetterOrDigit(value) || value == '_';

    /// <summary>The offset after the text element at <paramref name="offset"/>, crossing line breaks.</summary>
    public static int NextBoundary(ITextBuffer buffer, int offset)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (offset >= buffer.Length)
        {
            return buffer.Length;
        }

        if (SimpleAscii(buffer[offset]) && (offset + 1 == buffer.Length || buffer[offset + 1] < 128))
        {
            return offset + 1;
        }

        var line = buffer.LineOf(offset);
        var start = buffer.LineStart(line);
        var text = buffer.Line(line);
        var column = offset - start;
        return column >= text.Length ? offset + 1 : offset + StringInfo.GetNextTextElementLength(text, column);
    }

    /// <summary>The start of the text element before <paramref name="offset"/>, crossing line breaks.</summary>
    public static int PreviousBoundary(ITextBuffer buffer, int offset)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (offset <= 0)
        {
            return 0;
        }

        if (SimpleAscii(buffer[offset - 1]) && (offset == 1 || buffer[offset - 2] < 128))
        {
            return offset - 1;
        }

        var line = buffer.LineOf(offset);
        var start = buffer.LineStart(line);
        if (offset == start)
        {
            return offset - 1;
        }

        return start + PreviousElement(buffer.Line(line), offset - start);
    }

    /// <summary>The start of the text element that ends at or contains <paramref name="column"/> in a line.</summary>
    public static int PreviousElement(string line, int column)
    {
        ArgumentNullException.ThrowIfNull(line);
        var previous = 0;
        var position = 0;
        while (position < column && position < line.Length)
        {
            previous = position;
            position += StringInfo.GetNextTextElementLength(line, position);
        }

        return previous;
    }

    /// <summary>The nearest text-element boundary at or before <paramref name="column"/> in a line.</summary>
    public static int SnapToElement(string line, int column)
    {
        ArgumentNullException.ThrowIfNull(line);
        var position = 0;
        while (position < line.Length)
        {
            var next = position + StringInfo.GetNextTextElementLength(line, position);
            if (next > column)
            {
                return position;
            }

            position = next;
        }

        return line.Length;
    }

    /// <summary>The start of the word or punctuation run left of the offset, after skipping spaces.</summary>
    public static int WordLeft(ITextBuffer buffer, int offset)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var line = buffer.LineOf(offset);
        var start = buffer.LineStart(line);
        if (offset == start)
        {
            return Math.Max(0, offset - 1);
        }

        var text = buffer.Line(line);
        var i = offset - start;
        while (i > 0 && char.IsWhiteSpace(text[i - 1]))
        {
            i--;
        }

        var kind = i > 0 ? Kind(text[i - 1]) : 0;
        while (i > 0 && !char.IsWhiteSpace(text[i - 1]) && Kind(text[i - 1]) == kind)
        {
            i--;
        }

        return start + i;
    }

    /// <summary>The end of the word or punctuation run right of the offset, after skipping spaces.</summary>
    public static int WordRight(ITextBuffer buffer, int offset)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var line = buffer.LineOf(offset);
        var start = buffer.LineStart(line);
        var text = buffer.Line(line);
        var i = offset - start;
        if (i >= text.Length)
        {
            return Math.Min(buffer.Length, offset + 1);
        }

        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        var kind = i < text.Length ? Kind(text[i]) : 0;
        while (i < text.Length && !char.IsWhiteSpace(text[i]) && Kind(text[i]) == kind)
        {
            i++;
        }

        return start + i;
    }

    /// <summary>The identifier touching an offset, or an empty range at it when there is none.</summary>
    public static (int Start, int End) WordAt(ITextBuffer buffer, int offset)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var line = buffer.LineOf(offset);
        var start = buffer.LineStart(line);
        var text = buffer.Line(line);
        var column = Math.Clamp(offset - start, 0, text.Length);
        var from = column;
        var to = column;
        while (from > 0 && IsWordChar(text[from - 1]))
        {
            from--;
        }

        while (to < text.Length && IsWordChar(text[to]))
        {
            to++;
        }

        return (start + from, start + to);
    }

    /// <summary>The visual column of a code-unit column, with tabs advancing to the next multiple of the size.</summary>
    public static int VisualColumn(string line, int column, int tabSize)
    {
        ArgumentNullException.ThrowIfNull(line);
        var visual = 0;
        for (var i = 0; i < column && i < line.Length; i++)
        {
            visual = line[i] == '\t' ? (visual / tabSize + 1) * tabSize : visual + 1;
        }

        return visual;
    }

    /// <summary>The code-unit column nearest a visual column, never inside a text element.</summary>
    public static int ColumnAt(string line, int visual, int tabSize)
    {
        ArgumentNullException.ThrowIfNull(line);
        var current = 0;
        for (var i = 0; i < line.Length; i++)
        {
            var next = line[i] == '\t' ? (current / tabSize + 1) * tabSize : current + 1;
            if (next > visual)
            {
                return SnapToElement(line, visual - current < next - visual ? i : i + 1);
            }

            current = next;
        }

        return line.Length;
    }

    /// <summary>The leading spaces and tabs of a line.</summary>
    public static string Indentation(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var length = 0;
        while (length < line.Length && line[length] is ' ' or '\t')
        {
            length++;
        }

        return line[..length];
    }

    static int Kind(char value) => IsWordChar(value) ? 1 : 2;

    static bool SimpleAscii(char value) => value is >= ' ' and <= '~' or '\t';
}
