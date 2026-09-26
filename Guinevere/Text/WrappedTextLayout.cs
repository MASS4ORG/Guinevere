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
        ArgumentNullException.ThrowIfNull(options);
        if (options.LineHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(options));
        if (options.MaxLines < 0) throw new ArgumentOutOfRangeException(nameof(options));

        if (maxWidth <= 0f || options.WrapMode == TextWrapMode.None)
        {
            var unwrapped = new List<WrappedLine>();
            var start = 0;
            foreach (var line in text.Split('\n'))
            {
                unwrapped.Add(new WrappedLine(line, start));
                start += line.Length + 1;
            }
            return LimitLines(unwrapped, maxWidth, measure, options);
        }

        var lines = new List<WrappedLine>();
        var paragraphStart = 0;

        foreach (var paragraph in text.Split('\n'))
        {
            if (options.WrapMode == TextWrapMode.Character)
                AppendCharacters(lines, paragraph, paragraphStart, measure, maxWidth);
            else
                AppendParagraph(lines, paragraph, paragraphStart, measure, maxWidth);
            paragraphStart += paragraph.Length + 1;
        }

        if (options.WrapMode == TextWrapMode.WordThenCharacter)
        {
            var split = new List<WrappedLine>();
            foreach (var line in lines)
            {
                if (measure(line.Text) > maxWidth)
                    AppendCharacters(split, line.Text, line.Start, measure, maxWidth);
                else
                    split.Add(line);
            }
            lines = split;
        }

        return LimitLines(lines, maxWidth, measure, options);
    }

    static void AppendCharacters(List<WrappedLine> lines, string text, int start,
        Func<string, float> measure, float maxWidth)
    {
        if (text.Length == 0) { lines.Add(new WrappedLine("", start)); return; }
        var boundaries = StringInfo.ParseCombiningCharacters(text);
        var lineStart = 0;
        for (var i = 1; i < boundaries.Length; i++)
        {
            var end = i + 1 < boundaries.Length ? boundaries[i + 1] : text.Length;
            if (measure(text[lineStart..end]) <= maxWidth) continue;
            lines.Add(new WrappedLine(text[lineStart..boundaries[i]], start + lineStart));
            lineStart = boundaries[i];
        }
        lines.Add(new WrappedLine(text[lineStart..], start + lineStart));
    }

    static IReadOnlyList<WrappedLine> LimitLines(List<WrappedLine> lines, float maxWidth,
        Func<string, float> measure, TextLayoutOptions options)
    {
        if (options.MaxLines == 0 || lines.Count <= options.MaxLines) return lines;
        var visible = lines.GetRange(0, options.MaxLines);
        var last = visible[^1];
        var marker = options.Ellipsis ?? "";
        if (maxWidth > 0f)
        {
            while (marker.Length > 0 && measure(marker) > maxWidth) marker = marker[..^1];
            while (last.Text.Length > 0 && measure(last.Text + marker) > maxWidth)
                last = last with { Text = last.Text[..^1] };
        }
        visible[^1] = last with { Text = last.Text + marker };
        return visible;
    }

    static void AppendParagraph(List<WrappedLine> lines, string paragraph, int paragraphStart,
        Func<string, float> measure, float maxWidth)
    {
        if (paragraph.Length == 0)
        {
            lines.Add(new WrappedLine("", paragraphStart));
            return;
        }

        var current = "";
        var currentStart = paragraphStart;
        var searchFrom = 0;

        foreach (var word in paragraph.Split(' '))
        {
            var relative = paragraph.IndexOf(word, searchFrom, StringComparison.Ordinal);
            var wordStart = paragraphStart + relative;
            searchFrom = relative + word.Length + 1;

            if (current.Length == 0)
            {
                if (measure(word) <= maxWidth)
                {
                    current = word;
                    currentStart = wordStart;
                }
                else
                {
                    // A single word wider than the pane still gets a line of its own.
                    lines.Add(new WrappedLine(word, wordStart));
                }

                continue;
            }

            var candidate = current + " " + word;
            if (measure(candidate) <= maxWidth)
            {
                current = candidate;
            }
            else
            {
                lines.Add(new WrappedLine(current, currentStart));
                current = measure(word) <= maxWidth ? word : "";
                if (current.Length > 0) currentStart = wordStart;
                else lines.Add(new WrappedLine(word, wordStart));
            }
        }

        if (current.Length > 0) lines.Add(new WrappedLine(current, currentStart));
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
        if (string.IsNullOrEmpty(line)) return 0;
        var best = 0;
        var bestDistance = float.MaxValue;

        for (var index = 0; index <= line.Length; index++)
        {
            var distance = Math.Abs(clickX - measure(line[..index]));
            if (distance >= bestDistance) continue;

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
        if (from >= to) return null;

        var x = measure(line.Text[..(from - line.Start)]);
        var width = measure(line.Text[..(to - line.Start)]) - x;
        return new LineSelection(x, Math.Max(1f, width), from, to);
    }
}
