using System.Runtime.CompilerServices;
using SkiaSharp;

namespace Guinevere;

/// <summary>
/// Icons defined by <c>.pss</c> rules on the <c>icon</c> type, keyed by id: <c>glyph</c> with an optional
/// <c>font-family</c>, or <c>src = url(…)</c> with an optional <c>tint</c>, plus a default <c>color</c>. Swapping
/// sheets swaps the icon set; decoded files and fonts are cached until the sheets change.
/// </summary>
public static class StyledIcons
{
    static readonly ConditionalWeakTable<Gui, IconCache> States = new();

    extension(Gui gui)
    {
        /// <summary>
        /// Decoders for <c>src</c> formats Core does not read itself, such as the SVG decoder from
        /// <c>MASS4.Guinevere.Svg</c>. Raster formats (PNG, JPEG, WebP) need none.
        /// </summary>
        public IList<IIconDecoder> IconDecoders => States.GetOrCreateValue(gui).Decoders;

        /// <summary>
        /// Resolves the icon with the given id from the GUI's stylesheets, or <c>null</c> when no rule defines a
        /// loadable <c>src</c> or a <c>glyph</c>. When both are set, <c>src</c> wins; <c>src = none;</c> clears it.
        /// </summary>
        /// <param name="id">The icon id, matched by <c>icon#id</c> (escape dots as <c>icon#scene\.move</c>).</param>
        /// <param name="classes">Lookup classes, such as <c>file</c> and <c>ext-png</c>.</param>
        /// <param name="modifiers">Active semantic modifiers, such as <c>open</c>.</param>
        /// <param name="state">Interaction state, such as <see cref="StyleState.Hover"/>.</param>
        public Icon? ResolveIcon(string id, IReadOnlyList<string>? classes = null,
            IReadOnlyList<string>? modifiers = null, StyleState state = StyleState.None)
        {
            ArgumentException.ThrowIfNullOrEmpty(id);
            var sheets = gui.StyleSheets;
            var style = gui.ResolveStyle("icon", classes, id, state, modifiers);
            return States.GetOrCreateValue(gui).Get(sheets, style);
        }

        /// <summary>
        /// Lays out the stylesheet icon with the given id as a square node (see <see cref="Gui.Icon"/>). A missing
        /// icon still reserves its square, so swapping icon sets never moves the layout.
        /// </summary>
        /// <param name="id">The icon id, matched by <c>icon#id</c>.</param>
        /// <param name="size">Side of the square, or <c>0</c> for the scope text size.</param>
        /// <param name="tint">Overrides the rule's <c>color</c>; images and pictures take it only with <c>tint = true</c>.</param>
        /// <param name="classes">Lookup classes, such as <c>file</c> and <c>ext-png</c>.</param>
        /// <param name="modifiers">Active semantic modifiers, such as <c>open</c>.</param>
        /// <param name="state">Interaction state, such as <see cref="StyleState.Hover"/>.</param>
        /// <returns>The layout node the icon occupies.</returns>
        public LayoutNode StyledIcon(string id, float size = 0, Color? tint = null,
            IReadOnlyList<string>? classes = null, IReadOnlyList<string>? modifiers = null,
            StyleState state = StyleState.None) =>
            gui.Icon(gui.ResolveIcon(id, classes, modifiers, state), size, tint);
    }

    /// <summary>Per-GUI decoders plus icons, files and fonts cached for one stylesheet version.</summary>
    sealed class IconCache
    {
        public readonly List<IIconDecoder> Decoders = [];
        readonly ConditionalWeakTable<ResolvedStyle, StrongBox<Icon?>> _icons = new();
        readonly Dictionary<Uri, Icon?> _files = [];
        readonly Dictionary<string, Font?> _fonts = new(StringComparer.OrdinalIgnoreCase);
        int _version = -1;

        public Icon? Get(StyleSheetCollection sheets, ResolvedStyle style)
        {
            if (_version != sheets.Version)
            {
                _version = sheets.Version;
                _icons.Clear();
                _files.Clear();
                _fonts.Clear();
            }
            return _icons.GetValue(style, s => new StrongBox<Icon?>(Build(sheets, s))).Value;
        }

        Icon? Build(StyleSheetCollection sheets, ResolvedStyle style)
        {
            var icon = Source(style) ?? Glyph(sheets, style);
            if (icon is null) return null;
            var tint = icon.Kind == IconKind.Glyph
                       || (StyleValue.TryBool(style.Get("tint"), out var tintable) ? tintable : icon.Tintable);
            return icon with { Color = style.GetColor("color") ?? icon.Color, Tintable = tint };
        }

        Icon? Source(ResolvedStyle style)
        {
            if (IsNone(style.Get("src")) || style.GetUrl("src") is not { } uri) return null;
            if (!_files.TryGetValue(uri, out var icon)) _files[uri] = icon = Load(uri);
            return icon;
        }

        Icon? Load(Uri uri)
        {
            if (LocalPath(uri) is not { } path) return null;
            try
            {
                return Icon.FromFile(path, Decoders);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        Icon? Glyph(StyleSheetCollection sheets, ResolvedStyle style)
        {
            var raw = style.Get("glyph");
            if (raw is null || IsNone(raw)) return null;
            var glyph = StyleValue.Unescape(StyleValue.Unquote(raw));
            return glyph.Length == 0 ? null : Icon.FromGlyph(glyph, FontFor(sheets, style.Get("font-family")));
        }

        /// <summary>
        /// The first family in the list that a sheet's <c>@font-face</c> (later sheets first) or the system provides;
        /// <c>null</c> leaves the glyph to the scope's font fallback.
        /// </summary>
        Font? FontFor(StyleSheetCollection sheets, string? families)
        {
            if (string.IsNullOrWhiteSpace(families)) return null;
            foreach (var entry in families.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var family = StyleValue.Unquote(entry);
                if (!_fonts.TryGetValue(family, out var font)) _fonts[family] = font = LoadFont(sheets, family);
                if (font is not null) return font;
            }
            return null;
        }

        static Font? LoadFont(StyleSheetCollection sheets, string family)
        {
            for (var i = sheets.Count - 1; i >= 0; i--)
                foreach (var face in sheets[i].FontFaces)
                    if (string.Equals(face.Family, family, StringComparison.OrdinalIgnoreCase)
                        && LocalPath(face.Source) is { } path && SKTypeface.FromFile(path) is { } typeface)
                        return new Font(new SKFont(typeface));

            var system = SKFontManager.Default.MatchFamily(family);
            return string.Equals(system?.FamilyName, family, StringComparison.OrdinalIgnoreCase)
                ? new Font(new SKFont(system))
                : null;
        }

        static string? LocalPath(Uri uri) => uri.IsAbsoluteUri
            ? uri.IsFile ? uri.LocalPath : null
            : Uri.UnescapeDataString(uri.OriginalString);

        static bool IsNone(string? value) => string.Equals(value?.Trim(), "none", StringComparison.OrdinalIgnoreCase);
    }
}
