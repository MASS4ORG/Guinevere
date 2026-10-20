namespace Guinevere;

public partial class Gui
{
    /// <summary>
    /// Draws text as a layout node: in Pass1Build phase, creates a node sized to the text; in Pass2Render phase, draws the text in the node's rect.
    /// Returns the node for layout chaining; selectable text supports pointer selection and copying without editing.
    /// A <paramref name="wrapWidth"/> of <see cref="TextLayoutOptions.FillWidth"/> wraps to the node's laid-out width.
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
        var color = cfg.Color ?? CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value;
        var options = cfg.Layout ?? CurrentNodeScope.Get<LayoutNodeScopeTextLayout>().Value;
        var alignment = options.Alignment ?? (cfg.Center ? TextAlignment.Center : TextAlignment.Start);
        var (node, layout) = TextBlock(cfg.Text, cfg.Font, cfg.Size, cfg.WrapWidth, options,
            alignment);
        if (cfg.Selectable)
        {
            node.Cursor(PointerCursor.Text);
        }

        if (Pass != Pass.Pass2Render)
        {
            return node;
        }

        var origin = TextOrigin(node, layout, alignment);
        if (cfg.Selectable)
        {
            DrawTextSelection(node, ProcessTextSelection(node, cfg.Text, layout, origin), layout, origin);
        }

        DrawTextLayout(layout, origin, color, cfg.Effects, cfg.Clip, node);
        return node;
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
            totalWidth += run.Font.SkFont.MeasureText(run.Text);
        }

        return totalWidth;
    }
}
