namespace Guinevere;

/// <summary>What an <see cref="Icon"/> draws.</summary>
public enum IconKind
{
    /// <summary>A text glyph from a font, always drawn in the tint color.</summary>
    Glyph,

    /// <summary>A raster image, scaled to fit.</summary>
    Image,

    /// <summary>A recorded vector picture (for example a decoded SVG), scaled to fit its cull rectangle.</summary>
    Picture,
}

/// <summary>
/// A drawable icon: a font glyph, a raster image or a vector picture, drawn by <see cref="Gui.Icon"/> centred in a
/// square. Glyphs always take the tint; images and pictures only when <see cref="Tintable"/>.
/// </summary>
public sealed record Icon
{
    Icon(IconKind kind) => Kind = kind;

    /// <summary>What the icon draws.</summary>
    public IconKind Kind { get; }

    /// <summary>The glyph text (one code point, or a ligature) for <see cref="IconKind.Glyph"/>.</summary>
    public string? Glyph { get; init; }

    /// <summary>
    /// The glyph's font; <c>null</c> picks the first of the scope's text, widget-icon and icon fonts that has it.
    /// </summary>
    public Font? Font { get; init; }

    /// <summary>The raster image for <see cref="IconKind.Image"/>.</summary>
    public SKImage? Image { get; init; }

    /// <summary>The vector picture for <see cref="IconKind.Picture"/>.</summary>
    public SKPicture? Picture { get; init; }

    /// <summary>Whether a tint recolors the image or picture silhouette; glyphs are always tinted.</summary>
    public bool Tintable { get; init; }

    /// <summary>The color used when the call site passes no tint; <c>null</c> falls back to the scope text color.</summary>
    public Color? Color { get; init; }

    /// <summary>A glyph icon.</summary>
    /// <param name="glyph">The glyph text, such as <c>""</c>.</param>
    /// <param name="font">The glyph's font, or <c>null</c> for the scope fallback chain.</param>
    /// <param name="color">Default color, or <c>null</c> for the scope text color.</param>
    public static Icon FromGlyph(string glyph, Font? font = null, Color? color = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(glyph);
        return new Icon(IconKind.Glyph) { Glyph = glyph, Font = font, Color = color, Tintable = true };
    }

    /// <summary>A raster image icon.</summary>
    /// <param name="image">The image.</param>
    /// <param name="tintable">Whether a tint recolors the image silhouette.</param>
    public static Icon FromImage(SKImage image, bool tintable = false)
    {
        ArgumentNullException.ThrowIfNull(image);
        return new Icon(IconKind.Image) { Image = image, Tintable = tintable };
    }

    /// <summary>A vector picture icon, scaled from its cull rectangle.</summary>
    /// <param name="picture">The recorded picture.</param>
    /// <param name="tintable">Whether a tint recolors the picture silhouette.</param>
    public static Icon FromPicture(SKPicture picture, bool tintable = false)
    {
        ArgumentNullException.ThrowIfNull(picture);
        return new Icon(IconKind.Picture) { Picture = picture, Tintable = tintable };
    }

    /// <summary>
    /// Decodes an icon file: the first <paramref name="decoders"/> entry that accepts the extension wins, otherwise
    /// it is decoded as a raster image (PNG, JPEG, WebP, …). Returns <c>null</c> when nothing can decode it.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="decoders">Extra decoders, such as the SVG decoder from <c>MASS4.Guinevere.Svg</c>.</param>
    public static Icon? FromFile(string path, IEnumerable<IIconDecoder>? decoders = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var stream = File.OpenRead(path);
        return FromStream(stream, Path.GetExtension(path), decoders);
    }

    /// <inheritdoc cref="FromFile"/>
    /// <param name="stream">The encoded icon.</param>
    /// <param name="extension">The file extension, such as <c>.svg</c>, used to pick a decoder.</param>
    /// <param name="decoders">Extra decoders, such as the SVG decoder from <c>MASS4.Guinevere.Svg</c>.</param>
    public static Icon? FromStream(Stream stream, string extension, IEnumerable<IIconDecoder>? decoders = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        foreach (var decoder in decoders ?? [])
            if (decoder.CanDecode(extension)) return decoder.Decode(stream);

        using var data = SKData.Create(stream);
        var image = data is null ? null : SKImage.FromEncodedData(data);
        return image is null ? null : FromImage(image);
    }
}

/// <summary>Decodes an icon file format that Core does not handle itself, such as SVG.</summary>
public interface IIconDecoder
{
    /// <summary>Whether this decoder handles files with the given extension.</summary>
    /// <param name="extension">The extension including the dot, such as <c>.svg</c>.</param>
    bool CanDecode(string extension);

    /// <summary>Decodes the stream, or returns <c>null</c> when it is not a valid file.</summary>
    /// <param name="stream">The encoded icon.</param>
    Icon? Decode(Stream stream);
}
