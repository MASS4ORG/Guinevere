namespace Guinevere;

/// <summary>Synchronizes stylesheet faces with the GUI's registry and resolves styled font lists.</summary>
sealed class StyleFontRegistry(FontRegistry registry)
{
    readonly Dictionary<(Font Base, int Weight, bool Italic), Font> _restyled = [];
    int _version = -1;
    int _registryVersion = -1;
    StyleSheet[] _sheets = [];

    /// <summary>Loads the active sheets' faces into the shared registry, later sheets taking precedence.</summary>
    public void Synchronize(StyleSheetCollection sheets)
    {
        if (_version == sheets.Version) return;
        _version = sheets.Version;
        if (_sheets.SequenceEqual(sheets)) return;
        _sheets = [.. sheets];
        registry.RemoveSource(this);
        foreach (var sheet in sheets)
            foreach (var face in sheet.FontFaces)
                Load(face);
    }

    void Load(StyleFontFace face)
    {
        if (StyleFonts.LocalPath(face.Source) is not { } path) return;
        registry.RegisterFile(face.Family, path, face.Weight, face.Italic, this);
    }

    /// <summary>Resolves a family list or restyles the inherited list without losing its fallback faces.</summary>
    public Font? Resolve(StyleSheetCollection sheets, string? families, Font? baseFont = null, int weight = 400,
        bool italic = false)
    {
        Synchronize(sheets);
        if (!string.IsNullOrWhiteSpace(families)) return registry.Resolve(families, weight: weight, italic: italic);
        if (baseFont is null) return null;
        if (baseFont.Families is { } inherited)
            return registry.Resolve(inherited, weight: weight, italic: italic);
        if (_registryVersion != registry.Version)
        {
            _registryVersion = registry.Version;
            _restyled.Clear();
        }
        var key = (baseFont, weight, italic);
        if (_restyled.TryGetValue(key, out var cached)) return cached;
        var font = registry.Resolve(baseFont.FamilyName, weight: weight, italic: italic)
            ?? registry.Own(baseFont.Restyled(weight, italic));
        _restyled[key] = font;
        return font;
    }

    internal int Version => registry.Version;
}
