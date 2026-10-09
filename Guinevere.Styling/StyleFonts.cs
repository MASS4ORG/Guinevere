namespace Guinevere;

/// <summary>Parses font descriptors and local font source locations.</summary>
static class StyleFonts
{
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
