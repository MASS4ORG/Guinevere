namespace Guinevere;

/// <summary>What a rich-text node reports: its node, final layout, and the link under the pointer or clicked.</summary>
/// <param name="Node">The node the text occupies.</param>
/// <param name="Layout">The layout drawn; final in the render pass.</param>
/// <param name="Hovered">The link under the pointer during the render pass, or null.</param>
/// <param name="Clicked">The link pressed this frame during the render pass, or null.</param>
public readonly record struct RichTextResult(LayoutNode Node, TextLayout Layout, TextLink? Hovered, TextLink? Clicked);

public partial class Gui
{
    readonly TextLayoutCache _textLayouts = new();

    /// <summary>Number of text layouts built rather than reused from the cache.</summary>
    internal int TextLayoutBuilds => _textLayouts.Builds;

    /// <summary>
    /// Turns text into glyphs for every text layout of this GUI; replace it with a full shaper for ligatures and
    /// complex scripts. Changing it relayouts all text.
    /// </summary>
    public ITextShaper TextShaper
    {
        get;
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    } = BasicTextShaper.Instance;

    /// <summary>
    /// Lays out rich text with this scope's font, size, glyph fallback and layout options. Unchanged input returns the
    /// cached result, so measuring, drawing and hit testing share one layout.
    /// </summary>
    /// <param name="text">The text to lay out.</param>
    /// <param name="maxWidth">Wrap width; zero or less lays out without wrapping.</param>
    /// <param name="size">Logical font size, or zero for the scope's.</param>
    /// <param name="font">Font, or null for the scope's.</param>
    /// <param name="options">Wrapping and truncation, or null for the scope's.</param>
    /// <param name="alignment">Line placement, or null for the options' alignment or the start edge.</param>
    public TextLayout LayoutText(RichText text, float maxWidth = 0, float size = 0, Font? font = null,
        TextLayoutOptions? options = null, TextAlignment? alignment = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var scope = CurrentNodeScope;
        options ??= scope.Get<LayoutNodeScopeTextLayout>().Value;
        return LayoutText(TextRequest(text.Content, font, size, Math.Max(0f, maxWidth), options,
            AlignmentOf(alignment, options), scope));
    }

    /// <summary>The requested alignment, else the options', else the start edge.</summary>
    static TextAlignment AlignmentOf(TextAlignment? requested, TextLayoutOptions options) =>
        requested ?? options.Alignment ?? TextAlignment.Start;

    /// <summary>
    /// Draws rich text as a node and reports link hover and clicks. With <paramref name="wrapWidth"/> set to
    /// <see cref="TextLayoutOptions.FillWidth"/> the text wraps to the width layout gives the node, in the same frame.
    /// </summary>
    /// <param name="text">The text to draw.</param>
    /// <param name="wrapWidth">Wrap width, zero for none, or <see cref="TextLayoutOptions.FillWidth"/>.</param>
    /// <param name="size">Logical font size, or zero for the scope's.</param>
    /// <param name="color">Color for spans without one, or null for the scope's.</param>
    /// <param name="font">Font for spans without one, or null for the scope's.</param>
    /// <param name="alignment">Line placement, or null for the options' alignment or the start edge.</param>
    /// <param name="layout">Wrapping and truncation, or null for the scope's.</param>
    /// <param name="effects">Shadows, outline or gradient applied to every span.</param>
    public RichTextResult DrawRichText(RichText text, float wrapWidth = 0, float size = 0, Color? color = null,
        Font? font = null, TextAlignment? alignment = null, TextLayoutOptions? layout = null,
        TextEffects? effects = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var scope = CurrentNodeScope;
        var options = layout ?? scope.Get<LayoutNodeScopeTextLayout>().Value;
        var align = AlignmentOf(alignment, options);
        var (node, textLayout) = TextBlock(text.Content, font, size, wrapWidth, options, align);
        var links = textLayout.Links.Count > 0;
        if (links)
        {
            GetInteractable(node);
        }

        if (Pass != Pass.Pass2Render)
        {
            return new RichTextResult(node, textLayout, null, null);
        }

        var origin = TextOrigin(node, textLayout, align);
        var hovered = links ? HoveredLink(node, textLayout, origin) : null;
        var clicked = hovered is not null && Input.IsMouseButtonPressed(MouseButton.Left) ? hovered : null;
        DrawTextLayout(textLayout, origin, color ?? scope.Get<LayoutNodeScopeTextColor>().Value,
            ResolveTextEffects(effects, scope), false, node);
        return new RichTextResult(node, textLayout, hovered, clicked);
    }

    /// <summary>The link under the pointer, requesting the hand cursor while one is hovered.</summary>
    TextLink? HoveredLink(LayoutNode node, TextLayout layout, Vector2 origin)
    {
        var hovered = GetInteractable(node).OnHover() ? layout.LinkAt(LocalPointer(node) - origin) : null;
        if (hovered is not null)
        {
            node.Cursor(PointerCursor.Hand);
        }

        return hovered;
    }

    TextLayout LayoutText(TextLayoutRequest request) => _textLayouts.Get(request, Fonts);

    TextLayoutRequest TextRequest(RichTextContent content, Font? font, float size, float maxWidth,
        TextLayoutOptions options, TextAlignment alignment, LayoutNodeScope scope)
    {
        var scale = FontScaleFor(scope);
        var logical = size > 0 ? size : scope.Get<LayoutNodeScopeTextSize>().Value;
        return new TextLayoutRequest(content,
            new FontKey(font ?? scope.Get<LayoutNodeScopeTextFont>().Value, logical * scale),
            scope.Get<LayoutNodeScopeIconFont>().Value.SkFont.Typeface,
            scope.Get<LayoutNodeScopeWidgetIconFont>().Value, scale, maxWidth, options, alignment, Fonts.Version,
            TextShaper);
    }

    /// <summary>
    /// Creates the node for a text block and returns the layout to draw. A fixed or zero wrap width sizes the node to
    /// the text; <see cref="TextLayoutOptions.FillWidth"/> lets layout measure height from the node's final width.
    /// </summary>
    internal (LayoutNode Node, TextLayout Layout) TextBlock(RichTextContent content, Font? font, float size,
        float wrapWidth, TextLayoutOptions options, TextAlignment alignment)
    {
        var request = TextRequest(content, font, size, Math.Max(0f, wrapWidth), options, alignment,
            CurrentNodeScope);
        if (wrapWidth >= 0f)
        {
            var layout = LayoutText(request);
            return (Node(layout.Width, layout.Height), layout);
        }

        var node = Node();
        if (Pass == Pass.Pass1Build)
        {
            node.ContentMeasure = width =>
            {
                var measured = LayoutText(request with { MaxWidth = width });
                return new Vector2(measured.Width, measured.Height);
            };
            return (node, LayoutText(request));
        }

        return (node, LayoutText(request with { MaxWidth = node.InnerRect.W }));
    }

    /// <summary>Top-left of a layout inside its node, placing the block by its alignment.</summary>
    internal static Vector2 TextOrigin(LayoutNode node, TextLayout layout, TextAlignment alignment)
    {
        var inner = node.InnerRect;
        var factor = alignment switch
        {
            TextAlignment.Center => 0.5f,
            TextAlignment.End => 1f,
            _ => 0f
        };
        return new Vector2(inner.X + Math.Max(0f, (inner.W - layout.Width) * factor), inner.Y);
    }

    /// <summary>
    /// Draws a layout from <see cref="LayoutText(RichText, float, float, Font?, TextLayoutOptions?, TextAlignment?)"/>
    /// into the current node with its top-left at
    /// <paramref name="origin"/>, for controls that place text themselves, such as editors. Only the render pass draws.
    /// </summary>
    /// <param name="layout">The layout to draw.</param>
    /// <param name="origin">Top-left of the layout in screen space.</param>
    /// <param name="color">Color for runs without one, or null for the scope's.</param>
    /// <param name="effects">Shadows, outline or gradient applied to every run.</param>
    public void DrawTextLayout(TextLayout layout, Vector2 origin, Color? color = null, TextEffects? effects = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var scope = CurrentNodeScope;
        DrawTextLayout(layout, origin, color ?? scope.Get<LayoutNodeScopeTextColor>().Value,
            ResolveTextEffects(effects, scope), false, CurrentNode);
    }

    /// <summary>
    /// Queues a layout's backgrounds, inline images, glyph layers and decorations into a node, back to front; runs
    /// without a color use <paramref name="color"/>.
    /// </summary>
    internal void DrawTextLayout(TextLayout layout, Vector2 origin, Color color, TextEffects? effects, bool clip,
        LayoutNode node)
    {
        if (Pass != Pass.Pass2Render)
        {
            return;
        }

        DrawTextBackgrounds(layout, origin, node);
        var paints = new Dictionary<Color, List<(SKPaint Paint, Vector2 Offset)>>();
        List<(SKPaint Paint, Vector2 Offset)> PaintsFor(Color value)
        {
            if (!paints.TryGetValue(value, out var layers))
            {
                paints[value] = layers = BuildTextPaints(value, effects, node.InnerRect);
            }

            return layers;
        }

        var layerCount = PaintsFor(color).Count;
        for (var layer = 0; layer < layerCount; layer++)
        {
            foreach (var run in layout.Runs)
            {
                if (run.Image is { } image)
                {
                    if (layer == layerCount - 1)
                    {
                        DrawInlineImage(run, image, origin, node);
                    }

                    continue;
                }

                if (run.Glyphs?.Blob(run.Font.SkFont) is not { } blob)
                {
                    continue;
                }

                var (paint, offset) = PaintsFor(run.Style.Color ?? color)[layer];
                AddDraw(new Text(blob, run.Text, origin + new Vector2(run.X, run.Baseline) + offset, run.Font.SkFont,
                    paint, effects), clip, node);
            }
        }

        DrawTextDecorations(layout, origin, color, node);
    }

    void DrawInlineImage(TextLayoutRun run, RichTextImage image, Vector2 origin, LayoutNode node)
    {
        var scale = image.Width > 0f ? run.Width / image.Width : 1f;
        var height = image.Height * scale;
        var destination = new Rect(origin.X + run.X, origin.Y + run.Baseline - height, run.Width, height);
        AddDraw(new ImageDrawable(image.Image, destination, null, null), node: node);
    }

    static void DrawTextBackgrounds(TextLayout layout, Vector2 origin, LayoutNode node)
    {
        foreach (var line in layout.Lines)
        {
            for (var i = line.FirstRun; i < line.FirstRun + line.RunCount; i++)
            {
                var run = layout.Runs[i];
                if (run.Style.Background is { } background && run.Width > 0f)
                {
                    AddTextRect(node, background, origin.X + run.X, origin.Y + line.Y, run.Width, line.Height);
                }
            }
        }
    }

    static void DrawTextDecorations(TextLayout layout, Vector2 origin, Color color, LayoutNode node)
    {
        foreach (var run in layout.Runs)
        {
            var decoration = run.Style.Decoration ?? TextDecoration.None;
            if (decoration != TextDecoration.None && run.Width > 0f)
            {
                DrawRunDecorations(run, decoration, origin, run.Style.DecorationColor ?? run.Style.Color ?? color,
                    node);
            }
        }
    }

    /// <summary>Underline, strikethrough and overline of one run, placed by its font's metrics.</summary>
    static void DrawRunDecorations(TextLayoutRun run, TextDecoration decoration, Vector2 origin, Color color,
        LayoutNode node)
    {
        var metrics = run.Font.SkFont.Metrics;
        var size = run.Font.Size;
        var thickness = Math.Max(1f, metrics.UnderlineThickness ?? size / 14f);
        var baseline = origin.Y + run.Baseline;
        var x = origin.X + run.X;
        if (decoration.HasFlag(TextDecoration.Underline))
        {
            AddTextRect(node, color, x, baseline + (metrics.UnderlinePosition ?? size * 0.1f), run.Width, thickness);
        }

        if (decoration.HasFlag(TextDecoration.Strikethrough))
        {
            AddTextRect(node, color, x, baseline + (metrics.StrikeoutPosition ?? -size * 0.3f), run.Width,
                Math.Max(1f, metrics.StrikeoutThickness ?? thickness));
        }

        if (decoration.HasFlag(TextDecoration.Overline))
        {
            AddTextRect(node, color, x, baseline + metrics.Ascent, run.Width, thickness);
        }
    }

    static void AddTextRect(LayoutNode node, Color color, float x, float y, float width, float height)
    {
        var shape = Shape.Rectangle(width, height).SolidColor(color);
        node.DrawList.Add(new ShapePos(shape.Path, shape.Paint, new Vector2(x, y)));
    }
}
