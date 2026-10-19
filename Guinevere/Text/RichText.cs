using System.Text;

namespace Guinevere;

/// <summary>An image laid out inline with text, sized in logical pixels and sitting on the baseline.</summary>
/// <param name="Image">The image to draw.</param>
/// <param name="Width">Logical width.</param>
/// <param name="Height">Logical height.</param>
public sealed record RichTextImage(SKImage Image, float Width, float Height);

/// <summary>A link range of rich text and the target it reports when hovered or clicked.</summary>
/// <param name="Start">First source offset of the link.</param>
/// <param name="End">Source offset after the link.</param>
/// <param name="Target">Caller-defined target, such as a URL.</param>
public readonly record struct TextLink(int Start, int End, string Target);

/// <summary>
/// Builds multi-style text over ranges of one source string: fonts, sizes, colors, decorations, baseline shifts,
/// links and inline images. Keep an instance across frames, or rebuild an equal one; either way unchanged content
/// reuses the cached layout.
/// </summary>
public sealed class RichText
{
    readonly StringBuilder _text = new();
    readonly List<Span> _spans = [];
    RichTextContent? _content;

    /// <summary>Creates rich text, optionally starting with unstyled <paramref name="text"/>.</summary>
    public RichText(string text = "")
    {
        ArgumentNullException.ThrowIfNull(text);
        _text.Append(text);
    }

    /// <summary>The source string every span indexes into.</summary>
    public string Text => Content.Text;

    /// <summary>Number of UTF-16 characters in the source.</summary>
    public int Length => _text.Length;

    /// <summary>Appends text, styled when <paramref name="style"/> is given.</summary>
    public RichText Append(string text, TextSpanStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var start = _text.Length;
        _text.Append(text);
        return style is { } value ? Style(start, text.Length, value) : Changed();
    }

    /// <summary>Appends link text, underlined unless <paramref name="style"/> says otherwise.</summary>
    public RichText AppendLink(string text, string target, TextSpanStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var start = _text.Length;
        _text.Append(text);
        return Link(start, text.Length, target, style);
    }

    /// <summary>Appends an inline image represented in the source by <paramref name="alt"/>.</summary>
    public RichText AppendImage(SKImage image, float width, float height, string alt = "￼")
    {
        ArgumentException.ThrowIfNullOrEmpty(alt);
        var start = _text.Length;
        _text.Append(alt);
        return Image(start, alt.Length, image, width, height);
    }

    /// <summary>Styles a range; later spans override the properties they set.</summary>
    public RichText Style(int start, int length, TextSpanStyle style)
    {
        CheckRange(start, length);
        _spans.Add(new Span(start, start + length, style, null, null));
        return Changed();
    }

    /// <summary>Marks a range as a link, underlined unless <paramref name="style"/> says otherwise.</summary>
    public RichText Link(int start, int length, string target, TextSpanStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        CheckRange(start, length);
        _spans.Add(new Span(start, start + length, style ?? TextSpanStyle.Underlined, target, null));
        return Changed();
    }

    /// <summary>Replaces a range with an inline image that wraps and hit-tests as one unit.</summary>
    public RichText Image(int start, int length, SKImage image, float width, float height)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        CheckRange(start, length);
        ArgumentOutOfRangeException.ThrowIfZero(length);
        _spans.Add(new Span(start, start + length, default, null, new RichTextImage(image, width, height)));
        return Changed();
    }

    /// <summary>Unstyled rich text.</summary>
    public static implicit operator RichText(string text) => new(text);

    /// <summary>An immutable snapshot of the current content, rebuilt only after a change.</summary>
    internal RichTextContent Content => _content ??= RichTextContent.Create(_text.ToString(), _spans);

    void CheckRange(int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start + length, _text.Length, nameof(length));
    }

    RichText Changed()
    {
        _content = null;
        return this;
    }

    internal readonly record struct Span(int Start, int End, TextSpanStyle Style, string? Link, RichTextImage? Image);
}

/// <summary>A source range with one resolved style, link and image.</summary>
readonly record struct StyledRange(int Start, int End, TextSpanStyle Style, int Link, RichTextImage? Image);

/// <summary>Immutable rich-text content split into non-overlapping styled ranges; compares by value.</summary>
sealed class RichTextContent : IEquatable<RichTextContent>
{
    readonly int _hash;

    RichTextContent(string text, StyledRange[] ranges, TextLink[] links)
    {
        Text = text;
        Ranges = ranges;
        Links = links;
        var hash = new HashCode();
        hash.Add(text);
        foreach (var range in ranges)
        {
            hash.Add(range);
        }

        _hash = hash.ToHashCode();
    }

    internal string Text { get; }
    internal StyledRange[] Ranges { get; }
    internal TextLink[] Links { get; }

    /// <summary>Plain text with the inherited style everywhere.</summary>
    internal static RichTextContent Plain(string text) =>
        new(text, [new StyledRange(0, text.Length, default, -1, null)], []);

    internal static RichTextContent Create(string text, List<RichText.Span> spans)
    {
        if (spans.Count == 0)
        {
            return Plain(text);
        }

        var links = new List<TextLink>();
        var linkIndex = new int[spans.Count];
        for (var i = 0; i < spans.Count; i++)
        {
            linkIndex[i] = -1;
            if (spans[i].Link is { } target)
            {
                linkIndex[i] = links.Count;
                links.Add(new TextLink(spans[i].Start, spans[i].End, target));
            }
        }

        var boundaries = Boundaries(text.Length, spans);
        var ranges = new List<StyledRange>(boundaries.Count);
        for (var b = 0; b + 1 < boundaries.Count; b++)
        {
            ranges.Add(Resolve(boundaries[b], boundaries[b + 1], spans, linkIndex));
        }

        return new RichTextContent(text, [.. ranges], [.. links]);
    }

    /// <summary>Every span edge, except those that would split an inline image.</summary>
    static List<int> Boundaries(int length, List<RichText.Span> spans)
    {
        var edges = new SortedSet<int> { 0, length };
        foreach (var span in spans)
        {
            edges.Add(span.Start);
            edges.Add(span.End);
        }

        var result = new List<int>(edges.Count);
        foreach (var edge in edges)
        {
            if (!spans.Exists(span => span.Image is not null && edge > span.Start && edge < span.End))
            {
                result.Add(edge);
            }
        }

        return result;
    }

    static StyledRange Resolve(int start, int end, List<RichText.Span> spans, int[] linkIndex)
    {
        var style = default(TextSpanStyle);
        var link = -1;
        RichTextImage? image = null;
        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];
            if (span.Start >= end || span.End <= start || span.Start == span.End)
            {
                continue;
            }

            style = style.With(span.Style);
            link = linkIndex[i] >= 0 ? linkIndex[i] : link;
            image = span.Image ?? image;
        }

        return new StyledRange(start, end, style, link, image);
    }

    /// <inheritdoc />
    public bool Equals(RichTextContent? other) => other is not null && (ReferenceEquals(this, other)
        || (_hash == other._hash && Text == other.Text && Ranges.AsSpan().SequenceEqual(other.Ranges)
            && Links.AsSpan().SequenceEqual(other.Links)));

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RichTextContent other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _hash;
}
