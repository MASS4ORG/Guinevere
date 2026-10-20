namespace Guinevere;

/// <summary>A Markdown block ready to draw: its rich text, layout options, size and the images it resolved.</summary>
/// <param name="Text">The block's content.</param>
/// <param name="Options">Line height, wrapping and list indents.</param>
/// <param name="Size">Logical base size; headings are larger than the body.</param>
/// <param name="Font">Base font override, such as the code font, or null.</param>
/// <param name="Images">Each image source and what the resolver returned for it, in order.</param>
sealed record MarkdownBlockText(RichText Text, TextLayoutOptions Options, float Size, Font? Font,
    IReadOnlyList<(string Source, RichTextImage? Image)> Images)
{
    /// <summary>The source of a drawn image, for hover reporting.</summary>
    internal string? SourceOf(RichTextImage image)
    {
        foreach (var (source, resolved) in Images)
        {
            if (Equals(resolved, image))
            {
                return source;
            }
        }

        return null;
    }
}

/// <summary>Converts Markdown blocks into rich text, so Markdown draws, wraps and hit-tests like any rich text.</summary>
/// <param name="context">The style the block is converted with.</param>
/// <param name="resolve">Resolves image sources, or null to show alt text.</param>
sealed class MarkdownRichText(MarkdownRichText.Context context, Func<string, RichTextImage?>? resolve)
{
    /// <summary>
    /// What a block is converted against; equal contexts produce equal output. The image resolver is left out
    /// because callers often pass a new delegate each frame; resolved images are compared instead.
    /// </summary>
    internal readonly record struct Context(MarkdownStyle Style, MarkdownColors Colors, float Size, Font? CodeFont,
        TextLayoutOptions Options);

    readonly List<(string, RichTextImage?)> _images = [];

    /// <summary>Converts one block.</summary>
    internal static MarkdownBlockText Build(MarkdownBlock block, Context context,
        Func<string, RichTextImage?>? resolve) => new MarkdownRichText(context, resolve).Convert(block);

    MarkdownBlockText Convert(MarkdownBlock block)
    {
        var style = context.Style;
        var size = context.Size;
        var options = context.Options with { LineHeight = style.LineHeight };
        return block.Kind switch
        {
            MarkdownBlockKind.Code => new MarkdownBlockText(
                SyntaxHighlighting.ToRichText(block.Code ?? "", style.Highlighter(block.Language), style.Syntax),
                options with { WrapMode = style.CodeWrap, LineHeight = 1.35f, Alignment = null }, size * 0.92f,
                context.CodeFont, _images),
            MarkdownBlockKind.Heading => new MarkdownBlockText(Inline(block, TextSpanStyle.Bold), options,
                size * HeadingScale(block, style), null, _images),
            MarkdownBlockKind.ListItem => ListItem(block, options),
            _ => new MarkdownBlockText(Inline(block, default), options, size, null, _images),
        };
    }

    static float HeadingScale(MarkdownBlock block, MarkdownStyle style) =>
        style.HeadingScale[Math.Clamp(block.Level, 1, style.HeadingScale.Count) - 1];

    /// <summary>A list item: its marker, a tab to the text column, and a hanging indent so wrapped lines align.</summary>
    MarkdownBlockText ListItem(MarkdownBlock block, TextLayoutOptions options)
    {
        var indent = context.Style.ListIndent * context.Size;
        var start = block.Level * indent;
        var text = new RichText().Append(block.Marker ?? "•", TextSpanStyle.Colored(context.Colors.Muted)).Append("\t");
        Append(text, block, default);
        return new MarkdownBlockText(text, options with
        {
            FirstLineIndent = start,
            HangingIndent = start + indent,
            TabStops = [start + indent],
        }, context.Size, null, _images);
    }

    RichText Inline(MarkdownBlock block, TextSpanStyle baseStyle)
    {
        var text = new RichText();
        Append(text, block, baseStyle);
        return text;
    }

    void Append(RichText text, MarkdownBlock block, TextSpanStyle baseStyle)
    {
        foreach (var span in block.Spans)
        {
            var start = text.Length;
            var style = baseStyle.With(SpanStyle(span.Style));
            if (span.Image is { } source)
            {
                AppendImage(text, span, source, style);
            }
            else
            {
                text.Append(span.Text, style);
            }

            if (span.Url is { } url && text.Length > start)
            {
                text.Link(start, text.Length - start, url, style);
            }
        }
    }

    void AppendImage(RichText text, MarkdownSpan span, string source, TextSpanStyle style)
    {
        var image = resolve?.Invoke(source);
        _images.Add((source, image));
        if (image is not null)
        {
            text.AppendImage(image.Image, image.Width, image.Height);
            return;
        }

        // An image the caller cannot resolve shows its alt text, so the document still reads.
        text.Append(span.Text.Length > 0 ? span.Text : source,
            style.With(new TextSpanStyle { Color = context.Colors.Muted, Italic = true }));
    }

    TextSpanStyle SpanStyle(MarkdownSpanStyle span)
    {
        var code = span.HasFlag(MarkdownSpanStyle.Code);
        return new TextSpanStyle
        {
            Weight = span.HasFlag(MarkdownSpanStyle.Bold) ? 700 : null,
            Italic = span.HasFlag(MarkdownSpanStyle.Italic) ? true : null,
            Font = code ? context.CodeFont : null,
            Background = code ? context.Colors.InlineCode : null,
            Color = span.HasFlag(MarkdownSpanStyle.Link) ? context.Colors.Link : null,
            Decoration = Decoration(span),
        };
    }

    static TextDecoration? Decoration(MarkdownSpanStyle span) =>
        (span & (MarkdownSpanStyle.Link | MarkdownSpanStyle.Strike)) switch
        {
            MarkdownSpanStyle.Link => TextDecoration.Underline,
            MarkdownSpanStyle.Strike => TextDecoration.Strikethrough,
            MarkdownSpanStyle.None => null,
            _ => TextDecoration.Underline | TextDecoration.Strikethrough,
        };
}
