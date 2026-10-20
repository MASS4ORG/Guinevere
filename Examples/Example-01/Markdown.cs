using Guinevere;

namespace Example01;

public partial class Shared
{
    const string MarkdownSample = """
        # Streaming Markdown

        Text arrives a few characters at a time, like an **LLM answer**; only the *last block* is parsed again.

        - Headings, paragraphs and `inline code`
        - Bullet and numbered lists that wrap
          under their own text
        - Links such as [the repository](https://github.com/MASS4ORG/Guinevere) and images ![badge](badge)

        > Quotes nest, and keep their bar
        > while the answer streams.

        ```csharp
        // Highlighted with the shared syntax highlighter.
        var answer = gui.Markdown(streamed, style: style);
        if (answer.ClickedLink is { } link) Open(link);
        ```

        1. Unsupported syntax, such as tables, reads as text
        2. ~~Nothing~~ breaks mid-token
        """;

    static (ColorPalette Palette, MarkdownStyle Style)? _markdownStyle;
    static string _markdownLink = "none yet";

    /// <summary>A Markdown answer streaming in, with highlighted code, a quote, lists, a link and an image.</summary>
    internal static void MarkdownDemo(Gui gui, ColorPalette p)
    {
        var shown = (int)(gui.Time.Elapsed * 90 % (MarkdownSample.Length + 240));
        gui.RequestFrame();
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(12).Enter())
        {
            gui.ScrollY(p.Track, p.Border);
            Label(gui, p, $"Streamed {Math.Min(shown, MarkdownSample.Length)} of {MarkdownSample.Length} characters;"
                + $" last link clicked: {_markdownLink}");
            using (gui.Node().ExpandWidth().Padding(18).Enter())
            {
                gui.DrawBackgroundRect(p.SurfaceRaised, 12);
                var answer = gui.Markdown(MarkdownSample[..Math.Min(shown, MarkdownSample.Length)],
                    style: MarkdownStyleFor(p), images: MarkdownImage);
                _markdownLink = answer.ClickedLink ?? _markdownLink;
            }
        }
    }

    static MarkdownStyle MarkdownStyleFor(ColorPalette p)
    {
        if (_markdownStyle is { } cached && cached.Palette == p)
        {
            return cached.Style;
        }

        var style = new MarkdownStyle
        {
            FontSize = 15,
            Text = p.Text,
            Muted = p.TextDim,
            Link = p.Accent,
            CodeBackground = p.Surface,
            Rule = p.Border,
            Syntax = p.Name == "Light" ? SyntaxTheme.Light : SyntaxTheme.Dark,
        };
        _markdownStyle = (p, style);
        return style;
    }

    internal static RichTextImage? MarkdownImage(string source) =>
        source == "badge" ? new RichTextImage(_richTextBadge ??= Badge(), 18, 18) : null;
}
