using System.Runtime.CompilerServices;

namespace Guinevere;

/// <summary>Immutable identity of a font and its ordered fallbacks at a display-scaled size.</summary>
readonly struct FontKey : IEquatable<FontKey>
{
    static readonly ConditionalWeakTable<Font, Snapshot> Snapshots = new();
    readonly Snapshot _snapshot;
    readonly float _size;

    internal long Weight => _snapshot.Weight;

    internal FontKey(Font font, float size)
    {
        var snapshot = Snapshots.GetValue(font, static source => new Snapshot(source));
        if (!snapshot.Matches(font))
        {
            Snapshots.Remove(font);
            snapshot = Snapshots.GetValue(font, static source => new Snapshot(source));
        }

        _snapshot = snapshot;
        _size = size;
    }

    /// <summary>Creates an independent wrapper, optionally borrowing a cached native font.</summary>
    internal Font Resolve(float? size = null, FontCache? cache = null) => _snapshot.Resolve(size ?? _size, cache);

    /// <inheritdoc />
    public bool Equals(FontKey other) => _size == other._size && (ReferenceEquals(_snapshot, other._snapshot)
        || _snapshot.SameValue(other._snapshot));

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is FontKey other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_snapshot.Hash, _size);

    /// <summary>Shared immutable font state; weak source keys avoid retaining discarded caller fonts.</summary>
    sealed class Snapshot
    {
        readonly Face _face;
        readonly Face[] _fallbacks;

        internal long Weight => 16L + _fallbacks.Length * 16L;
        internal int Hash { get; }

        internal Snapshot(Font font)
        {
            _face = new Face(font);
            _fallbacks = font.Fallbacks.Count == 0 ? [] : new Face[font.Fallbacks.Count];
            for (var i = 0; i < _fallbacks.Length; i++)
            {
                _fallbacks[i] = new Face(font.Fallbacks[i]);
            }

            Hash = HashCode.Combine(_face.Typeface, _fallbacks.Length);
        }

        internal bool Matches(Font font)
        {
            if (_face != new Face(font) || _fallbacks.Length != font.Fallbacks.Count)
            {
                return false;
            }

            for (var i = 0; i < _fallbacks.Length; i++)
            {
                if (_fallbacks[i] != new Face(font.Fallbacks[i]))
                {
                    return false;
                }
            }

            return true;
        }

        internal bool SameValue(Snapshot other) => _face == other._face
            && _fallbacks.AsSpan().SequenceEqual(other._fallbacks);

        internal Font Resolve(float size, FontCache? cache)
        {
            var font = _face.Resolve(size, cache);
            if (_fallbacks.Length > 0)
            {
                var fallbacks = new Font[_fallbacks.Length];
                for (var i = 0; i < fallbacks.Length; i++)
                {
                    fallbacks[i] = _fallbacks[i].Resolve(size, cache);
                }

                font.Fallbacks = fallbacks;
            }

            return font;
        }
    }

    internal readonly record struct Face(SKTypeface Typeface, bool Embolden, float SkewX, float ScaleX, string? Families,
        int? Weight, bool? Italic, SKFontEdging Edging, SKFontHinting Hinting, bool Subpixel, bool LinearMetrics,
        bool BaselineSnap, bool EmbeddedBitmaps, bool ForceAutoHinting)
    {
        internal Face(Font font) : this(font.SkFont.Typeface, font.SkFont.Embolden, font.SkFont.SkewX,
            font.SkFont.ScaleX, font.Families, font.RequestedWeight, font.RequestedItalic, font.SkFont.Edging,
            font.SkFont.Hinting, font.SkFont.Subpixel, font.SkFont.LinearMetrics, font.SkFont.BaselineSnap,
            font.SkFont.EmbeddedBitmaps, font.SkFont.ForceAutoHinting)
        { }

        internal SKFont Create(float size) => new(Typeface, size, ScaleX, SkewX)
        {
            Embolden = Embolden,
            Edging = Edging,
            Hinting = Hinting,
            Subpixel = Subpixel,
            LinearMetrics = LinearMetrics,
            BaselineSnap = BaselineSnap,
            EmbeddedBitmaps = EmbeddedBitmaps,
            ForceAutoHinting = ForceAutoHinting,
        };

        internal Font Resolve(float size, FontCache? cache) => new(cache?.Get(this, size) ?? Create(size),
            cache is not null)
        {
            Families = Families,
            RequestedWeight = Weight,
            RequestedItalic = Italic,
        };
    }
}
