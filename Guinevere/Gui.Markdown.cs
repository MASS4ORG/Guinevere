namespace Guinevere;

/// <summary>What a Markdown document reports this frame; hover and clicks come from the render pass.</summary>
/// <param name="Node">The document's node.</param>
/// <param name="HoveredLink">The link target under the pointer, or null.</param>
/// <param name="ClickedLink">The link target pressed this frame, or null.</param>
/// <param name="HoveredImage">The source of the image under the pointer, or null.</param>
public readonly record struct MarkdownResult(LayoutNode Node, string? HoveredLink, string? ClickedLink,
    string? HoveredImage);

public partial class Gui
{
    static readonly MarkdownStyle DefaultMarkdownStyle = new();

    /// <summary>A document's last parse and converted blocks, kept per call site so streamed text parses once.</summary>
    sealed class MarkdownState
    {
        internal MarkdownDocument? Document;
        internal MarkdownRichText.Context Context;
        internal List<(MarkdownBlock Block, MarkdownBlockText Text)> Blocks = [];
    }

    /// <summary>Links and images found while drawing a document's blocks.</summary>
    struct MarkdownHits
    {
        internal string? Hovered;
        internal string? Clicked;
        internal string? Image;
    }

    /// <summary>
    /// Draws Markdown as a column of blocks. Parsing is incremental, so text that grows — such as a streamed answer —
    /// only parses its last block again; layouts are cached, and a new <paramref name="style"/> restyles without
    /// parsing.
    /// </summary>
    /// <param name="source">The Markdown text.</param>
    /// <param name="width">The width to wrap to, or <see cref="TextLayoutOptions.FillWidth"/> for the parent's.</param>
    /// <param name="style">Fonts, colors and spacing, or null for defaults from the scope.</param>
    /// <param name="images">Resolves an image source to an image, or returns null to show its alt text.</param>
    /// <returns>The node and the link or image under the pointer.</returns>
    public MarkdownResult Markdown(string source, float width = TextLayoutOptions.FillWidth, MarkdownStyle? style = null,
        Func<string, RichTextImage?>? images = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        style ??= DefaultMarkdownStyle;
        var scope = CurrentNodeScope;
        var size = style.FontSize > 0f ? style.FontSize : scope.Get<LayoutNodeScopeTextSize>().Value;
        var context = new MarkdownRichText.Context(style, style.Colors(scope.Get<LayoutNodeScopeTextColor>().Value),
            size, style.CodeFont ?? Fonts.ResolveRole(FontRole.Code), scope.Get<LayoutNodeScopeTextLayout>().Value);
        var node = Node().Direction(Axis.Vertical).Gap(style.BlockSpacing * size);
        if (width > 0f)
        {
            node.Width(width);
        }
        else
        {
            node.ExpandWidth();
        }

        using var _ = node.Enter();
        var state = ControlState($"{node.Id}/markdown", () => new MarkdownState());
        RefreshMarkdown(state, source, context, images);
        var hits = new MarkdownHits();
        DrawMarkdownBlocks(state, 0, state.Blocks.Count, 0, ref hits);
        return new MarkdownResult(node, hits.Hovered, hits.Clicked, hits.Image);
    }

    /// <summary>Parses the new source and reconverts only blocks whose content, style or images changed.</summary>
    static void RefreshMarkdown(MarkdownState state, string source, MarkdownRichText.Context context,
        Func<string, RichTextImage?>? images)
    {
        var document = MarkdownDocument.Parse(source, state.Document);
        var restyled = state.Context != context;
        var blocks = new List<(MarkdownBlock, MarkdownBlockText)>(document.Blocks.Count);
        for (var i = 0; i < document.Blocks.Count; i++)
        {
            var block = document.Blocks[i];
            blocks.Add(!restyled && i < state.Blocks.Count && ReferenceEquals(state.Blocks[i].Block, block)
                && SameImages(state.Blocks[i].Text, images)
                    ? state.Blocks[i]
                    : (block, MarkdownRichText.Build(block, context, images)));
        }

        state.Document = document;
        state.Context = context;
        state.Blocks = blocks;
    }

    static bool SameImages(MarkdownBlockText text, Func<string, RichTextImage?>? images)
    {
        foreach (var (source, image) in text.Images)
        {
            if (!Equals(images?.Invoke(source), image))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Draws blocks at one quote depth; a run of deeper blocks becomes one quote with a bar.</summary>
    void DrawMarkdownBlocks(MarkdownState state, int start, int end, int depth, ref MarkdownHits hits)
    {
        for (var i = start; i < end; i++)
        {
            if (state.Blocks[i].Block.QuoteDepth <= depth)
            {
                DrawMarkdownBlock(state, state.Blocks[i], ref hits);
                continue;
            }

            var stop = i;
            while (stop < end && state.Blocks[stop].Block.QuoteDepth > depth)
            {
                stop++;
            }

            DrawMarkdownQuote(state, i, stop, depth + 1, ref hits);
            i = stop - 1;
        }
    }

    void DrawMarkdownQuote(MarkdownState state, int start, int end, int depth, ref MarkdownHits hits)
    {
        var context = state.Context;
        var size = context.Size;
        using var quote = Node().ExpandWidth().Direction(Axis.Vertical).Gap(context.Style.BlockSpacing * size)
            .Padding(0, 0, 0, context.Style.QuoteIndent * size).Enter();
        if (Pass == Pass.Pass2Render)
        {
            var rect = CurrentNode.Rect;
            AddTextRect(CurrentNode, context.Colors.Rule, rect.X, rect.Y, Math.Max(2f, size * 0.2f), rect.H);
        }

        DrawMarkdownBlocks(state, start, end, depth, ref hits);
    }

    void DrawMarkdownBlock(MarkdownState state, (MarkdownBlock Block, MarkdownBlockText Text) item,
        ref MarkdownHits hits)
    {
        var context = state.Context;
        switch (item.Block.Kind)
        {
            case MarkdownBlockKind.Rule:
                var rule = Node(-1, Math.Max(1f, context.Size * 0.08f)).ExpandWidth();
                if (Pass == Pass.Pass2Render)
                {
                    AddTextRect(rule, context.Colors.Rule, rule.Rect.X, rule.Rect.Y, rule.Rect.W, rule.Rect.H);
                }

                break;
            case MarkdownBlockKind.Code:
                using (Node().ExpandWidth().Padding(context.Style.CodePadding * context.Size).Enter())
                {
                    DrawBackgroundRect(context.Colors.CodeBackground, context.Size * 0.3f);
                    DrawMarkdownText(item.Text, context, ref hits);
                }

                break;
            default:
                DrawMarkdownText(item.Text, context, ref hits);
                break;
        }
    }

    void DrawMarkdownText(MarkdownBlockText text, MarkdownRichText.Context context, ref MarkdownHits hits)
    {
        var result = DrawRichText(text.Text, TextLayoutOptions.FillWidth, text.Size, context.Colors.Text, text.Font,
            null, text.Options);
        if (text.Images.Count > 0)
        {
            hits.Image ??= HoveredMarkdownImage(result, text);
        }

        hits.Hovered ??= result.Hovered?.Target;
        hits.Clicked ??= result.Clicked?.Target;
    }

    /// <summary>The source of the image under the pointer in a block; registers the block for hover either way.</summary>
    string? HoveredMarkdownImage(RichTextResult result, MarkdownBlockText text)
    {
        if (!GetInteractable(result.Node).OnHover() || Pass != Pass.Pass2Render)
        {
            return null;
        }

        var origin = TextOrigin(result.Node, result.Layout, text.Options.Alignment ?? TextAlignment.Start);
        return result.Layout.ImageAt(LocalPointer(result.Node) - origin) is { } image ? text.SourceOf(image) : null;
    }
}
