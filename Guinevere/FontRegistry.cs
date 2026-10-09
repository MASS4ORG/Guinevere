namespace Guinevere;

/// <summary>Font roles applications can assign independently of individual controls.</summary>
public enum FontRole
{
    /// <summary>Ordinary interface text.</summary>
    Ui,
    /// <summary>Monospaced interface text.</summary>
    UiMono,
    /// <summary>Source code and logs.</summary>
    Code,
    /// <summary>Widget icon glyphs.</summary>
    Icon,
    /// <summary>Emoji and symbols.</summary>
    Emoji,
}

/// <summary>Registers font faces and caches family, weight, slant and ordered fallback lookup.</summary>
public sealed class FontRegistry : IDisposable
{
    readonly List<Face> _faces = [];
    readonly Dictionary<(string Families, float Size, int Weight, bool Italic), Font?> _resolved = [];
    readonly Dictionary<FontRole, Font> _roleFonts = [];
    readonly Dictionary<FontRole, string> _roleFamilies = [];
    readonly List<WeakReference<Font>> _ownedFonts = [];
    readonly List<WeakReference<SKTypeface>> _ownedFaces = [];
    bool _disposed;
    static readonly HashSet<string> GenericFamilies = new(StringComparer.OrdinalIgnoreCase)
        { "sans-serif", "serif", "monospace", "cursive", "fantasy", "system-ui", "emoji" };

    sealed record Face(string Family, Font Font, int Weight, bool Italic, object? Owner);

    /// <summary>Changes whenever registrations or roles change, allowing consumers to invalidate their caches.</summary>
    public int Version { get; private set; }

    /// <summary>Registers a caller-owned font under a family alias; equal descriptors replace the previous face.</summary>
    public void Register(string family, Font font, int? weight = null, bool? italic = null)
    {
        ArgumentNullException.ThrowIfNull(font);
        Register(family, font, weight ?? font.Weight, italic ?? font.Italic, null);
    }

    /// <summary>Loads and registers a local font file; invalid or unreadable files throw.</summary>
    public void RegisterFile(string family, string path, int? weight = null, bool? italic = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        RegisterStream(family, stream, weight, italic);
    }

    /// <summary>Loads a font from the stream's current position without closing the caller's stream.</summary>
    public void RegisterStream(string family, Stream stream, int? weight = null, bool? italic = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(family);
        ArgumentNullException.ThrowIfNull(stream);
        using var data = SKData.Create(stream);
        var face = SKTypeface.FromData(data) ?? throw new InvalidDataException("The stream is not a font.");
        _ownedFaces.Add(new WeakReference<SKTypeface>(face));
        var font = Own(new Font(new SKFont(face)));
        Register(family, font, weight, italic);
    }

    /// <summary>Assigns a caller-owned font to a role and registers its family for subsequent lookup.</summary>
    public void SetRole(FontRole role, Font font)
    {
        ArgumentNullException.ThrowIfNull(font);
        Register(font.FamilyName, font);
        _roleFamilies.Remove(role);
        if (_roleFonts.GetValueOrDefault(role) == font) return;
        _roleFonts[role] = font;
        Invalidate();
    }

    /// <summary>Assigns an ordered family list to a role; it resolves again when registrations change.</summary>
    public void SetRole(FontRole role, string families)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(families);
        _roleFonts.Remove(role);
        if (_roleFamilies.GetValueOrDefault(role) == families) return;
        _roleFamilies[role] = families;
        Invalidate();
    }

    /// <summary>Removes a role assignment, allowing the GUI's inherited font defaults to apply.</summary>
    public void ClearRole(FontRole role)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var removed = _roleFonts.Remove(role);
        removed |= _roleFamilies.Remove(role);
        if (removed) Invalidate();
    }

    /// <summary>Resolves a role, or returns null when it has no available face.</summary>
    public Font? ResolveRole(FontRole role, float? size = null, int? weight = null, bool? italic = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_roleFamilies.TryGetValue(role, out var families))
            return Resolve(families, size ?? 12f, weight ?? 400, italic ?? false);
        var font = _roleFonts.GetValueOrDefault(role);
        return font is null ? null : ResolveAssignedRole(font, size, weight, italic);
    }

    Font ResolveAssignedRole(Font font, float? size, int? weight, bool? italic)
    {
        if (size is null && weight is null && italic is null) return font;
        return Resolve(font.Families ?? $"\"{font.FamilyName}\"", size ?? font.Size,
            weight ?? font.Weight, italic ?? font.Italic)!;
    }

    /// <summary>Resolves a comma-separated family list, retaining every available family for glyph fallback.</summary>
    public Font? Resolve(string families, float size = 12f, int weight = 400, bool italic = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(families);
        ArgumentOutOfRangeException.ThrowIfLessThan(weight, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(weight, 1000);
        if (!float.IsFinite(size) || size <= 0f) throw new ArgumentOutOfRangeException(nameof(size));
        var key = (families, size, weight, italic);
        if (_resolved.TryGetValue(key, out var cached)) return cached;
        var fonts = new List<Font>();
        foreach (var family in ParseFamilies(families))
            if (Find(family, weight, italic) is { } face)
                fonts.Add(Own(Styled(face, size, weight, italic)));
        var font = fonts.Count == 0 ? null : fonts[0];
        if (font is not null)
        {
            font.Families = families;
            font.RequestedWeight = weight;
            font.RequestedItalic = italic;
            font.Fallbacks = fonts.Skip(1).ToArray();
        }
        _resolved[key] = font;
        return font;
    }

    internal void Register(string family, Font font, int weight, bool italic, object? owner)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(family);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentOutOfRangeException.ThrowIfLessThan(weight, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(weight, 1000);
        var index = _faces.FindIndex(f => f.Family.Equals(family, StringComparison.OrdinalIgnoreCase)
            && f.Weight == weight && f.Italic == italic && ReferenceEquals(f.Owner, owner));
        var entry = new Face(family, font, weight, italic, owner);
        if (index >= 0 && _faces[index] == entry) return;
        if (index >= 0) _faces.RemoveAt(index);
        _faces.Add(entry);
        Invalidate();
    }

    internal void RemoveSource(object owner)
    {
        if (_faces.RemoveAll(face => ReferenceEquals(face.Owner, owner)) > 0) Invalidate();
    }

    SKTypeface? Find(string family, int weight, bool italic)
    {
        return FindRegistered(family, weight, italic)?.Font.SkFont.Typeface ?? FindSystem(family, weight, italic);
    }

    Face? FindRegistered(string family, int weight, bool italic)
    {
        Face? best = null;
        var bestRank = int.MaxValue;
        for (var i = _faces.Count - 1; i >= 0; i--)
        {
            var candidate = _faces[i];
            if (!candidate.Family.Equals(family, StringComparison.OrdinalIgnoreCase)) continue;
            var rank = (candidate.Italic == italic ? 0 : 10000) + WeightRank(candidate.Weight, weight);
            if (rank >= bestRank) continue;
            best = candidate;
            bestRank = rank;
        }
        return best;
    }

    SKTypeface? FindSystem(string family, int weight, bool italic)
    {
        using var style = new SKFontStyle(weight, (int)SKFontStyleWidth.Normal,
            italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        var face = SKFontManager.Default.MatchFamily(family, style);
        if (face is null) return null;
        if (GenericFamilies.Contains(family)
            || string.Equals(face.FamilyName, family, StringComparison.OrdinalIgnoreCase))
        {
            _ownedFaces.Add(new WeakReference<SKTypeface>(face));
            return face;
        }
        face.Dispose();
        return null;
    }

    static int WeightRank(int available, int requested)
    {
        if (available == requested) return 0;
        if (requested is >= 400 and <= 500)
            return RegularWeightRank(available, requested);
        var preferred = requested < 400 ? available < requested : available > requested;
        return Math.Abs(available - requested) + (preferred ? 0 : 1000);
    }

    static int RegularWeightRank(int available, int requested)
    {
        if (available >= requested && available <= 500) return available - requested;
        return available < requested ? 500 - available : available;
    }

    static Font Styled(SKTypeface face, float size, int weight, bool italic) => new(new SKFont(face, size)
    {
        Embolden = weight >= 600 && face.FontWeight < 600,
        SkewX = italic && face.FontSlant == SKFontStyleSlant.Upright ? -0.25f : 0f,
    });

    static IEnumerable<string> ParseFamilies(string families)
    {
        var start = 0;
        var quote = '\0';
        for (var i = 0; i <= families.Length; i++)
        {
            if (i < families.Length) quote = FamilyQuote(families[i], quote);
            if (i < families.Length && (families[i] != ',' || quote != '\0')) continue;
            var family = families[start..i].Trim().Trim('\'', '"');
            if (family.Length > 0) yield return family;
            start = i + 1;
        }
    }

    static char FamilyQuote(char character, char quote)
    {
        if (character is not ('\'' or '"')) return quote;
        return quote == '\0' ? character : quote == character ? '\0' : quote;
    }

    void Invalidate()
    {
        Version++;
        _resolved.Clear();
        _ownedFonts.RemoveAll(reference => !reference.TryGetTarget(out _));
        _ownedFaces.RemoveAll(reference => !reference.TryGetTarget(out _));
    }

    internal Font Own(Font font)
    {
        _ownedFonts.Add(new WeakReference<Font>(font));
        return font;
    }

    internal void RegisterFile(string family, string path, int weight, bool italic, object owner)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var typeface = SKTypeface.FromFile(path);
        if (typeface is null) return;
        _ownedFaces.Add(new WeakReference<SKTypeface>(typeface));
        Register(family, Own(new Font(new SKFont(typeface))), weight, italic, owner);
    }

    /// <summary>Releases loaded and resolved fonts; caller-supplied fonts remain owned by their callers.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var reference in _ownedFonts)
            if (reference.TryGetTarget(out var font)) font.Dispose();
        foreach (var reference in _ownedFaces)
            if (reference.TryGetTarget(out var face)) face.Dispose();
        _ownedFonts.Clear();
        _ownedFaces.Clear();
        _faces.Clear();
        _roleFonts.Clear();
        _roleFamilies.Clear();
        Invalidate();
    }
}
