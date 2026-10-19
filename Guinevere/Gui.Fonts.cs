namespace Guinevere;

public partial class Gui
{
    /// <summary>Text scaling in layout coordinates; viewport geometry applies its uniform scale separately.</summary>
    public float FontScale => LayoutNodeScopeStack.TryPeek(out var scope) ? FontScaleFor(scope) : _fontScale;

    float _fontScale = 1f;

    float FontScaleFor(LayoutNodeScope scope) =>
        scope.Get<LayoutNodeScopeViewport>().Value?.TextScale ?? _fontScale;

    /// <summary>Returns the scope font at the requested logical size, including display scaling and fallback.</summary>
    public Font GetTextFont(float size = 0, LayoutNodeScope? scope = null)
    {
        scope ??= CurrentNodeScope;
        var effectiveSize = size > 0 ? size : scope.Get<LayoutNodeScopeTextSize>().Value;
        return scope.Get<LayoutNodeScopeTextFont>().Value.Resized(effectiveSize * FontScaleFor(scope));
    }

    static readonly TextLayoutOptions UnwrappedText = new() { WrapMode = TextWrapMode.None };

    /// <summary>Measures text with the same scope font, display scale, glyph fallback and layout as DrawText.</summary>
    public float MeasureTextWidth(string text, float size = 0, LayoutNodeScope? scope = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        scope ??= CurrentNodeScope;
        return LayoutText(TextRequest(RichTextContent.Plain(text), null, size, 0f, UnwrappedText,
            TextAlignment.Start, scope)).Width;
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
        _fontScale = canvasScale > 0f ? scale / canvasScale : scale;
    }
}
