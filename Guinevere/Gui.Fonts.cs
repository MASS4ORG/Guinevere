namespace Guinevere;

public partial class Gui
{
    /// <summary>Display scaling applied to text sizes in framebuffer coordinates.</summary>
    public float FontScale { get; private set; } = 1f;

    /// <summary>Returns the scope font at the requested logical size, including display scaling and fallback.</summary>
    public Font GetTextFont(float size = 0, LayoutNodeScope? scope = null)
    {
        scope ??= CurrentNodeScope;
        var effectiveSize = size > 0 ? size : scope.Get<LayoutNodeScopeTextSize>().Value;
        return scope.Get<LayoutNodeScopeTextFont>().Value.Resized(effectiveSize * FontScale);
    }

    /// <summary>Measures text with the same scope font, display scale and glyph fallback used by DrawText.</summary>
    public float MeasureTextWidth(string text, float size = 0, LayoutNodeScope? scope = null)
    {
        scope ??= CurrentNodeScope;
        var font = GetTextFont(size, scope);
        var emoji = scope.Get<LayoutNodeScopeIconFont>().Value.Resized(font.Size);
        return MeasureLineWidth(text, font, emoji, scope);
    }

    List<FontRun> CreateFontRuns(string text, Font mainFont, Font iconFont, LayoutNodeScope? scope = null)
    {
        scope ??= CurrentNodeScope;
        return FontTextLayout.Create(text, mainFont,
            scope.Get<LayoutNodeScopeWidgetIconFont>().Value, iconFont);
    }

    /// <summary>Splits text by Unicode scalar into the same font runs used for measurement and drawing.</summary>
    internal IReadOnlyList<(string Text, Font Font)> CreateTextRuns(string text, Font mainFont, Font iconFont,
        LayoutNodeScope? scope = null) =>
        CreateFontRuns(text, mainFont, iconFont, scope).Select(run => (run.Text, run.Font)).ToArray();

    void ApplyFontScale()
    {
        var scale = Platform.TryGet<IDisplayCapability>(out var display) ? display!.ScaleFactor : 1f;
        scale = float.IsFinite(scale) && scale > 0f ? scale : 1f;
        var matrix = Canvas!.TotalMatrix;
        var canvasScale = MathF.Sqrt(matrix.ScaleX * matrix.ScaleX + matrix.SkewY * matrix.SkewY);
        FontScale = canvasScale > 0f ? scale / canvasScale : scale;
    }
}
