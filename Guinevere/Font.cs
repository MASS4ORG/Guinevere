using System.Runtime.CompilerServices;

namespace Guinevere;

/// <summary>
/// Represents a font wrapper that provides an abstraction layer over SkiaSharp's SKFont,
/// allowing users to work with fonts without directly depending on SkiaSharp.
/// </summary>
public class Font
{
    readonly SKFont _skFont;

    /// <summary>
    ///
    /// </summary>
    internal Font()
        : this(new SKFont())
    {
    }

    /// <summary>
    /// Initializes a new instance of the Font class with the specified typeface and size.
    /// </summary>
    /// <param name="typeface">The typeface to use for the font.</param>
    /// <param name="size">The size of the font in points. Default is 12.</param>
    Font(Typeface typeface, float size = 12f)
        : this(new SKFont(typeface.SkTypeface, size))
    {
    }

    /// <summary>
    /// Initializes a new instance of the Font class from an existing SKFont.
    /// Used internally by the framework and for font fallback functionality.
    /// </summary>
    /// <param name="skFont">The SkiaSharp font to wrap.</param>
    public Font(SKFont skFont)
    {
        _skFont = skFont;
    }

    /// <summary>
    /// Gets or sets the size of the font in points.
    /// </summary>
    public float Size
    {
        get => _skFont.Size;
        set => _skFont.Size = value;
    }

    /// <summary>
    /// Gets the typeface of the font.
    /// </summary>
    Typeface Typeface => new(_skFont.Typeface);

    /// <summary>
    /// Gets the underlying SkiaSharp font object.
    /// This property is used internally by the framework and should not be accessed directly by user code.
    /// </summary>
    internal SKFont SkFont => _skFont;

    /// <summary>
    /// Measures the dimensions of the specified text when rendered with this font.
    /// </summary>
    /// <param name="text">The text to measure.</param>
    /// <returns>A rectangle representing the bounds of the text.</returns>
    public Rect MeasureText(string text)
    {
        _skFont.MeasureText(text, out var bounds);
        return new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
    }

    /// <summary>
    /// Creates a new Font instance with the specified size while keeping the same typeface.
    /// </summary>
    /// <param name="size">The new size for the font in points.</param>
    /// <returns>A new Font instance with the specified size.</returns>
    public Font WithSize(float size)
    {
        return new Font(Typeface, size);
    }

    /// <summary>
    /// Creates a Font from a file path.
    /// </summary>
    /// <param name="fontPath">The path to the font file.</param>
    /// <param name="size">The size of the font in points. Default is 12.</param>
    /// <returns>A new Font instance loaded from the specified file.</returns>
    public static Font FromFile(string fontPath, float size = 12f)
    {
        var typeface = SKTypeface.FromFile(fontPath);
        return new Font(new Typeface(typeface), size);
    }

    /// <summary>
    /// Creates a Font from a stream.
    /// </summary>
    /// <param name="fontStream">The stream containing the font data.</param>
    /// <param name="size">The size of the font in points. Default is 12.</param>
    /// <returns>A new Font instance loaded from the specified stream.</returns>
    public static Font FromStream(Stream fontStream, float size = 12f)
    {
        var data = SKData.Create(fontStream);
        var typeface = SKTypeface.FromData(data);
        return new Font(new Typeface(typeface), size);
    }

    /// <summary>
    /// Creates a Font from a system font family name.
    /// </summary>
    /// <param name="familyName">The name of the font family.</param>
    /// <param name="size">The size of the font in points. Default is 12.</param>
    /// <param name="style">The style of the font. Default is Normal.</param>
    /// <returns>A new Font instance with the specified family name and style.</returns>
    public static Font FromFamilyName(string familyName, float size = 12f, FontStyle style = FontStyle.Normal)
    {
        var skStyle = style switch
        {
            FontStyle.Normal => SKFontStyle.Normal,
            FontStyle.Bold => SKFontStyle.Bold,
            FontStyle.Italic => SKFontStyle.Italic,
            FontStyle.BoldItalic => SKFontStyle.BoldItalic,
            _ => SKFontStyle.Normal
        };

        var typeface = SKTypeface.FromFamilyName(familyName, skStyle);
        return new Font(new Typeface(typeface), size);
    }

    /// <summary>
    /// Releases all resources used by the Font.
    /// </summary>
    public void Dispose()
    {
        _skFont.Dispose();
    }

    /// <summary>
    /// Loads a font from the specified file path.
    /// </summary>
    /// <param name="fontPath">The path to the font file to load.</param>
    /// <param name="member">The name of the calling member for diagnostic purposes. Defaults to the name of the caller.</param>
    /// <returns>A Font object representing the loaded font.</returns>
    /// <exception cref="NotImplementedException">Thrown when the method is not yet implemented.</exception>
    public static Font LoadFont(string fontPath, [CallerMemberName] string? member = null)
    {
        throw new NotImplementedException();
    }
}
