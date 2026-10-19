namespace Guinevere.Tests;

/// <summary>GPOS pair adjustment and legacy kern parsing, against small hand-built tables.</summary>
public class OpenTypeKerningTests
{
    /// <summary>Big-endian table bytes with offsets patched once their targets are written.</summary>
    sealed class Table
    {
        readonly List<byte> _bytes = [];

        internal int Position => _bytes.Count;

        internal Table U16(int value)
        {
            _bytes.Add((byte)(value >> 8));
            _bytes.Add((byte)value);
            return this;
        }

        internal Table U32(int value) => U16(value >> 16).U16(value & 0xFFFF);

        internal Table Tag(string tag) => U16((tag[0] << 8) | tag[1]).U16((tag[2] << 8) | tag[3]);

        internal int Reserve()
        {
            U16(0);
            return Position - 2;
        }

        /// <summary>Writes the distance from <paramref name="origin"/> to the current position at a reserved slot.</summary>
        internal void Patch(int slot, int origin)
        {
            var value = Position - origin;
            _bytes[slot] = (byte)(value >> 8);
            _bytes[slot + 1] = (byte)value;
        }

        internal byte[] ToArray() => [.. _bytes];
    }

    /// <summary>
    /// Lookup 0 holds a glyph-pair subtable (10,20 → -50) before a class subtable (glyphs 10–11 against 20–21 →
    /// -30). Lookup 1 is an extension wrapping a glyph pair (5,6 → +40) with an x placement before the advance.
    /// A "liga" feature points at a lookup that must not be read as kerning.
    /// </summary>
    static byte[] Gpos()
    {
        var t = new Table();
        t.U16(1).U16(0).U16(0);
        var featureListSlot = t.Reserve();
        var lookupListSlot = t.Reserve();

        t.Patch(featureListSlot, 0);
        var featureList = t.Position;
        t.U16(2).Tag("liga");
        var ligaSlot = t.Reserve();
        t.Tag("kern");
        var kernSlot = t.Reserve();
        t.Patch(ligaSlot, featureList);
        t.U16(0).U16(1).U16(2);
        t.Patch(kernSlot, featureList);
        t.U16(0).U16(2).U16(0).U16(1);

        t.Patch(lookupListSlot, 0);
        var lookupList = t.Position;
        t.U16(3);
        var lookupSlots = new[] { t.Reserve(), t.Reserve(), t.Reserve() };

        t.Patch(lookupSlots[0], lookupList);
        var lookup0 = t.Position;
        t.U16(2).U16(0).U16(2);
        var glyphPairSlot = t.Reserve();
        var classPairSlot = t.Reserve();
        t.Patch(glyphPairSlot, lookup0);
        GlyphPair(t, 10, 20, -50, 4);
        t.Patch(classPairSlot, lookup0);
        ClassPair(t);

        t.Patch(lookupSlots[1], lookupList);
        var lookup1 = t.Position;
        t.U16(9).U16(0).U16(1);
        var extensionSlot = t.Reserve();
        t.Patch(extensionSlot, lookup1);
        t.U16(1).U16(2).U32(8);
        GlyphPair(t, 5, 6, 40, 5);

        t.Patch(lookupSlots[2], lookupList);
        t.U16(4).U16(0).U16(0);
        return t.ToArray();
    }

    static void GlyphPair(Table t, int left, int right, int value, int valueFormat)
    {
        var start = t.Position;
        t.U16(1);
        var coverageSlot = t.Reserve();
        t.U16(valueFormat).U16(0).U16(1);
        var setSlot = t.Reserve();
        t.Patch(setSlot, start);
        t.U16(1).U16(right);
        if ((valueFormat & 1) != 0)
        {
            t.U16(7);
        }

        t.U16(value & 0xFFFF);
        t.Patch(coverageSlot, start);
        t.U16(1).U16(1).U16(left);
    }

    static void ClassPair(Table t)
    {
        var start = t.Position;
        t.U16(2);
        var coverageSlot = t.Reserve();
        t.U16(4).U16(0);
        var class1Slot = t.Reserve();
        var class2Slot = t.Reserve();
        t.U16(2).U16(2);
        t.U16(0).U16(0).U16(0).U16(-30 & 0xFFFF);
        t.Patch(coverageSlot, start);
        t.U16(2).U16(1).U16(10).U16(12).U16(0);
        t.Patch(class1Slot, start);
        t.U16(1).U16(10).U16(3).U16(1).U16(1).U16(0);
        t.Patch(class2Slot, start);
        t.U16(2).U16(1).U16(20).U16(21).U16(1);
    }

    static byte[] Kern()
    {
        var t = new Table();
        t.U16(0).U16(1);
        t.U16(0).U16(20).U16(1);
        t.U16(1).U16(6).U16(0).U16(0);
        t.U16(3).U16(4).U16(-25 & 0xFFFF);
        return t.ToArray();
    }

    [Fact]
    public void FirstMatchingSubtableOfALookupDecidesThePair()
    {
        var kerning = OpenTypeKerning.FromTables(Gpos(), null, 1000);

        Assert.True(kerning.HasKerning);
        Assert.Equal(-50, kerning.Adjustment(10, 20));
        Assert.Equal(-30, kerning.Adjustment(10, 21));
        Assert.Equal(-30, kerning.Adjustment(11, 20));
        Assert.Equal(0, kerning.Adjustment(12, 20));
        Assert.Equal(0, kerning.Adjustment(13, 20));
    }

    [Fact]
    public void ExtensionLookupsAreFollowedAndPlacementIsSkipped()
    {
        var kerning = OpenTypeKerning.FromTables(Gpos(), null, 1000);

        Assert.Equal(40, kerning.Adjustment(5, 6));
        Assert.Equal(0, kerning.Adjustment(6, 5));
    }

    [Fact]
    public void ApplyScalesFontUnitsToTheSize()
    {
        var kerning = OpenTypeKerning.FromTables(Gpos(), null, 1000);
        float[] advances = [5f, 5f, 5f];

        kerning.Apply([10, 20, 99], advances, 10f);

        Assert.Equal([4.5f, 5f, 5f], advances);
    }

    [Fact]
    public void LegacyKernTableIsUsedWithoutGposKerning()
    {
        var kerning = OpenTypeKerning.FromTables(null, Kern(), 1000);

        Assert.Equal(-25, kerning.Adjustment(3, 4));
        Assert.Equal(0, kerning.Adjustment(4, 3));
    }

    [Fact]
    public void MalformedOrEmptyTablesMeanNoKerning()
    {
        Assert.False(OpenTypeKerning.FromTables([0, 1, 0], null, 1000).HasKerning);
        Assert.False(OpenTypeKerning.FromTables(null, [0, 1], 1000).HasKerning);
        Assert.False(OpenTypeKerning.FromTables(null, null, 1000).HasKerning);
        float[] advances = [1f, 1f];
        OpenTypeKerning.FromTables(null, null, 1000).Apply([1, 2], advances, 10f);
        Assert.Equal([1f, 1f], advances);
    }

    [Fact]
    public void RealFontKernsAPairThroughGpos()
    {
        var font = Font.FromFile(FontRegistryTests.FontPath(), 40);
        var glyphs = font.SkFont.GetGlyphs("AV");

        Assert.True(OpenTypeKerning.For(font.SkFont.Typeface).Adjustment(glyphs[0], glyphs[1]) < 0);
    }
}
