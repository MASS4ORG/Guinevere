namespace Guinevere;

public partial class Gui
{
    /// <summary>
    /// Draws text as a layout node: in Pass1Build phase, creates a node sized to the text; in Pass2Render phase, draws the text in the node's rect.
    /// Returns the node for layout chaining.
    /// </summary>
    public LayoutNode DrawText(
        string text,
        float size = 0,
        Color? color = null,
        Font? font = null,
        float wrapWidth = 0,
        bool centerInRect = true,
        bool clip = false,
        TextEffects? effects = null)
    {
        return DrawTextOrGlyph(new DrawConfig(
            text,
            font ?? CurrentNodeScope.Get<LayoutNodeScopeTextFont>().Value,
            size, color, centerInRect, clip, wrapWidth, effects));
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
            font ?? CurrentNodeScope.Get<LayoutNodeScopeIconFont>().Value,
            size, color, centerInRect, clip, 0, effects));
    }

    private record struct DrawConfig(
        string Text,
        Font Font,
        float Size,
        Color? Color,
        bool Center,
        bool Clip,
        float WrapWidth,
        TextEffects? Effects = null);

    private record struct FontRun(
        string Text,
        Font Font,
        int StartIndex,
        int Length);

    /// <summary>
    /// Checks if a character is supported by the given font by querying the underlying typeface.
    /// Returns true if the font contains a glyph for the specified character, false otherwise.
    /// This is used to determine when to fall back to the icon font for unsupported characters.
    /// </summary>
    /// <param name="font">The font to check for character support.</param>
    /// <param name="character">The character to test for support.</param>
    /// <returns>True if the font supports the character, false if fallback is needed.</returns>
    private static bool IsCharacterSupported(Font font, char character)
    {
        return font.SkFont.Typeface.GetGlyph(character) != 0;
    }

    /// <summary>
    /// Splits text into runs where each run uses the same font (either main font or icon font fallback).
    /// </summary>
    private List<FontRun> CreateFontRuns(string text, Font mainFont, Font iconFont)
    {
        var runs = new List<FontRun>();
        if (string.IsNullOrEmpty(text))
            return runs;

        var currentRunStart = 0;
        var currentFont = IsCharacterSupported(mainFont, text[0]) ? mainFont : iconFont;

        for (var i = 1; i < text.Length; i++)
        {
            var charFont = IsCharacterSupported(mainFont, text[i]) ? mainFont : iconFont;

            if (charFont != currentFont)
            {
                // End current run and start a new one
                runs.Add(new FontRun(
                    text.Substring(currentRunStart, i - currentRunStart),
                    currentFont,
                    currentRunStart,
                    i - currentRunStart));

                currentRunStart = i;
                currentFont = charFont;
            }
        }

        // Add the final run
        runs.Add(new FontRun(
            text.Substring(currentRunStart),
            currentFont,
            currentRunStart,
            text.Length - currentRunStart));

        return runs;
    }

    private LayoutNode DrawTextOrGlyph(DrawConfig cfg)
    {
        var size = cfg.Size > 0 ? cfg.Size : CurrentNodeScope.Get<LayoutNodeScopeTextSize>().Value;
        var color = cfg.Color ?? CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value;

        var mainFont = new Font(new SKFont(cfg.Font.SkFont.Typeface, size));
        var iconFont =
            new Font(new SKFont(CurrentNodeScope.Get<LayoutNodeScopeIconFont>().Value.SkFont.Typeface, size));

        // Handle text wrapping if wrapWidth is specified
        var lines = cfg.WrapWidth > 0
            ? WrapTextWithFallback(cfg.Text, mainFont, iconFont, cfg.WrapWidth)
            : cfg.Text.Split('\n');

        var maxWidth = 0f;
        var lineHeight = size * 1.2f; // Add some line spacing

        foreach (var line in lines)
        {
            var lineWidth = MeasureLineWidth(line, mainFont, iconFont);
            maxWidth = Math.Max(maxWidth, lineWidth);
        }

        // If wrapping is enabled, use the wrap width as max width
        if (cfg.WrapWidth > 0) maxWidth = Math.Min(maxWidth, cfg.WrapWidth);

        var totalHeight = lines.Length * lineHeight;

        var node = Node(maxWidth, totalHeight);

        if (Pass != Pass.Pass2Render)
            return node;

        var layers = BuildTextPaints(color, cfg.Effects, node.InnerRect);

        // Draw each line
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineWidth = MeasureLineWidth(line, mainFont, iconFont);

            var pos = node.InnerRect.Position;
            pos.Y += (i + 1) * lineHeight; // Move down for each line

            if (cfg.Center) pos.X += Math.Max((node.InnerRect.W - lineWidth) * 0.5f, 0f);

            DrawLineWithFallback(line, pos, mainFont, iconFont, layers, cfg.Clip, node);
        }

        return node;
    }

    /// <summary>
    /// Builds the ordered layers one line of text is drawn with, back to front: drop shadow,
    /// outline, fill, inner shadow. Each layer carries a position offset (used for the inner
    /// shadow). With no <see cref="TextEffects"/> this is a single flat-colour fill.
    /// </summary>
    private static List<(SKPaint Paint, Vector2 Offset)> BuildTextPaints(Color color, TextEffects? effects, Rect bounds)
    {
        if (effects is null)
            return [(new SKPaint { IsAntialias = true, Color = color }, Vector2.Zero)];

        var layers = new List<(SKPaint, Vector2)>(4);

        if (effects.DropShadow is { } drop)
        {
            layers.Add((new SKPaint
            {
                IsAntialias = true,
                Color = drop.Color,
                ImageFilter = SKImageFilter.CreateDropShadowOnly(
                    drop.Offset.X, drop.Offset.Y, drop.Blur, drop.Blur, drop.Color),
            }, Vector2.Zero));
        }

        if (effects.Outline is { } outline && outline.Width > 0f)
        {
            layers.Add((new SKPaint
            {
                IsAntialias = true,
                Color = outline.Color,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = outline.Width * 2f, // half is hidden behind the fill
                StrokeJoin = SKStrokeJoin.Round,
            }, Vector2.Zero));
        }

        var fill = new SKPaint { IsAntialias = true, Color = color };
        if (effects.Gradient is { } gradient)
            fill.Shader = BuildGradientShader(gradient, bounds);
        layers.Add((fill, Vector2.Zero));

        if (effects.InnerShadow is { } inner)
        {
            // The offset, blurred copy in shadow colour, kept only where the fill already painted:
            // a soft dark band along the offset edge, inside the glyphs.
            layers.Add((new SKPaint
            {
                IsAntialias = true,
                Color = inner.Color,
                BlendMode = SKBlendMode.SrcATop,
                ImageFilter = inner.Blur > 0f ? SKImageFilter.CreateBlur(inner.Blur, inner.Blur) : null,
            }, inner.Offset));
        }

        return layers;
    }

    private static SKShader BuildGradientShader(TextEffects.TextGradient gradient, Rect bounds)
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
    private float MeasureLineWidth(string line, Font mainFont, Font iconFont)
    {
        var runs = CreateFontRuns(line, mainFont, iconFont);
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
    private void DrawLineWithFallback(string line, Vector2 startPos, Font mainFont, Font iconFont,
        IReadOnlyList<(SKPaint Paint, Vector2 Offset)> layers, bool clip, LayoutNode node)
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
                AddDraw(new Text(runs[i].Text, positions[i] + offset, runs[i].Font.SkFont, paint), clip, node);
        }
    }

    private string[] WrapText(string text, SKFont font, float maxWidth)
    {
        var lines = new List<string>();
        var paragraphs = text.Split('\n');

        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrEmpty(paragraph))
            {
                lines.Add("");
                continue;
            }

            var words = paragraph.Split(' ');
            var currentLine = "";

            foreach (var word in words)
            {
                var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                font.MeasureText(testLine, out var bounds);

                if (bounds.Width <= maxWidth)
                {
                    currentLine = testLine;
                }
                else
                {
                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        lines.Add(currentLine);
                        currentLine = word;
                    }
                    else
                    {
                        // Single word is too long, add it anyway
                        lines.Add(word);
                    }
                }
            }

            if (!string.IsNullOrEmpty(currentLine)) lines.Add(currentLine);
        }

        return lines.ToArray();
    }

    /// <summary>
    /// Wraps text with font fallback support.
    /// </summary>
    private string[] WrapTextWithFallback(string text, Font mainFont, Font iconFont, float maxWidth)
    {
        var lines = new List<string>();
        var paragraphs = text.Split('\n');

        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrEmpty(paragraph))
            {
                lines.Add("");
                continue;
            }

            var words = paragraph.Split(' ');
            var currentLine = "";

            foreach (var word in words)
            {
                var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                var testWidth = MeasureLineWidth(testLine, mainFont, iconFont);

                if (testWidth <= maxWidth)
                {
                    currentLine = testLine;
                }
                else
                {
                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        lines.Add(currentLine);
                        currentLine = word;
                    }
                    else
                    {
                        // Single word is too long, add it anyway
                        lines.Add(word);
                    }
                }
            }

            if (!string.IsNullOrEmpty(currentLine)) lines.Add(currentLine);
        }

        return lines.ToArray();
    }
}
