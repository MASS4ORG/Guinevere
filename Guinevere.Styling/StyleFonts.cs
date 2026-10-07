using SkiaSharp;

namespace Guinevere;

/// <summary>
/// Resolves <c>font-family</c> lists with a weight and slant against the sheets' <c>@font-face</c> files, then system
/// families. A face without the requested weight or slant is emboldened or skewed. Cached until the sheets change.
/// </summary>
sealed class StyleFonts
{
    /// <summary>The weight from which bold is synthesized when the face is lighter.</summary>
    const int BoldWeight = 600;

    readonly Dictionary<(string? Families, SKTypeface? Base, int Weight, bool Italic), Font?> _fonts = [];
    readonly Dictionary<string, SKTypeface?> _faces = new(StringComparer.OrdinalIgnoreCase);
    int _version = -1;

    /// <summary>
    /// The first family in <paramref name="families"/> that can be found, or <paramref name="baseFont"/>'s typeface
    /// when no family is given; <c>null</c> when nothing matches.
    /// </summary>
    public Font? Resolve(StyleSheetCollection sheets, string? families, Font? baseFont = null, int weight = 400,
        bool italic = false)
    {
        if (_version != sheets.Version)
        {
            _version = sheets.Version;
            _fonts.Clear();
            _faces.Clear();
        }

        var noFamily = string.IsNullOrWhiteSpace(families);
        var key = (noFamily ? null : families, noFamily ? baseFont?.SkFont.Typeface : null, weight, italic);
        if (!_fonts.TryGetValue(key, out var font)) _fonts[key] = font = Create(sheets, key.Item1, key.Item2, weight, italic);
        return font;
    }

    Font? Create(StyleSheetCollection sheets, string? families, SKTypeface? baseFace, int weight, bool italic)
    {
        var style = new SKFontStyle(weight, (int)SKFontStyleWidth.Normal,
            italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        var face = families is null ? Restyled(baseFace, style) : FirstFamily(sheets, families, style);
        return face is null ? null : Synthesized(face, weight, italic);
    }

    /// <summary>The system face of the inherited family in the requested style, else the inherited face itself.</summary>
    static SKTypeface? Restyled(SKTypeface? baseFace, SKFontStyle style) =>
        baseFace is null ? null : System(baseFace.FamilyName, style) ?? baseFace;

    static Font Synthesized(SKTypeface face, int weight, bool italic) => new(new SKFont(face)
    {
        Embolden = weight >= BoldWeight && face.FontWeight < BoldWeight,
        SkewX = italic && face.FontSlant == SKFontStyleSlant.Upright ? -0.25f : 0f,
    });

    SKTypeface? FirstFamily(StyleSheetCollection sheets, string families, SKFontStyle style)
    {
        foreach (var entry in families.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var family = StyleValue.Unquote(entry);
            if (!_faces.TryGetValue(family, out var face)) _faces[family] = face = FontFace(sheets, family);
            if ((face ?? System(family, style)) is { } found) return found;
        }
        return null;
    }

    /// <summary>The <c>@font-face</c> file for a family, later sheets first.</summary>
    static SKTypeface? FontFace(StyleSheetCollection sheets, string family)
    {
        for (var i = sheets.Count - 1; i >= 0; i--)
            foreach (var face in sheets[i].FontFaces)
                if (string.Equals(face.Family, family, StringComparison.OrdinalIgnoreCase)
                    && LocalPath(face.Source) is { } path && SKTypeface.FromFile(path) is { } typeface)
                    return typeface;
        return null;
    }

    /// <summary>A system face of exactly this family; font managers return a fallback family for unknown names.</summary>
    static SKTypeface? System(string family, SKFontStyle style)
    {
        var face = SKFontManager.Default.MatchFamily(family, style);
        return string.Equals(face?.FamilyName, family, StringComparison.OrdinalIgnoreCase) ? face : null;
    }

    /// <summary>A local file path for a file or relative URI; <c>null</c> for remote locations.</summary>
    public static string? LocalPath(Uri uri) => uri.IsAbsoluteUri
        ? uri.IsFile ? uri.LocalPath : null
        : Uri.UnescapeDataString(uri.OriginalString);

    /// <summary>A CSS <c>font-weight</c>: 1–1000, <c>normal</c> (400) or <c>bold</c> (700).</summary>
    public static int Weight(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" or "normal" => 400,
        "bold" => 700,
        var text => StyleValue.TryFloat(text, out var number) ? Math.Clamp((int)number, 1, 1000) : 400,
    };

    /// <summary>Whether a CSS <c>font-style</c> asks for a slanted face (<c>italic</c> or <c>oblique</c>).</summary>
    public static bool Italic(string? value) => value?.Trim().ToLowerInvariant() is "italic" or "oblique";
}
