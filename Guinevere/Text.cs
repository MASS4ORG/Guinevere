namespace Guinevere;

/// <summary>
/// Represents a drawable text element that can be rendered onto a canvas.
/// Instances of this class encapsulate text rendering properties such as
/// the text content, position, font, and paint settings.
/// </summary>
public class Text(string label, Vector2? position = null, SKFont? font = null, SKPaint? paint = null)
    : IDrawable, IInkBounds
{
    internal Text(string label, Vector2? position, SKFont? font, SKPaint? paint, TextEffects? effects)
        : this(label, position, font, paint) => Effects = effects;

    /// <summary>Draws positioned glyphs, so spacing, kerning and justification match the layout exactly.</summary>
    internal Text(SKTextBlob blob, string label, Vector2 position, SKFont font, SKPaint paint, TextEffects? effects)
        : this(label, position, font, paint, effects) => Blob = blob;

    internal TextEffects? Effects { get; }

    internal SKTextBlob? Blob { get; }

    /// <inheritdoc/>
    SKRect? IInkBounds.InkBounds(LayoutNode node)
    {
        if (Blob is null && string.IsNullOrEmpty(Label))
        {
            return SKRect.Empty;
        }

        var glyphs = Blob?.Bounds ?? MeasureLabel();
        glyphs.Offset(Position.X, Position.Y);
        var ink = SKRect.Inflate(glyphs, 1f, 1f);
        if (Effects is not { } value)
        {
            return Ink.Painted(ink, Paint);
        }

        return WithTextEffects(ink, glyphs, Paint, value);
    }

    SKRect MeasureLabel()
    {
        var measured = Font ?? new SKFont();
        measured.MeasureText(Label, out var glyphs);
        return glyphs;
    }

    static SKRect? WithTextEffects(SKRect ink, SKRect glyphs, SKPaint? paint, TextEffects effects)
    {
        if (paint?.MaskFilter is not null)
        {
            return null;
        }

        ink = AddStrokeBounds(ink, paint);
        ink = AddOutlineBounds(ink, effects.Outline);
        return AddShadowBounds(ink, glyphs, effects);
    }

    static SKRect AddStrokeBounds(SKRect ink, SKPaint? paint)
    {
        if (paint is not { Style: not SKPaintStyle.Fill })
        {
            return ink;
        }

        var stroke = paint.StrokeWidth * 0.5f + 1f;
        return SKRect.Inflate(ink, stroke, stroke);
    }

    static SKRect AddOutlineBounds(SKRect ink, TextEffects.TextOutline? outline)
    {
        if (outline is not { Width: > 0 } value)
        {
            return ink;
        }

        return SKRect.Inflate(ink, value.Width, value.Width);
    }

    static SKRect AddShadowBounds(SKRect ink, SKRect glyphs, TextEffects effects)
    {
        if (effects.DropShadow is { } drop)
        {
            ink = JoinShadow(ink, glyphs, drop);
        }

        ink = AddDropShadows(ink, glyphs, effects.DropShadows);
        if (effects.InnerShadow is { } inner)
        {
            ink = JoinShadow(ink, glyphs, inner);
        }

        return ink;
    }

    static SKRect AddDropShadows(SKRect ink, SKRect glyphs, IReadOnlyList<TextEffects.TextShadow>? shadows)
    {
        if (shadows is null)
        {
            return ink;
        }

        for (var i = 0; i < shadows.Count; i++)
        {
            ink = JoinShadow(ink, glyphs, shadows[i]);
        }

        return ink;
    }

    static SKRect JoinShadow(SKRect ink, SKRect glyphs, TextEffects.TextShadow shadow)
    {
        var blur = Math.Max(shadow.Blur, 0f) * 3f + 1f;
        var shadowBounds = SKRect.Inflate(glyphs, blur, blur);
        shadowBounds.Offset(shadow.Offset.X, shadow.Offset.Y);
        return Ink.Join(ink, shadowBounds);
    }

    /// <summary>
    /// Gets the label text to be rendered. This property contains the string value
    /// that represents the textual content associated with this drawing instance.
    /// </summary>
    public string? Label { get; } = label;

    /// <summary>
    /// Gets the <see cref="SKFont"/> defining the font style and typeface used
    /// to render the text. This property specifies the visual appearance of the
    /// text, including weight, size, and family.
    /// </summary>
    public SKFont? Font { get; } = font;

    /// <summary>
    /// Gets the <see cref="Vector2"/> representing the position of the text.
    /// This property defines the coordinates within the canvas where the text
    /// will be drawn.
    /// </summary>
    public Vector2 Position { get; } = position ?? Vector2.Zero;

    /// <summary>
    /// Gets or sets the <see cref="SKPaint"/> object used to define the paint style
    /// for rendering the text. This includes settings such as color, stroke, and fill
    /// for text drawing operations.
    /// </summary>
    public SKPaint? Paint { get; set; } = paint;

    /// <summary>
    /// Renders the specified text onto the provided canvas using the given parameters.
    /// Scroll offsets are now handled during layout calculation.
    /// </summary>
    /// <param name="gui">The GUI context used for rendering the text.</param>
    /// <param name="node">The layout node that the text is associated with.</param>
    /// <param name="canvas">The canvas onto which the text will be drawn.</param>
    public void Render(Gui gui, LayoutNode node, SKCanvas canvas)
    {
        if (Blob is not null)
        {
            canvas.DrawText(Blob, Position.X, Position.Y, Paint ?? new SKPaint());
            return;
        }

        canvas.DrawText(Label, Position, SKTextAlign.Left, Font, Paint);
    }

    /// <summary>
    /// Defines an implicit conversion operator that enables a string to be seamlessly converted into a Text instance.
    /// </summary>
    /// <param name="text">The string to convert into a Text instance.</param>
    /// <returns>A new instance of the Text class constructed from the specified string.</returns>
    public static implicit operator Text(string text) => new(text);

    /// <summary>
    /// Defines an implicit conversion operator that allows a string to be converted to a Text instance.
    /// </summary>
    /// <param name="text">The string to be converted to a Text instance.</param>
    /// <returns>A new Text instance representing the given string.</returns>
    public static implicit operator Text(char text) => new(text.ToString());
}
