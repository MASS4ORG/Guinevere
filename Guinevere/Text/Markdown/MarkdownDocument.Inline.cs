using System.Text;

namespace Guinevere;

public sealed partial class MarkdownDocument
{
    /// <summary>Turns one block's inline Markdown into styled runs, merging neighbours with the same style.</summary>
    sealed class InlineParser(List<MarkdownSpan> spans)
    {
        internal void Parse(string text, MarkdownSpanStyle style, string? url)
        {
            var plain = new StringBuilder();
            for (var i = 0; i < text.Length; i++)
            {
                var consumed = Special(text, i, style, url, plain);
                if (consumed < 0)
                {
                    plain.Append(text[i]);
                }
                else
                {
                    i = consumed;
                }
            }

            Flush(plain, style, url);
        }

        /// <summary>Parses markup starting at <paramref name="i"/>; returns its last index, or -1 for a literal.</summary>
        int Special(string text, int i, MarkdownSpanStyle style, string? url, StringBuilder plain) => text[i] switch
        {
            '\\' => Escape(text, i, plain),
            '`' => Code(text, i, style, url, plain),
            '!' => Image(text, i, style, url, plain),
            '[' when url is null => Link(text, i, style, plain),
            _ => Emphasis(text, i, style, url, plain),
        };

        static int Escape(string text, int i, StringBuilder plain)
        {
            if (i + 1 >= text.Length || char.IsAsciiLetterOrDigit(text[i + 1]))
            {
                return -1;
            }

            plain.Append(text[i + 1]);
            return i + 1;
        }

        int Code(string text, int i, MarkdownSpanStyle style, string? url, StringBuilder plain)
        {
            var end = Closing(text, i + 1, "`");
            if (end < 0)
            {
                return -1;
            }

            Flush(plain, style, url);
            spans.Add(new MarkdownSpan(text[(i + 1)..end], style | MarkdownSpanStyle.Code, url));
            return end;
        }

        int Image(string text, int i, MarkdownSpanStyle style, string? url, StringBuilder plain)
        {
            if (i + 1 >= text.Length || text[i + 1] != '[' || LinkAt(text, i + 1) is not { } image)
            {
                return -1;
            }

            Flush(plain, style, url);
            spans.Add(new MarkdownSpan(image.Label, style | MarkdownSpanStyle.Image, url, image.Url));
            return image.End;
        }

        int Link(string text, int i, MarkdownSpanStyle style, StringBuilder plain)
        {
            if (LinkAt(text, i) is not { } link)
            {
                return -1;
            }

            Flush(plain, style, null);
            Parse(link.Label, style | MarkdownSpanStyle.Link, link.Url);
            return link.End;
        }

        int Emphasis(string text, int i, MarkdownSpanStyle style, string? url, StringBuilder plain)
        {
            if (EmphasisMarker(text, i) is not { } emphasis)
            {
                return -1;
            }

            var start = i + emphasis.Marker.Length;
            var end = Closes(text, start, emphasis.Marker);
            if (end < 0)
            {
                return -1;
            }

            Flush(plain, style, url);
            Parse(text[start..end], style | emphasis.Style, url);
            return end + emphasis.Marker.Length - 1;
        }

        void Flush(StringBuilder plain, MarkdownSpanStyle style, string? url)
        {
            if (plain.Length == 0)
            {
                return;
            }

            if (spans is [.., var last] && Continues(last, style, url))
            {
                spans[^1] = last with { Text = last.Text + plain };
            }
            else
            {
                spans.Add(new MarkdownSpan(plain.ToString(), style, url));
            }

            plain.Clear();
        }

        static bool Continues(MarkdownSpan last, MarkdownSpanStyle style, string? url) =>
            last.Style == style && last.Url == url && last.Image is null;
    }

    /// <summary>Emphasis markers, longest first, so <c>**</c> is not read as two <c>*</c>.</summary>
    static readonly (string Marker, MarkdownSpanStyle Style)[] EmphasisMarkers =
    [
        ("**", MarkdownSpanStyle.Bold), ("__", MarkdownSpanStyle.Bold), ("~~", MarkdownSpanStyle.Strike),
        ("*", MarkdownSpanStyle.Italic), ("_", MarkdownSpanStyle.Italic),
    ];

    /// <summary>The emphasis marker at <paramref name="i"/>; a single underscore only opens at a word start.</summary>
    static (string Marker, MarkdownSpanStyle Style)? EmphasisMarker(string text, int i)
    {
        foreach (var emphasis in EmphasisMarkers)
        {
            if (text.AsSpan(i).StartsWith(emphasis.Marker, StringComparison.Ordinal))
            {
                return emphasis.Marker != "_" || i == 0 || !char.IsLetterOrDigit(text[i - 1]) ? emphasis : null;
            }
        }

        return null;
    }

    /// <summary>Where an emphasis opened at <paramref name="start"/> closes; neither edge may touch a space.</summary>
    static int Closes(string text, int start, string marker)
    {
        if (start >= text.Length || text[start] == ' ')
        {
            return -1;
        }

        var end = Closing(text, start, marker);
        return end > 0 && text[end - 1] != ' ' ? end : -1;
    }

    static int Closing(string text, int from, string marker)
    {
        var at = text.IndexOf(marker, from, StringComparison.Ordinal);
        at = marker == "*" ? SkipDoubled(text, at) : at;
        at = marker is "**" or "__" ? LastOfRun(text, at, marker[0]) : at;
        return at > from || (at == from && marker == "`") ? at : -1;
    }

    /// <summary>"**" must not close a single "*" run.</summary>
    static int SkipDoubled(string text, int at)
    {
        while (at >= 0 && at + 1 < text.Length && text[at + 1] == '*')
        {
            at = text.IndexOf('*', at + 2);
        }

        return at;
    }

    /// <summary>In "***", "**" closes with the last two, leaving the first to close an inner "*".</summary>
    static int LastOfRun(string text, int at, char marker)
    {
        while (at >= 0 && at + 2 < text.Length && text[at + 2] == marker)
        {
            at++;
        }

        return at;
    }

    /// <summary>A <c>[label](url)</c> opening at <paramref name="open"/>; brackets nest, so a label may hold an image.</summary>
    static (string Label, string Url, int End)? LinkAt(string text, int open)
    {
        var close = MatchingBracket(text, open);
        if (close < 0 || close + 1 >= text.Length || text[close + 1] != '(')
        {
            return null;
        }

        var end = text.IndexOf(')', close + 2);
        return end < 0 ? null : (text[(open + 1)..close], text[(close + 2)..end].Trim(), end);
    }

    static int MatchingBracket(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            depth += text[i] switch
            {
                '[' => 1,
                ']' => -1,
                _ => 0
            };
            if (depth == 0)
            {
                return i;
            }
        }

        return -1;
    }
}
