using System.Globalization;

namespace Guinevere;

/// <summary>One visual line of a wrapped message, with where it starts in the original text.</summary>
/// <param name="Text">The line's characters, as drawn.</param>
/// <param name="Start">Offset of the line's first character in the message.</param>
public readonly record struct WrappedLine(string Text, int Start);

/// <summary>A horizontal selection run inside one wrapped line, in pixels relative to the line's left edge.</summary>
/// <param name="X">Where the run starts, relative to the line's left edge.</param>
/// <param name="Width">The run's width.</param>
/// <param name="Start">The run's character offset.</param>
/// <param name="End">The first character after the run.</param>
public readonly record struct LineSelection(float X, float Width, int Start, int End);

/// <summary>A visual line as a source range plus the marker drawn after it, such as a truncation ellipsis.</summary>
readonly record struct LineRange(int Start, int End, string Marker);

/// <summary>Width of a source range followed by a suffix that is not part of the source.</summary>
delegate float RangeMeasure(int start, int end, string suffix);

/// <summary>
/// Lays a message out into visual lines: greedy word wrap to a width, and the coordinate/offset
/// mappings that let a pane implement drag-to-select over the wrapped result. Kept free of GUI types
/// so the wrapping and hit-testing rules can be tested on their own; the pane supplies the font and
/// the rectangles.
/// </summary>
public static class WrappedTextLayout
{
    /// <summary>Height one wrapped line occupies for a given font size, matching the text renderer.</summary>
    /// <param name="fontSize">The font size the text is drawn at.</param>
    /// <returns>The line height in pixels.</returns>
    public static float LineHeight(float fontSize) => fontSize * 1.2f;

    /// <summary>
    /// Wraps text to a width by greedy word join, splitting paragraphs on newlines. Each returned line
    /// carries its offset in the original message, so a click on the line maps back to a character in
    /// the un-wrapped text.
    /// </summary>
    /// <param name="text">The message to wrap.</param>
    /// <param name="font">The font measuring the glyphs.</param>
    /// <param name="maxWidth">The width lines may occupy, in pixels.</param>
    /// <returns>The visual lines.</returns>
    public static IReadOnlyList<WrappedLine> Wrap(string text, SKFont font, float maxWidth)
    {
        ArgumentNullException.ThrowIfNull(font);
        return Wrap(text, maxWidth, line => font.MeasureText(line));
    }

    /// <summary>Wraps text using the same width measurement as the renderer, including font fallback.</summary>
    public static IReadOnlyList<WrappedLine> Wrap(string text, float maxWidth, Func<string, float> measure)
        => Wrap(text, maxWidth, measure, new TextLayoutOptions());

    /// <summary>Wraps and truncates text with a single set of rules for measuring and drawing.</summary>
    public static IReadOnlyList<WrappedLine> Wrap(string text, float maxWidth, Func<string, float> measure,
        TextLayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(measure);
        var ranges = WrapRanges(text, maxWidth, (start, end, suffix) => measure(text[start..end] + suffix),
            options);
        var lines = new List<WrappedLine>(ranges.Count);
        foreach (var range in ranges)
        {
            lines.Add(new WrappedLine(text[range.Start..range.End] + range.Marker, range.Start));
        }

        return lines;
    }

    /// <summary>
    /// The wrapping and truncation rules over source offsets, so styled text whose width depends on where a
    /// range sits can share them with plain text.
    /// </summary>
    internal static List<LineRange> WrapRanges(string text, float maxWidth, RangeMeasure measure,
        TextLayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.LineHeight <= 0f || options.MaxLines < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        var mode = maxWidth > 0f ? options.WrapMode : TextWrapMode.None;
        var lines = new List<LineRange>();
        var paragraphStart = 0;
        while (true)
        {
            var newline = text.IndexOf('\n', paragraphStart);
            var paragraphEnd = newline < 0 ? text.Length : newline;
            AppendLines(lines, text, paragraphStart, paragraphEnd, measure, maxWidth, mode);
            if (newline < 0)
            {
                break;
            }

            paragraphStart = newline + 1;
        }

        if (mode == TextWrapMode.WordThenCharacter)
        {
            lines = SplitWideLines(lines, text, measure, maxWidth);
        }

        return LimitLines(lines, text, maxWidth, measure, options);
    }

    static void AppendLines(List<LineRange> lines, string text, int start, int end, RangeMeasure measure,
        float maxWidth, TextWrapMode mode)
    {
        switch (mode)
        {
            case TextWrapMode.None:
                lines.Add(new LineRange(start, end, ""));
                break;
            case TextWrapMode.Character:
                AppendCharacters(lines, text, start, end, measure, maxWidth);
                break;
            default:
                AppendParagraph(lines, text, start, end, measure, maxWidth);
                break;
        }
    }

    static List<LineRange> SplitWideLines(List<LineRange> lines, string text, RangeMeasure measure, float maxWidth)
    {
        var split = new List<LineRange>(lines.Count);
        foreach (var line in lines)
        {
            if (measure(line.Start, line.End, "") > maxWidth)
            {
                AppendCharacters(split, text, line.Start, line.End, measure, maxWidth);
            }
            else
            {
                split.Add(line);
            }
        }

        return split;
    }

    static void AppendCharacters(List<LineRange> lines, string text, int start, int end, RangeMeasure measure,
        float maxWidth)
    {
        if (start == end)
        {
            lines.Add(new LineRange(start, start, ""));
            return;
        }

        var boundaries = StringInfo.ParseCombiningCharacters(text[start..end]);
        var lineStart = 0;
        for (var i = 1; i < boundaries.Length; i++)
        {
            var next = i + 1 < boundaries.Length ? boundaries[i + 1] : end - start;
            if (measure(start + lineStart, start + next, "") <= maxWidth)
            {
                continue;
            }

            lines.Add(new LineRange(start + lineStart, start + boundaries[i], ""));
            lineStart = boundaries[i];
        }

        lines.Add(new LineRange(start + lineStart, end, ""));
    }

    static List<LineRange> LimitLines(List<LineRange> lines, string text, float maxWidth, RangeMeasure measure,
        TextLayoutOptions options)
    {
        if (options.MaxLines == 0 || lines.Count <= options.MaxLines)
        {
            return lines;
        }

        var visible = lines.GetRange(0, options.MaxLines);
        var last = visible[^1];
        var marker = options.Ellipsis ?? "";
        visible[^1] = maxWidth > 0f ? FitMarker(last, marker, text, maxWidth, measure) : last with { Marker = marker };
        return visible;
    }

    /// <summary>Shortens the marker, then the line, until both fit the width.</summary>
    static LineRange FitMarker(LineRange line, string marker, string text, float maxWidth, RangeMeasure measure)
    {
        var end = line.End;
        while (marker.Length > 0 && measure(end, end, marker) > maxWidth)
        {
            marker = marker[..^1];
        }

        while (end > line.Start && measure(line.Start, end, marker) > maxWidth)
        {
            // Never split a surrogate pair, so the truncated line still draws its last full character.
            end -= end - 2 >= line.Start && char.IsLowSurrogate(text[end - 1])
                && char.IsHighSurrogate(text[end - 2]) ? 2 : 1;
        }

        return line with { End = end, Marker = marker };
    }

    static void AppendParagraph(List<LineRange> lines, string text, int paragraphStart, int paragraphEnd,
        RangeMeasure measure, float maxWidth)
    {
        if (paragraphStart == paragraphEnd)
        {
            lines.Add(new LineRange(paragraphStart, paragraphStart, ""));
            return;
        }

        var added = lines.Count;
        var currentStart = paragraphStart;
        var currentEnd = paragraphStart;
        var wordStart = paragraphStart;
        while (true)
        {
            var space = text.IndexOf(' ', wordStart, paragraphEnd - wordStart);
            var wordEnd = space < 0 ? paragraphEnd : space;
            AppendWord(lines, measure, maxWidth, wordStart, wordEnd, ref currentStart, ref currentEnd);
            if (space < 0)
            {
                break;
            }

            wordStart = space + 1;
        }

        if (currentEnd > currentStart)
        {
            lines.Add(new LineRange(currentStart, currentEnd, ""));
        }

        // A paragraph of only spaces still occupies a line.
        if (lines.Count == added)
        {
            lines.Add(new LineRange(paragraphStart, paragraphStart, ""));
        }
    }

    static void AppendWord(List<LineRange> lines, RangeMeasure measure, float maxWidth, int wordStart, int wordEnd,
        ref int currentStart, ref int currentEnd)
    {
        if (currentEnd == currentStart)
        {
            if (measure(wordStart, wordEnd, "") <= maxWidth)
            {
                currentStart = wordStart;
                currentEnd = wordEnd;
            }
            else
            {
                // A single word wider than the pane still gets a line of its own.
                lines.Add(new LineRange(wordStart, wordEnd, ""));
            }

            return;
        }

        if (measure(currentStart, wordEnd, "") <= maxWidth)
        {
            currentEnd = wordEnd;
            return;
        }

        lines.Add(new LineRange(currentStart, currentEnd, ""));
        if (measure(wordStart, wordEnd, "") <= maxWidth)
        {
            currentStart = wordStart;
            currentEnd = wordEnd;
        }
        else
        {
            currentStart = currentEnd = wordEnd;
            lines.Add(new LineRange(wordStart, wordEnd, ""));
        }
    }

    /// <summary>The character offset nearest a point on one wrapped line.</summary>
    /// <param name="font">Font the line is measured with.</param>
    /// <param name="line">The line's text.</param>
    /// <param name="clickX">The x to locate, relative to the line's left edge.</param>
    /// <returns>The column within the line.</returns>
    public static int PositionInLine(SKFont font, string line, float clickX)
    {
        ArgumentNullException.ThrowIfNull(font);
        return PositionInLine(text => font.MeasureText(text), line, clickX);
    }

    /// <summary>The nearest character offset using the renderer's width measurement.</summary>
    public static int PositionInLine(Func<string, float> measure, string line, float clickX)
    {
        ArgumentNullException.ThrowIfNull(measure);
        if (string.IsNullOrEmpty(line))
        {
            return 0;
        }

        var best = 0;
        var bestDistance = float.MaxValue;

        for (var index = 0; index <= line.Length; index++)
        {
            var distance = Math.Abs(clickX - measure(line[..index]));
            if (distance >= bestDistance)
            {
                continue;
            }

            best = index;
            bestDistance = distance;
        }

        return best;
    }

    /// <summary>
    /// The run of <paramref name="selectionStart"/>..<paramref name="selectionEnd"/> that falls inside
    /// the given line, or null when that line holds no selected characters.
    /// </summary>
    /// <param name="font">Font measuring the line's prefix widths.</param>
    /// <param name="line">The wrapped line, with its offset in the message.</param>
    /// <param name="selectionStart">The selection's lower bound.</param>
    /// <param name="selectionEnd">The selection's upper bound.</param>
    /// <returns>The highlight run, or null when the line is entirely outside the selection.</returns>
    public static LineSelection? SelectionOn(SKFont font, WrappedLine line, int selectionStart, int selectionEnd)
    {
        ArgumentNullException.ThrowIfNull(font);
        return SelectionOn(text => font.MeasureText(text), line, selectionStart, selectionEnd);
    }

    /// <summary>Measures a selection run using the renderer's width measurement.</summary>
    public static LineSelection? SelectionOn(Func<string, float> measure, WrappedLine line,
        int selectionStart, int selectionEnd)
    {
        ArgumentNullException.ThrowIfNull(measure);

        var from = Math.Max(line.Start, selectionStart);
        var to = Math.Min(line.Start + line.Text.Length, selectionEnd);
        if (from >= to)
        {
            return null;
        }

        var x = measure(line.Text[..(from - line.Start)]);
        var width = measure(line.Text[..(to - line.Start)]) - x;
        return new LineSelection(x, Math.Max(1f, width), from, to);
    }
}
