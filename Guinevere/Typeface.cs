namespace Guinevere;

/// <summary>
/// Represents a typeface wrapper that provides an abstraction layer over SkiaSharp's SKTypeface.
/// </summary>
public class Typeface
{
    readonly SKTypeface _skTypeface;

    /// <summary>
    /// Initializes a new instance of the Typeface class.
    /// </summary>
    /// <param name="skTypeface">The SkiaSharp typeface to wrap.</param>
    internal Typeface(SKTypeface skTypeface)
    {
        _skTypeface = skTypeface;
    }

    /// <summary>
    /// Gets the family name of the typeface.
    /// </summary>
    public string FamilyName => _skTypeface.FamilyName;

    /// <summary>
    /// Gets the style of the typeface.
    /// </summary>
    public FontStyle Style => _skTypeface.FontStyle.Slant switch
    {
        SKFontStyleSlant.Upright when _skTypeface.FontStyle.Weight == (int)SKFontStyleWeight.Bold => FontStyle.Bold,
        SKFontStyleSlant.Italic when _skTypeface.FontStyle.Weight == (int)SKFontStyleWeight.Bold =>
            FontStyle.BoldItalic,
        SKFontStyleSlant.Italic => FontStyle.Italic,
        _ => FontStyle.Normal
    };

    /// <summary>
    /// Gets the underlying SkiaSharp typeface object.
    /// This property is used internally by the framework and should not be accessed directly by user code.
    /// </summary>
    internal SKTypeface SkTypeface => _skTypeface;

    /// <summary>
    /// Creates a Typeface from a file path.
    /// </summary>
    /// <param name="fontPath">The path to the font file.</param>
    /// <returns>A new Typeface instance loaded from the specified file.</returns>
    public static Typeface FromFile(string fontPath)
    {
        var skTypeface = SKTypeface.FromFile(fontPath);
        return new Typeface(skTypeface);
    }

    /// <summary>
    /// Creates a Typeface from a stream.
    /// </summary>
    /// <param name="fontStream">The stream containing the font data.</param>
    /// <returns>A new Typeface instance loaded from the specified stream.</returns>
    public static Typeface FromStream(Stream fontStream)
    {
        var data = SKData.Create(fontStream);
        var skTypeface = SKTypeface.FromData(data);
        return new Typeface(skTypeface);
    }

    /// <summary>
    /// Creates a Typeface from a system font family name.
    /// </summary>
    /// <param name="familyName">The name of the font family.</param>
    /// <param name="style">The style of the font. Default is Normal.</param>
    /// <returns>A new Typeface instance with the specified family name and style.</returns>
    public static Typeface FromFamilyName(string familyName, FontStyle style = FontStyle.Normal)
    {
        var skStyle = style switch
        {
            FontStyle.Normal => SKFontStyle.Normal,
            FontStyle.Bold => SKFontStyle.Bold,
            FontStyle.Italic => SKFontStyle.Italic,
            FontStyle.BoldItalic => SKFontStyle.BoldItalic,
            _ => SKFontStyle.Normal
        };

        var skTypeface = SKTypeface.FromFamilyName(familyName, skStyle);
        return new Typeface(skTypeface);
    }

    /// <summary>
    /// Releases all resources used by the Typeface.
    /// </summary>
    public void Dispose()
    {
        _skTypeface.Dispose();
    }
}
