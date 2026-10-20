using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>Markdown parsing, incremental reparsing and drawing as rich text.</summary>
public class MarkdownTests
{
    static string Text(MarkdownBlock block) => string.Concat(block.Spans.Select(s => s.Text));

    /// <summary>Each block kind is recognized with its level, marker and quote depth.</summary>
    [Fact]
    public void ParsesBlocks()
    {
        var doc = MarkdownDocument.Parse("""
            ## Title
            First line
            same paragraph.

            - one
              continued
            3. three

            > quoted

            ```cs
            var x = 1;
            ```
            ---
                indented code
            """);

        Assert.Equal(
            [MarkdownBlockKind.Heading, MarkdownBlockKind.Paragraph, MarkdownBlockKind.ListItem,
                MarkdownBlockKind.ListItem, MarkdownBlockKind.Paragraph, MarkdownBlockKind.Code, MarkdownBlockKind.Rule,
                MarkdownBlockKind.Code],
            doc.Blocks.Select(b => b.Kind));
        Assert.Equal(2, doc.Blocks[0].Level);
        Assert.Equal("First line same paragraph.", Text(doc.Blocks[1]));
        Assert.Equal(("•", "one continued"), (doc.Blocks[2].Marker, Text(doc.Blocks[2])));
        Assert.Equal("3.", doc.Blocks[3].Marker);
        Assert.Equal(1, doc.Blocks[4].QuoteDepth);
        Assert.Equal(("var x = 1;", "cs"), (doc.Blocks[5].Code, doc.Blocks[5].Language));
        Assert.Equal("indented code", doc.Blocks[7].Code);
        Assert.Equal(doc.Source.IndexOf("First", StringComparison.Ordinal), doc.Blocks[1].SourceStart);
    }

    /// <summary>Inline markers become styled spans; unmatched markers stay literal.</summary>
    [Fact]
    public void ParsesInline()
    {
        var spans = MarkdownDocument.ParseInline("a **b *c*** `d` [e](http://x) ~~f~~ \\*g 2*3 *open");

        Assert.Contains(spans, s => s is { Text: "b ", Style: MarkdownSpanStyle.Bold });
        Assert.Contains(spans, s => s is { Text: "c", Style: MarkdownSpanStyle.Bold | MarkdownSpanStyle.Italic });
        Assert.Contains(spans, s => s is { Text: "d", Style: MarkdownSpanStyle.Code });
        Assert.Contains(spans, s => s is { Text: "e", Style: MarkdownSpanStyle.Link, Url: "http://x" });
        Assert.Contains(spans, s => s is { Text: "f", Style: MarkdownSpanStyle.Strike });
        Assert.EndsWith(" *g 2*3 *open", spans[^1].Text);
    }

    [Theory]
    [InlineData("***both***", "both", MarkdownSpanStyle.Bold | MarkdownSpanStyle.Italic)]
    [InlineData("__strong__", "strong", MarkdownSpanStyle.Bold)]
    [InlineData("_em_", "em", MarkdownSpanStyle.Italic)]
    [InlineData("[~~gone~~](x)", "gone", MarkdownSpanStyle.Link | MarkdownSpanStyle.Strike)]
    [InlineData("*a **b** c*", "b", MarkdownSpanStyle.Italic | MarkdownSpanStyle.Bold)]
    public void ParsesEmphasisVariants(string source, string text, MarkdownSpanStyle style)
    {
        Assert.Contains(MarkdownDocument.ParseInline(source), span => span.Text == text && span.Style == style);
    }

    [Theory]
    [InlineData("snake_case_name")]
    [InlineData("a * b * c")]
    [InlineData("trailing\\")]
    [InlineData("`unclosed")]
    [InlineData("![no link")]
    [InlineData("**open")]
    public void LiteralMarkersStayPlainText(string source)
    {
        var span = Assert.Single(MarkdownDocument.ParseInline(source));

        Assert.Equal((source, MarkdownSpanStyle.None), (span.Text, span.Style));
    }

    [Fact]
    public void NumberedMarkersAcceptParenthesesAndRejectLongNumbers()
    {
        var doc = MarkdownDocument.Parse("2) second\n\n1234567890. not a list\n\n-not a list");

        Assert.Equal("2)", doc.Blocks[0].Marker);
        Assert.Equal(MarkdownBlockKind.Paragraph, doc.Blocks[1].Kind);
        Assert.Equal(MarkdownBlockKind.Paragraph, doc.Blocks[2].Kind);
    }

    [Fact]
    public void LinkAndStrikeCombineDecorations()
    {
        var block = MarkdownDocument.Parse("[~~x~~](y)").Blocks[0];
        var style = new MarkdownStyle();
        var text = MarkdownRichText.Build(block, new MarkdownRichText.Context(style, style.Colors(Color.White), 10f,
            null, new TextLayoutOptions()), null);

        Assert.Contains(text.Text.Content.Ranges,
            range => range.Style.Decoration == (TextDecoration.Underline | TextDecoration.Strikethrough));
    }

    [Fact]
    public void ParsesImagesIncludingImagesInsideLinks()
    {
        var spans = MarkdownDocument.ParseInline("see ![logo](logo.png) and [![badge](b.svg)](https://x)");

        Assert.Contains(spans, s => s is { Text: "logo", Style: MarkdownSpanStyle.Image, Image: "logo.png" });
        Assert.Contains(spans, s => s is
        {
            Text: "badge", Style: MarkdownSpanStyle.Image | MarkdownSpanStyle.Link, Image: "b.svg", Url: "https://x"
        });
    }

    /// <summary>An unterminated fence, as in a streamed answer, still yields a code block.</summary>
    [Fact]
    public void ParsesPartialDocuments()
    {
        var doc = MarkdownDocument.Parse("Here:\r\n```\nfirst line");

        Assert.Equal("first line", doc.Blocks[^1].Code);
        Assert.DoesNotContain('\r', doc.Source);
    }

    [Fact]
    public void UnsupportedSyntaxDegradesToText()
    {
        var doc = MarkdownDocument.Parse("| a | b |\n|---|---|\n\n<div>x</div>\n\n[unclosed](link");

        Assert.All(doc.Blocks, block => Assert.Equal(MarkdownBlockKind.Paragraph, block.Kind));
        Assert.Equal("| a | b | |---|---|", Text(doc.Blocks[0]));
        Assert.Equal("<div>x</div>", Text(doc.Blocks[1]));
        Assert.Equal("[unclosed](link", Text(doc.Blocks[2]));
    }

    [Fact]
    public void GrowingSourceReusesClosedBlocks()
    {
        var first = MarkdownDocument.Parse("# Title\n\nOne paragraph");
        var grown = MarkdownDocument.Parse("# Title\n\nOne paragraph that grew\n\n- item", first);
        var fresh = MarkdownDocument.Parse("# Title\n\nOne paragraph that grew\n\n- item");

        Assert.Same(first.Blocks[0], grown.Blocks[0]);
        Assert.Equal(fresh.Blocks.Select(Text), grown.Blocks.Select(Text));
        Assert.Equal(fresh.Blocks.Select(b => b.SourceStart), grown.Blocks.Select(b => b.SourceStart));
        Assert.Same(grown, MarkdownDocument.Parse("# Title\n\nOne paragraph that grew\n\n- item", grown));
        Assert.NotSame(first.Blocks[0], MarkdownDocument.Parse("# Other", first).Blocks[0]);
    }

    static LayoutNode Blocks(MarkdownResult result) => result.Node;

    [Fact]
    public void DrawsWrappedBlocksInsideANodeAndCachesAcrossFrames()
    {
        using var harness = new FrameHarness(400, 600);
        var width = 300f;
        MarkdownResult result = default;
        const string Source = "# Hi\n\nSome **bold** text that is long enough to wrap onto more lines in a pane.\n\n"
            + "> quoted\n\n- item\n\n---\n\n```cs\nint x = 1;\n```";
        void Draw(Gui gui) => result = gui.Markdown(Source, width);

        harness.Frame(Draw);
        var builds = harness.Gui.TextLayoutBuilds;
        var wideHeight = result.Node.Rect.H;
        harness.Frame(Draw);

        Assert.Equal(builds, harness.Gui.TextLayoutBuilds);
        Assert.Equal(300f, result.Node.Rect.W);
        Assert.Equal(6, Blocks(result).Children.Count);
        width = 140f;
        harness.Frame(Draw);
        Assert.True(result.Node.Rect.H > wideHeight);
        using var image = harness.Snapshot();
        using var pixmap = image.PeekPixels();
        Assert.Contains(pixmap.GetPixelSpan().ToArray().Chunk(4), pixel => pixel[3] > 0);
    }

    [Fact]
    public void LinksAreHoveredAndClicked()
    {
        using var harness = new FrameHarness();
        MarkdownResult result = default;
        void Draw(Gui gui) => result = gui.Markdown("[go](target) here", 300);

        harness.Frame(Draw);
        var text = result.Node.Children[0];
        harness.Input.MoveTo(text.InnerRect.X + 3, text.InnerRect.Y + 6);
        harness.Frame(Draw);
        Assert.Equal("target", result.HoveredLink);
        harness.Input.PressButton();
        harness.Frame(Draw);

        Assert.Equal("target", result.ClickedLink);
    }

    [Fact]
    public void ImagesResolveThroughTheCallerAndFallBackToAltText()
    {
        using var surface = SKSurface.Create(new SKImageInfo(40, 40));
        using var picture = surface.Snapshot();
        var logo = new RichTextImage(picture, 40, 40);
        RichTextImage? available = null;
        using var harness = new FrameHarness();
        MarkdownResult result = default;
        void Draw(Gui gui) => result = gui.Markdown("![logo](logo.png) after", 300,
            images: source => source == "logo.png" ? available : null);

        harness.Frame(Draw);
        var text = result.Node.Children[0];
        Assert.True(text.Rect.H < 40f);
        available = logo;
        harness.Frame(Draw);
        harness.Frame(Draw);
        text = result.Node.Children[0];
        Assert.True(text.Rect.H >= 40f);
        harness.Input.MoveTo(text.InnerRect.X + 5, text.InnerRect.Y + 10);
        harness.Frame(Draw);

        Assert.Equal("logo.png", result.HoveredImage);
    }

    [Fact]
    public void StyleChangesRestyleTheSameDocument()
    {
        using var harness = new FrameHarness();
        var style = new MarkdownStyle { FontSize = 12 };
        MarkdownResult result = default;
        void Draw(Gui gui) => result = gui.Markdown("plain text", 300, style);

        harness.Frame(Draw);
        var small = result.Node.Rect.H;
        style = style with { FontSize = 24 };
        harness.Frame(Draw);

        Assert.True(result.Node.Rect.H > small);
    }

    [Fact]
    public void ListItemsHangWrappedLinesUnderTheirText()
    {
        var block = MarkdownDocument.Parse("  - nested item").Blocks[0];
        var context = new MarkdownRichText.Context(new MarkdownStyle(), new MarkdownStyle().Colors(Color.White), 10f,
            null, new TextLayoutOptions());

        var text = MarkdownRichText.Build(block, context, null);

        Assert.Equal(1, block.Level);
        Assert.Equal(14f, text.Options.FirstLineIndent, 3);
        Assert.Equal(28f, text.Options.HangingIndent, 3);
        Assert.Equal([28f], text.Options.TabStops);
        Assert.StartsWith("•\t", text.Text.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void CodeBlocksAreHighlightedByLanguageAndHeadingsScaled()
    {
        var doc = MarkdownDocument.Parse("# Big\n\n```csharp\nreturn 1;\n```\n\n```unknown\nreturn 1;\n```");
        var style = new MarkdownStyle();
        var context = new MarkdownRichText.Context(style, style.Colors(Color.White), 10f, null,
            new TextLayoutOptions());

        var heading = MarkdownRichText.Build(doc.Blocks[0], context, null);
        var known = MarkdownRichText.Build(doc.Blocks[1], context, null);
        var unknown = MarkdownRichText.Build(doc.Blocks[2], context, null);

        Assert.Equal(17f, heading.Size, 3);
        Assert.Contains(known.Text.Content.Ranges,
            range => range.Style.Color == style.Syntax.StyleOf(SyntaxKind.Keyword)!.Value.Color);
        Assert.Single(unknown.Text.Content.Ranges);
        Assert.Equal(style.CodeWrap, known.Options.WrapMode);
    }

    [Fact]
    public void UnresolvedImagesReportNoSource()
    {
        var text = new MarkdownBlockText(new RichText(), new TextLayoutOptions(), 10f, null, [("a.png", null)]);
        using var surface = SKSurface.Create(new SKImageInfo(2, 2));
        using var picture = surface.Snapshot();

        Assert.Null(text.SourceOf(new RichTextImage(picture, 2, 2)));
    }
}
