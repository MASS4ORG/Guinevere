using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace Guinevere;

/// <summary>Owns cached stylesheet bitmap backgrounds and their load or syntax diagnostics per GUI.</summary>
public static class StyledBackgrounds
{
    static readonly ConditionalWeakTable<Gui, BackgroundCache> States = [];
    static readonly Regex BitmapUrl = new("""^url\(\s*(?:"[^"]+"|'[^']+'|[^()\s,'"]+)\s*\)$""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    extension(Gui gui)
    {
        /// <summary>Located bitmap background failures for the current stylesheet version.</summary>
        public IReadOnlyList<StyleDiagnostic> BackgroundDiagnostics
        {
            get
            {
                var cache = States.GetOrCreateValue(gui);
                cache.Synchronize(gui.StyleSheets.Version);
                return cache.Diagnostics;
            }
        }

        /// <summary>Disposes decoded backgrounds; call between frames after asset changes or at GUI shutdown.</summary>
        public void ClearBackgroundImages() => States.GetOrCreateValue(gui).Clear();
    }

    internal static StyleBitmapBackground? Resolve(Gui gui, ResolvedStyle style)
    {
        var cache = States.GetOrCreateValue(gui);
        cache.Synchronize(gui.StyleSheets.Version);
        return cache.Get(style);
    }

    sealed class BackgroundCache
    {
        readonly Dictionary<(Uri, Func<Uri, Stream?>?), SKImage?> _images = [];
        readonly ConditionalWeakTable<ResolvedStyle, StrongBox<StyleBitmapBackground?>> _styles = [];
        readonly HashSet<StyleDiagnostic> _reported = [];
        internal readonly List<StyleDiagnostic> Diagnostics = [];
        int _version = -1;

        internal void Synchronize(int version)
        {
            if (_version == version)
            {
                return;
            }

            Clear();
            _version = version;
        }

        internal void Clear()
        {
            foreach (var image in _images.Values)
            {
                image?.Dispose();
            }

            _images.Clear();
            _styles.Clear();
            _reported.Clear();
            Diagnostics.Clear();
        }

        internal StyleBitmapBackground? Get(ResolvedStyle style) =>
            _styles.GetValue(style, s => new StrongBox<StyleBitmapBackground?>(Build(s))).Value;

        StyleBitmapBackground? Build(ResolvedStyle style)
        {
            var property = style.Has("background-image") ? "background-image" : "background";
            var raw = style.Get(property);
            if (!IsBitmap(raw))
            {
                return null;
            }

            try
            {
                if (!BitmapUrl.IsMatch(raw!.Trim()))
                {
                    throw new FormatException("Expected a single url() bitmap background.");
                }

                var uri = style.GetUrl(property) ?? throw new FormatException("Expected a single url() bitmap background.");
                var repeat = StyleBitmapBackground.Repeat(style.Get("background-repeat"));
                var size = StyleBitmapBackground.Size(style.Get("background-size"));
                var position = StyleBitmapBackground.Position(style.Get("background-position"));
                var slice = StyleBitmapBackground.Slice(style.Get("background-slice"));
                var image = Image(style, property, uri);
                return new StyleBitmapBackground(image, repeat, size, position, slice);
            }
            catch (BackgroundSyntaxException exception)
            {
                Report(style, exception.Property, exception.Message);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
            {
                Report(style, property, exception.Message);
            }
            return null;
        }

        SKImage Image(ResolvedStyle style, string property, Uri uri)
        {
            var resolver = style.Sources?.GetValueOrDefault(property)?.Options.ResourceResolver;
            var key = (uri, resolver);
            if (!_images.TryGetValue(key, out var image))
            {
                _images[key] = image = Load(uri, resolver);
            }

            return image ?? throw new IOException($"Cannot load bitmap background '{uri}'.");
        }

        static bool IsBitmap(string? raw) => raw is not null
            && !string.Equals(raw.Trim(), "none", StringComparison.OrdinalIgnoreCase)
            && !raw.StartsWith("linear-gradient(", StringComparison.OrdinalIgnoreCase)
            && !StyleValue.TryColor(raw, out _);

        static SKImage? Load(Uri uri, Func<Uri, Stream?>? resolver)
        {
            try
            {
                using var stream = resolver is not null ? resolver(uri)
                    : StyleFonts.LocalPath(uri) is { } path ? File.OpenRead(path) : null;
                return stream is null ? null : SKImage.FromEncodedData(stream);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        void Report(ResolvedStyle style, string property, string message)
        {
            var source = style.Sources?.GetValueOrDefault(property);
            var name = source?.Options.SourceName;
            var line = source?.Line ?? 0;
            var column = source?.Column ?? 0;
            var diagnostic = new StyleDiagnostic($"{name ?? "<inline>"}:{line}:{column}: {message}", name,
                Line: line, Column: column);
            if (_reported.Add(diagnostic))
            {
                Diagnostics.Add(diagnostic);
            }
        }
    }
}

sealed class BackgroundSyntaxException(string property, string message) : FormatException(message)
{
    internal string Property { get; } = property;
}
