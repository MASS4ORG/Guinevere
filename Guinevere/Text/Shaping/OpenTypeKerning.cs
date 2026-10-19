using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Guinevere;

/// <summary>
/// Pair kerning read from a font: GPOS pair-adjustment lookups of the <c>kern</c> feature, or the legacy
/// <c>kern</c> table when the font has no GPOS kerning. Only horizontal advance adjustments of the first glyph are
/// applied, which is what pair kerning needs.
/// </summary>
sealed class OpenTypeKerning
{
    const uint GposTag = 0x47504F53;
    const uint KernTag = 0x6B65726E;
    const int PairAdjustment = 2;
    const int Extension = 9;

    static readonly ConditionalWeakTable<SKTypeface, OpenTypeKerning> Fonts = new();
    static readonly OpenTypeKerning None = new([], [], 1);

    readonly List<PairLookup> _gpos;
    readonly Dictionary<uint, short> _legacy;
    readonly float _unitsPerEm;
    readonly ConcurrentDictionary<uint, short> _pairs = new();

    OpenTypeKerning(List<PairLookup> gpos, Dictionary<uint, short> legacy, int unitsPerEm)
    {
        _gpos = gpos;
        _legacy = legacy;
        _unitsPerEm = Math.Max(1, unitsPerEm);
    }

    /// <summary>Whether the font has any kerning data.</summary>
    internal bool HasKerning => _gpos.Count > 0 || _legacy.Count > 0;

    /// <summary>The kerning of a typeface, read once and shared.</summary>
    internal static OpenTypeKerning For(SKTypeface typeface) => Fonts.GetValue(typeface, Read);

    /// <summary>Adds each pair's adjustment to the advance of its first glyph.</summary>
    internal void Apply(ReadOnlySpan<ushort> glyphs, Span<float> advances, float size)
    {
        if (!HasKerning)
        {
            return;
        }

        var scale = size / _unitsPerEm;
        for (var i = 0; i + 1 < glyphs.Length; i++)
        {
            advances[i] += Adjustment(glyphs[i], glyphs[i + 1]) * scale;
        }
    }

    /// <summary>The adjustment of a glyph pair in font units.</summary>
    internal short Adjustment(ushort left, ushort right) =>
        _pairs.GetOrAdd(((uint)left << 16) | right, static (key, kerning) => kerning.Lookup(key), this);

    short Lookup(uint key)
    {
        var left = (ushort)(key >> 16);
        var right = (ushort)key;
        if (_gpos.Count == 0)
        {
            return _legacy.GetValueOrDefault(key);
        }

        var total = 0;
        foreach (var lookup in _gpos)
        {
            total += lookup.Adjustment(left, right);
        }

        return (short)Math.Clamp(total, short.MinValue, short.MaxValue);
    }

    static OpenTypeKerning Read(SKTypeface typeface) => FromTables(
        typeface.TryGetTableData(GposTag, out var gpos) ? gpos : null,
        typeface.TryGetTableData(KernTag, out var kern) ? kern : null,
        typeface.UnitsPerEm);

    /// <summary>Kerning from raw GPOS and kern table bytes; GPOS wins when it has kerning lookups.</summary>
    internal static OpenTypeKerning FromTables(byte[]? gposData, byte[]? kernData, int unitsPerEm)
    {
        try
        {
            var gpos = gposData is null ? [] : ReadGpos(gposData);
            var legacy = gpos.Count == 0 && kernData is not null ? ReadKern(kernData) : [];
            return gpos.Count == 0 && legacy.Count == 0 ? None : new OpenTypeKerning(gpos, legacy, unitsPerEm);
        }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            // A malformed table only loses kerning; text still lays out with nominal advances.
            return None;
        }
    }

    static List<PairLookup> ReadGpos(byte[] data)
    {
        var lookups = new List<PairLookup>();
        var featureList = U16(data, 6);
        var lookupList = U16(data, 8);
        foreach (var index in KernLookups(data, featureList))
        {
            var offset = lookupList + U16(data, lookupList + 2 + (index * 2));
            var type = U16(data, offset);
            var count = U16(data, offset + 4);
            var tables = new List<PairTable>(count);
            for (var s = 0; s < count; s++)
            {
                var subtable = offset + U16(data, offset + 6 + (s * 2));
                var subtype = type;
                if (type == Extension)
                {
                    subtype = U16(data, subtable + 2);
                    subtable += (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(subtable + 4));
                }

                if (subtype == PairAdjustment)
                {
                    tables.Add(new PairTable(data, subtable));
                }
            }

            if (tables.Count > 0)
            {
                lookups.Add(new PairLookup(tables));
            }
        }

        return lookups;
    }

    /// <summary>A lookup's subtables; the first one that matches a pair decides its adjustment.</summary>
    sealed class PairLookup(List<PairTable> tables)
    {
        internal int Adjustment(ushort left, ushort right)
        {
            foreach (var table in tables)
            {
                if (table.TryAdjustment(left, right, out var value))
                {
                    return value;
                }
            }

            return 0;
        }
    }

    /// <summary>Indices of the lookups the font's <c>kern</c> features use, in lookup order.</summary>
    static SortedSet<int> KernLookups(byte[] data, int featureList)
    {
        var lookups = new SortedSet<int>();
        var count = U16(data, featureList);
        for (var f = 0; f < count; f++)
        {
            var record = featureList + 2 + (f * 6);
            if (BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(record)) != KernTag)
            {
                continue;
            }

            var feature = featureList + U16(data, record + 4);
            var indices = U16(data, feature + 2);
            for (var i = 0; i < indices; i++)
            {
                lookups.Add(U16(data, feature + 4 + (i * 2)));
            }
        }

        return lookups;
    }

    static Dictionary<uint, short> ReadKern(byte[] data)
    {
        var pairs = new Dictionary<uint, short>();
        if (U16(data, 0) != 0)
        {
            return pairs;
        }

        var offset = 4;
        for (var t = U16(data, 2); t > 0; t--)
        {
            var length = U16(data, offset + 2);
            var coverage = U16(data, offset + 4);
            if (coverage >> 8 == 0 && (coverage & 1) == 1)
            {
                var count = U16(data, offset + 6);
                for (var p = 0; p < count; p++)
                {
                    var pair = offset + 14 + (p * 6);
                    pairs.TryAdd(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(pair)),
                        BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(pair + 4)));
                }
            }

            offset += length;
        }

        return pairs;
    }

    static int U16(byte[] data, int offset) => BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset));

    /// <summary>One GPOS pair-adjustment subtable, format 1 (glyph pairs) or 2 (class pairs).</summary>
    sealed class PairTable(byte[] data, int offset)
    {
        readonly int _format = U16(data, offset);
        readonly int _coverage = offset + U16(data, offset + 2);
        readonly int _valueFormat1 = U16(data, offset + 4);
        readonly int _valueFormat2 = U16(data, offset + 6);

        /// <summary>Whether this subtable applies to the pair: format 1 when it lists it, format 2 when it covers it.</summary>
        internal bool TryAdjustment(ushort left, ushort right, out int value)
        {
            value = 0;
            var index = CoverageIndex(data, _coverage, left);
            if (index < 0)
            {
                return false;
            }

            if (_format == 2)
            {
                value = ClassPair(left, right);
                return true;
            }

            return _format == 1 && GlyphPair(index, right, out value);
        }

        int RecordSize => ValueSize(_valueFormat1) + ValueSize(_valueFormat2);

        bool GlyphPair(int index, ushort right, out int value)
        {
            value = 0;
            if (index >= U16(data, offset + 8))
            {
                return false;
            }

            var set = offset + U16(data, offset + 10 + (index * 2));
            var count = U16(data, set);
            var size = 2 + RecordSize;
            int low = 0, high = count - 1;
            while (low <= high)
            {
                var mid = (low + high) / 2;
                var record = set + 2 + (mid * size);
                var second = U16(data, record);
                if (second == right)
                {
                    value = XAdvance(record + 2, _valueFormat1);
                    return true;
                }

                (low, high) = second < right ? (mid + 1, high) : (low, mid - 1);
            }

            return false;
        }

        int ClassPair(ushort left, ushort right)
        {
            var class1 = ClassOf(data, offset + U16(data, offset + 8), left);
            var class2 = ClassOf(data, offset + U16(data, offset + 10), right);
            var class1Count = U16(data, offset + 12);
            var class2Count = U16(data, offset + 14);
            if (class1 >= class1Count || class2 >= class2Count)
            {
                return 0;
            }

            var record = offset + 16 + (((class1 * class2Count) + class2) * RecordSize);
            return XAdvance(record, _valueFormat1);
        }

        int XAdvance(int record, int format) => (format & 4) == 0
            ? 0
            : BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(record + ValueSize(format & 3)));
    }

    static int ValueSize(int format) => 2 * int.PopCount(format & 0xFF);

    static int CoverageIndex(byte[] data, int coverage, ushort glyph)
    {
        var count = U16(data, coverage + 2);
        if (U16(data, coverage) == 1)
        {
            int low = 0, high = count - 1;
            while (low <= high)
            {
                var mid = (low + high) / 2;
                var value = U16(data, coverage + 4 + (mid * 2));
                if (value == glyph)
                {
                    return mid;
                }

                (low, high) = value < glyph ? (mid + 1, high) : (low, mid - 1);
            }

            return -1;
        }

        for (var r = 0; r < count; r++)
        {
            var range = coverage + 4 + (r * 6);
            if (glyph >= U16(data, range) && glyph <= U16(data, range + 2))
            {
                return U16(data, range + 4) + glyph - U16(data, range);
            }
        }

        return -1;
    }

    static int ClassOf(byte[] data, int classDef, ushort glyph)
    {
        if (U16(data, classDef) == 1)
        {
            var first = U16(data, classDef + 2);
            var count = U16(data, classDef + 4);
            return glyph >= first && glyph < first + count ? U16(data, classDef + 6 + ((glyph - first) * 2)) : 0;
        }

        var ranges = U16(data, classDef + 2);
        for (var r = 0; r < ranges; r++)
        {
            var range = classDef + 4 + (r * 6);
            if (glyph >= U16(data, range) && glyph <= U16(data, range + 2))
            {
                return U16(data, range + 4);
            }
        }

        return 0;
    }
}
