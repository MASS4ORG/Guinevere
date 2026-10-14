namespace Guinevere;

public partial class Gui
{
    /// <summary>
    /// Draws text as a layout node: in Pass1Build phase, creates a node sized to the text; in Pass2Render phase, draws the text in the node's rect.
    /// Returns the node for layout chaining; selectable text supports pointer selection and copying without editing.
    /// </summary>
    public LayoutNode DrawText(
        string text,
        float size = 0,
        Color? color = null,
        Font? font = null,
        float wrapWidth = 0,
        bool centerInRect = true,
        bool clip = false,
        TextEffects? effects = null,
        TextLayoutOptions? layout = null) =>
        DrawText(text, false, size, color, font, wrapWidth, centerInRect, clip, effects, layout);

    /// <summary>Draws a text node with optional read-only pointer selection and keyboard copying.</summary>
    public LayoutNode DrawText(
        string text,
        bool selectable,
        float size = 0,
        Color? color = null,
        Font? font = null,
        float wrapWidth = 0,
        bool centerInRect = true,
        bool clip = false,
        TextEffects? effects = null,
        TextLayoutOptions? layout = null)
    {
        return DrawTextOrGlyph(new DrawConfig(
            text,
            font ?? CurrentNodeScope.Get<LayoutNodeScopeTextFont>().Value,
            size, color, centerInRect, clip, wrapWidth,
            ResolveTextEffects(effects, CurrentNodeScope), layout, selectable));
    }

    /// <summary>
    /// Draws a glyph (icon) as a layout node: in Pass1Build phase, creates a node sized to the glyph; in Pass2Render phase, draws the glyph in the node's rect.
    /// Returns the node for layout chaining.
    /// </summary>
    public LayoutNode DrawGlyph(
        char iconCode,
        float size = 0,
        Color? color = null,
        Font? font = null,
        bool centerInRect = true,
        bool clip = false,
        TextEffects? effects = null)
    {
        return DrawTextOrGlyph(new DrawConfig(
            iconCode.ToString(),
            font ?? CurrentNodeScope.Get<LayoutNodeScopeWidgetIconFont>().Value,
            size, color, centerInRect, clip, 0,
            ResolveTextEffects(effects, CurrentNodeScope)));
    }

    static TextEffects? ResolveTextEffects(TextEffects? explicitEffects, LayoutNodeScope scope)
    {
        if (explicitEffects is { } explicitValue)
        {
            return explicitValue;
        }

        var inherited = scope.Get<LayoutNodeScopeTextEffects>().Value;
        return inherited == default ? null : inherited;
    }

    /// <summary>
    /// Where a line's baseline sits inside its line box, centring the ascent and descent. Placing it
    /// at the bottom of the box left every glyph sitting low with no room for descenders.
    /// </summary>
    static float Baseline(Font font, float lineHeight)
    {
        var metrics = font.SkFont.Metrics;
        var ascent = -metrics.Ascent;
        var descent = metrics.Descent;

        return ((lineHeight - (ascent + descent)) / 2f) + ascent;
    }

    record struct DrawConfig(
        string Text,
        Font Font,
        float Size,
        Color? Color,
        bool Center,
        bool Clip,
        float WrapWidth,
        TextEffects? Effects = null,
        TextLayoutOptions? Layout = null,
        bool Selectable = false);

    LayoutNode DrawTextOrGlyph(DrawConfig cfg)
    {
        var size = (cfg.Size > 0 ? cfg.Size : CurrentNodeScope.Get<LayoutNodeScopeTextSize>().Value) * FontScale;
        var color = cfg.Color ?? CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value;

        var mainFont = cfg.Font.Resized(size);
        var iconFont =
            new Font(new SKFont(CurrentNodeScope.Get<LayoutNodeScopeIconFont>().Value.SkFont.Typeface, size));

        // Handle text wrapping if wrapWidth is specified
        var layout = cfg.Layout ?? CurrentNodeScope.Get<LayoutNodeScopeTextLayout>().Value;
        var wrappedLines = WrappedTextLayout.Wrap(cfg.Text, cfg.WrapWidth,
            line => MeasureLineWidth(line, mainFont, iconFont), layout);

        var maxWidth = 0f;
        var lineHeight = size * layout.LineHeight;

        foreach (var wrappedLine in wrappedLines)
        {
            var lineWidth = MeasureLineWidth(wrappedLine.Text, mainFont, iconFont);
            maxWidth = Math.Max(maxWidth, lineWidth);
        }

        // If wrapping is enabled, use the wrap width as max width
        if (cfg.WrapWidth > 0 && layout.WrapMode != TextWrapMode.None)
        {
            maxWidth = Math.Min(maxWidth, cfg.WrapWidth);
        }

        var totalHeight = wrappedLines.Count * lineHeight;

        var node = Node(maxWidth, totalHeight);
        RenderTextNode(cfg, node, wrappedLines, mainFont, iconFont, lineHeight, color);
        return node;
    }

    void RenderTextNode(DrawConfig cfg, LayoutNode node, IReadOnlyList<WrappedLine> wrappedLines,
        Font mainFont, Font iconFont, float lineHeight, Color color)
    {
        if (cfg.Selectable)
        {
            node.Cursor(PointerCursor.Text);
        }

        if (Pass != Pass.Pass2Render)
        {
            return;
        }

        var selection = cfg.Selectable
            ? ProcessTextSelection(node, cfg.Text, wrappedLines, mainFont, iconFont, lineHeight, cfg.Center)
            : null;
        var layers = BuildTextPaints(color, cfg.Effects, node.InnerRect);

        // Draw each line
        for (var i = 0; i < wrappedLines.Count; i++)
        {
            var line = wrappedLines[i].Text;
            var lineWidth = MeasureLineWidth(line, mainFont, iconFont);

            var pos = node.InnerRect.Position;
            pos.Y += (i * lineHeight) + Baseline(mainFont, lineHeight);

            if (cfg.Center)
            {
                pos.X += Math.Max((node.InnerRect.W - lineWidth) * 0.5f, 0f);
            }

            if (selection is not null)
            {
                DrawTextSelection(node, selection, wrappedLines[i], mainFont, iconFont,
                    new Rect(pos.X, node.InnerRect.Y + i * lineHeight, lineWidth, lineHeight));
            }

            DrawLineWithFallback(line, pos, mainFont, iconFont, layers, cfg.Effects, cfg.Clip, node);
        }

    }

    /// <summary>
    /// Builds the ordered layers one line of text is drawn with, back to front: drop shadow,
    /// outline, fill, inner shadow. Each layer carries a position offset (used for the inner
    /// shadow). With no <see cref="TextEffects"/> this is a single flat-color fill.
    /// </summary>
    static List<(SKPaint Paint, Vector2 Offset)> BuildTextPaints(Color color, TextEffects? effects, Rect bounds)
    {
        if (effects is null)
        {
            return [(new SKPaint { IsAntialias = true, Color = color }, Vector2.Zero)];
        }

        var effect = effects.Value;
        var layers = new List<(SKPaint, Vector2)>(4 + (effect.DropShadows?.Count ?? 0));
        AddLegacyDropShadow(layers, effect.DropShadow);
        AddDropShadows(layers, effect.DropShadows);
        AddOutline(layers, effect.Outline);
        layers.Add((BuildFillPaint(color, effect.Gradient, bounds), Vector2.Zero));
        AddInnerShadow(layers, effect.InnerShadow);
        return layers;
    }

    static void AddLegacyDropShadow(List<(SKPaint, Vector2)> layers, TextEffects.TextShadow? shadow)
    {
        if (shadow is { } value)
        {
            layers.Add((BuildOuterShadowPaint(value), Vector2.Zero));
        }
    }

    static void AddDropShadows(List<(SKPaint, Vector2)> layers, IReadOnlyList<TextEffects.TextShadow>? shadows)
    {
        if (shadows is null)
        {
            return;
        }

        for (var i = shadows.Count - 1; i >= 0; i--)
        {
            layers.Add((BuildOuterShadowPaint(shadows[i]), Vector2.Zero));
        }
    }

    static void AddOutline(List<(SKPaint, Vector2)> layers, TextEffects.TextOutline? outline)
    {
        if (outline is not { Width: > 0 } value)
        {
            return;
        }

        layers.Add((new SKPaint
        {
            IsAntialias = true,
            Color = value.Color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = value.Width * 2f, // half is hidden behind the fill
            StrokeJoin = SKStrokeJoin.Round,
        }, Vector2.Zero));
    }

    static SKPaint BuildFillPaint(Color color, TextEffects.TextGradient? gradient, Rect bounds)
    {
        var fill = new SKPaint { IsAntialias = true, Color = color };
        if (gradient is { } value)
        {
            fill.Shader = BuildGradientShader(value, bounds);
        }

        return fill;
    }

    static void AddInnerShadow(List<(SKPaint, Vector2)> layers, TextEffects.TextShadow? shadow)
    {
        if (shadow is not { } inner)
        {
            return;
        }

        layers.Add((new SKPaint
        {
            IsAntialias = true,
            Color = inner.Color,
            BlendMode = SKBlendMode.SrcATop,
            ImageFilter = inner.Blur > 0f ? SKImageFilter.CreateBlur(inner.Blur, inner.Blur) : null,
        }, inner.Offset));
    }

    static SKPaint BuildOuterShadowPaint(TextEffects.TextShadow shadow) => new()
    {
        IsAntialias = true,
        Color = shadow.Color,
        ImageFilter = SKImageFilter.CreateDropShadowOnly(
            shadow.Offset.X, shadow.Offset.Y, shadow.Blur, shadow.Blur, shadow.Color),
    };

    static SKShader BuildGradientShader(TextEffects.TextGradient gradient, Rect bounds)
    {
        var rect = new SKRect(bounds.X, bounds.Y, bounds.X + bounds.W, bounds.Y + bounds.H);
        SKColor[] colors = [gradient.From, gradient.To];

        if (gradient.Radial)
        {
            return SKShader.CreateRadialGradient(
                new SKPoint(rect.MidX, rect.MidY),
                Math.Max(rect.Width, rect.Height) * 0.5f,
                colors, null, SKShaderTileMode.Clamp);
        }

        var radians = gradient.AngleDegrees * MathF.PI / 180f;
        var half = Math.Max(rect.Width, rect.Height) * 0.5f;
        var dx = MathF.Cos(radians) * half;
        var dy = MathF.Sin(radians) * half;

        return SKShader.CreateLinearGradient(
            new SKPoint(rect.MidX - dx, rect.MidY - dy),
            new SKPoint(rect.MidX + dx, rect.MidY + dy),
            colors, null, SKShaderTileMode.Clamp);
    }

    /// <summary>
    /// Measures the width of a line of text with font fallback support.
    /// </summary>
    internal float MeasureLineWidth(string line, Font mainFont, Font iconFont, LayoutNodeScope? scope = null)
    {
        var runs = CreateFontRuns(line, mainFont, iconFont, scope);
        var totalWidth = 0f;

        foreach (var run in runs)
        {
            run.Font.SkFont.MeasureText(run.Text, out var bounds);
            totalWidth += bounds.Width;
        }

        return totalWidth;
    }

    /// <summary>
    /// Draws a line of text with font fallback support.
    /// </summary>
    void DrawLineWithFallback(string line, Vector2 startPos, Font mainFont, Font iconFont,
        IReadOnlyList<(SKPaint Paint, Vector2 Offset)> layers, TextEffects? effects, bool clip, LayoutNode node)
    {
        var runs = CreateFontRuns(line, mainFont, iconFont);

        // Precompute each run's x once, then draw the whole line per layer (back to front).
        var positions = new Vector2[runs.Count];
        var currentX = startPos.X;
        for (var i = 0; i < runs.Count; i++)
        {
            positions[i] = new Vector2(currentX, startPos.Y);
            runs[i].Font.SkFont.MeasureText(runs[i].Text, out var bounds);
            currentX += bounds.Width;
        }

        foreach (var (paint, offset) in layers)
        {
            for (var i = 0; i < runs.Count; i++)
            {
                AddDraw(new Text(runs[i].Text, positions[i] + offset, runs[i].Font.SkFont, paint, effects), clip, node);
            }
        }
    }

}
