using System.Text;

namespace Guinevere;

/// <summary>What a Markdown block is.</summary>
public enum MarkdownBlockKind
{
    /// <summary>Running text.</summary>
    Paragraph,
    /// <summary>An ATX heading; <see cref="MarkdownBlock.Level"/> is 1–6.</summary>
    Heading,
    /// <summary>A fenced or indented code block; the text is in <see cref="MarkdownBlock.Code"/>.</summary>
    Code,
    /// <summary>A bullet or numbered list item; <see cref="MarkdownBlock.Level"/> is its nesting depth.</summary>
    ListItem,
    /// <summary>A thematic break.</summary>
    Rule,
}

/// <summary>How a run of inline text is styled.</summary>
[Flags]
public enum MarkdownSpanStyle
{
    /// <summary>Plain text.</summary>
    None = 0,
    /// <summary><c>**strong**</c>.</summary>
    Bold = 1,
    /// <summary><c>*emphasis*</c>.</summary>
    Italic = 2,
    /// <summary><c>`code`</c>.</summary>
    Code = 4,
    /// <summary><c>[text](url)</c>.</summary>
    Link = 8,
    /// <summary><c>~~strikethrough~~</c>.</summary>
    Strike = 16,
    /// <summary><c>![alt](src)</c>; the text is the alt text and <see cref="MarkdownSpan.Image"/> the source.</summary>
    Image = 32,
}

/// <summary>A run of inline text with one style.</summary>
/// <param name="Text">The text, or an image's alt text.</param>
/// <param name="Style">Its style.</param>
/// <param name="Url">The link target, for <see cref="MarkdownSpanStyle.Link"/>.</param>
/// <param name="Image">The image source, for <see cref="MarkdownSpanStyle.Image"/>.</param>
public sealed record MarkdownSpan(string Text, MarkdownSpanStyle Style, string? Url = null, string? Image = null);

/// <summary>One block of a Markdown document.</summary>
/// <param name="Kind">What the block is.</param>
/// <param name="Spans">Its inline content; empty for code and rules.</param>
/// <param name="Level">Heading level or list depth.</param>
/// <param name="QuoteDepth">How many <c>&gt;</c> quote levels contain it.</param>
/// <param name="Marker">A list item's bullet or number, such as <c>•</c> or <c>3.</c>.</param>
/// <param name="Code">A code block's text.</param>
/// <param name="Language">A fenced code block's info string.</param>
/// <param name="SourceStart">Offset in the normalized source where the block's first line starts.</param>
public sealed record MarkdownBlock(
    MarkdownBlockKind Kind,
    IReadOnlyList<MarkdownSpan> Spans,
    int Level = 0,
    int QuoteDepth = 0,
    string? Marker = null,
    string? Code = null,
    string? Language = null,
    int SourceStart = 0);

/// <summary>
/// A parsed CommonMark subset: headings, paragraphs, fenced and indented code, bullet and numbered lists, block
/// quotes, rules, and inline emphasis, strong, code, strikethrough, links and images. Anything else reads as plain
/// text, so partial documents — such as a streamed answer mid-token — always parse.
/// </summary>
public sealed partial class MarkdownDocument
{
    MarkdownDocument(string source, IReadOnlyList<MarkdownBlock> blocks)
    {
        Source = source;
        Blocks = blocks;
    }

    /// <summary>The source with line endings normalized to <c>\n</c>.</summary>
    public string Source { get; }

    /// <summary>The document's blocks in order.</summary>
    public IReadOnlyList<MarkdownBlock> Blocks { get; }

    /// <summary>
    /// Parses Markdown. When <paramref name="previous"/> parsed a prefix of <paramref name="source"/>, as when an
    /// answer streams in, its closed blocks are reused and only the last block onwards is parsed again.
    /// </summary>
    /// <param name="source">The Markdown.</param>
    /// <param name="previous">An earlier parse of a prefix of the same text, or null.</param>
    public static MarkdownDocument Parse(string source, MarkdownDocument? previous = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        source = source.ReplaceLineEndings("\n");
        if (previous is not null && previous.Source == source)
        {
            return previous;
        }

        var reused = previous is { Blocks.Count: > 1 } && source.StartsWith(previous.Source, StringComparison.Ordinal)
            ? previous.Blocks.Take(previous.Blocks.Count - 1).ToList()
            : [];
        var start = reused.Count == 0 ? 0 : previous!.Blocks[reused.Count].SourceStart;
        var blocks = new BlockParser(source, start).Parse();
        reused.AddRange(blocks);
        return new MarkdownDocument(source, reused);
    }

    /// <summary>Parses inline Markdown into styled runs.</summary>
    /// <param name="text">One block's text.</param>
    /// <returns>The runs; unmatched markers stay literal.</returns>
    public static IReadOnlyList<MarkdownSpan> ParseInline(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var spans = new List<MarkdownSpan>();
        new InlineParser(spans).Parse(text, MarkdownSpanStyle.None, null);
        return spans;
    }

    /// <summary>Splits source lines into blocks, starting at a line boundary.</summary>
    sealed class BlockParser(string source, int start)
    {
        readonly List<MarkdownBlock> _blocks = [];
        readonly StringBuilder _paragraph = new();
        readonly List<(int Start, string Text)> _lines = [];
        int _paragraphQuote;
        int _paragraphStart;

        internal List<MarkdownBlock> Parse()
        {
            var offset = start;
            while (offset <= source.Length)
            {
                var newline = source.IndexOf('\n', offset);
                var end = newline < 0 ? source.Length : newline;
                _lines.Add((offset, source[offset..end]));
                offset = end + 1;
            }

            for (var i = 0; i < _lines.Count; i++)
            {
                i = Line(i);
            }

            FlushParagraph();
            return _blocks;
        }

        void FlushParagraph()
        {
            if (_paragraph.Length == 0)
            {
                return;
            }

            _blocks.Add(new MarkdownBlock(MarkdownBlockKind.Paragraph, ParseInline(_paragraph.ToString()),
                QuoteDepth: _paragraphQuote, SourceStart: _paragraphStart));
            _paragraph.Clear();
        }

        /// <summary>Handles one line and returns the index of the last line it consumed.</summary>
        int Line(int index)
        {
            var (lineStart, raw) = _lines[index];
            var (quote, line) = StripQuotes(raw);
            var trimmed = line.TrimStart();
            var indent = line.Length - trimmed.Length;
            if (IsFence(trimmed))
            {
                return Fence(index, quote, trimmed, indent, lineStart);
            }

            if (trimmed.Length == 0)
            {
                FlushParagraph();
                return index;
            }

            if (Block(trimmed, quote, indent, lineStart) is { } block)
            {
                FlushParagraph();
                _blocks.Add(block);
                return index;
            }

            Text(index, line, trimmed, indent, quote, lineStart);
            return index;
        }

        static bool IsFence(string trimmed) =>
            trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal);

        int Fence(int index, int quote, string trimmed, int indent, int lineStart)
        {
            FlushParagraph();
            var fence = trimmed[..3];
            var code = new List<string>();
            var i = index + 1;
            for (; i < _lines.Count; i++)
            {
                var inner = StripQuotes(_lines[i].Text).Line;
                if (inner.TrimStart().StartsWith(fence, StringComparison.Ordinal))
                {
                    break;
                }

                code.Add(inner.Length >= indent && string.IsNullOrWhiteSpace(inner[..indent]) ? inner[indent..] : inner);
            }

            var language = trimmed[3..].Trim();
            _blocks.Add(new MarkdownBlock(MarkdownBlockKind.Code, [], QuoteDepth: quote, Code: string.Join('\n', code),
                Language: language.Length > 0 ? language : null, SourceStart: lineStart));
            return i;
        }

        MarkdownBlock? Block(string trimmed, int quote, int indent, int lineStart)
        {
            if (IsRule(trimmed))
            {
                return new MarkdownBlock(MarkdownBlockKind.Rule, [], QuoteDepth: quote, SourceStart: lineStart);
            }

            if (Heading(trimmed) is { } heading)
            {
                return new MarkdownBlock(MarkdownBlockKind.Heading, ParseInline(heading.Text), heading.Level, quote,
                    SourceStart: lineStart);
            }

            if (ListMarker(trimmed) is { } item)
            {
                return new MarkdownBlock(MarkdownBlockKind.ListItem, ParseInline(trimmed[item.Length..]), indent / 2,
                    quote, item.Marker, SourceStart: lineStart);
            }

            return null;
        }

        void Text(int index, string line, string trimmed, int indent, int quote, int lineStart)
        {
            if (indent >= 4 && _paragraph.Length == 0)
            {
                _blocks.Add(new MarkdownBlock(MarkdownBlockKind.Code, [], QuoteDepth: quote, Code: line[4..],
                    SourceStart: lineStart));
                return;
            }

            if (ContinuesListItem(index, indent))
            {
                var last = _blocks[^1];
                _blocks[^1] = last with
                {
                    Spans = [.. last.Spans, new MarkdownSpan(" ", MarkdownSpanStyle.None), .. ParseInline(trimmed)],
                };
                return;
            }

            if (_paragraph.Length > 0)
            {
                _paragraph.Append(line.EndsWith("  ", StringComparison.Ordinal) ? '\n' : ' ');
            }
            else
            {
                _paragraphQuote = quote;
                _paragraphStart = lineStart;
            }

            _paragraph.Append(trimmed.TrimEnd());
        }

        /// <summary>A lazy continuation of a list item joins the item rather than starting a paragraph.</summary>
        bool ContinuesListItem(int index, int indent) =>
            _paragraph.Length == 0 && indent > 0 && _blocks is [.., { Kind: MarkdownBlockKind.ListItem }]
            && index > 0 && StripQuotes(_lines[index - 1].Text).Line.Trim().Length > 0;
    }

    static (int Depth, string Line) StripQuotes(string line)
    {
        var depth = 0;
        var rest = line.AsSpan();
        while (rest.TrimStart() is ['>', .. var after])
        {
            depth++;
            rest = after is [' ', .. var spaced] ? spaced : after;
        }

        return (depth, depth == 0 ? line : rest.ToString());
    }

    static bool IsRule(string line)
    {
        var compact = line.Replace(" ", "", StringComparison.Ordinal);
        return compact.Length >= 3 && compact[0] is '-' or '*' or '_' && compact.All(c => c == compact[0]);
    }

    static (int Level, string Text)? Heading(string line)
    {
        var level = 0;
        while (level < line.Length && line[level] == '#')
        {
            level++;
        }

        return level is >= 1 and <= 6 && (level == line.Length || line[level] == ' ')
            ? (level, line[level..].Trim().TrimEnd('#').TrimEnd())
            : null;
    }

    static (string Marker, int Length)? ListMarker(string line) => line is ['-' or '*' or '+', ' ', ..]
        ? ("•", 2)
        : NumberedMarker(line);

    /// <summary>A <c>3.</c> or <c>3)</c> marker of up to nine digits followed by a space.</summary>
    static (string Marker, int Length)? NumberedMarker(string line)
    {
        var digits = 0;
        while (digits < line.Length && digits < 9 && char.IsAsciiDigit(line[digits]))
        {
            digits++;
        }

        return digits > 0 && EndsNumber(line, digits) ? (line[..(digits + 1)], digits + 2) : null;
    }

    static bool EndsNumber(string line, int at) =>
        at + 1 < line.Length && line[at] is '.' or ')' && line[at + 1] == ' ';
}
