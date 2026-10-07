using System.Xml;
using SkiaSharp;
using Svg.Skia;

namespace Guinevere;

/// <summary>
/// Loads SVG files as <see cref="Icon"/> pictures through Svg.Skia. Parse once and reuse the icon: it scales from
/// the SVG's view box to any size without re-parsing.
/// </summary>
public static class SvgIcon
{
    /// <summary>Loads an SVG file, or returns <c>null</c> when it is not a valid SVG.</summary>
    /// <param name="path">The <c>.svg</c> file path.</param>
    /// <param name="tintable">Whether a tint recolors the icon silhouette, as for single-color icon sets.</param>
    public static Icon? FromFile(string path, bool tintable = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var stream = File.OpenRead(path);
        return FromStream(stream, tintable);
    }

    /// <summary>Loads an SVG document from a stream, or returns <c>null</c> when it is not a valid SVG.</summary>
    /// <param name="stream">The SVG document.</param>
    /// <param name="tintable">Whether a tint recolors the icon silhouette, as for single-color icon sets.</param>
    public static Icon? FromStream(Stream stream, bool tintable = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Decode(svg => svg.Load(stream), tintable);
    }

    /// <summary>Parses SVG markup, or returns <c>null</c> when it is not a valid SVG.</summary>
    /// <param name="markup">The SVG document text.</param>
    /// <param name="tintable">Whether a tint recolors the icon silhouette, as for single-color icon sets.</param>
    public static Icon? FromMarkup(string markup, bool tintable = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(markup);
        return Decode(svg => svg.FromSvg(markup), tintable);
    }

    static Icon? Decode(Func<SKSvg, SKPicture?> load, bool tintable)
    {
        try
        {
            return load(new SKSvg()) is { } picture ? Icon.FromPicture(picture, tintable) : null;
        }
        catch (XmlException)
        {
            return null;
        }
    }
}

/// <summary>Decodes <c>.svg</c> files for <see cref="Icon.FromFile"/> and stylesheet icon <c>src</c> values.</summary>
public sealed class SvgIconDecoder : IIconDecoder
{
    /// <summary>A shared decoder; it holds no state.</summary>
    public static readonly SvgIconDecoder Instance = new();

    /// <inheritdoc/>
    public bool CanDecode(string extension) =>
        string.Equals(extension, ".svg", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public Icon? Decode(Stream stream) => SvgIcon.FromStream(stream);
}
