namespace Guinevere;

/// <summary>
/// Represents a drawable text element that can be rendered onto a canvas.
/// Instances of this class encapsulate text rendering properties such as
/// the text content, position, font, and paint settings.
/// </summary>
public class Text(string label, Vector2? position = null, SKFont? font = null, SKPaint? paint = null)
    : IDrawable
{
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
