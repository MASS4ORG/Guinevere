namespace Guinevere;

/// <summary>Glyphs for one run of text in one font.</summary>
/// <param name="Glyphs">Glyph ids in visual order.</param>
/// <param name="Advances">Horizontal advance of each glyph in pixels, including kerning.</param>
/// <param name="Clusters">UTF-16 offset of each glyph's source character, relative to the run's start.</param>
/// <param name="XOffsets">Horizontal shift of each glyph from its pen position, such as for marks, or null.</param>
/// <param name="YOffsets">Downward shift of each glyph from the baseline, such as for stacked marks, or null.</param>
public sealed record ShapedText(ushort[] Glyphs, float[] Advances, int[] Clusters, float[]? XOffsets = null,
    float[]? YOffsets = null);

/// <summary>
/// Turns text into positioned glyphs. Text layout calls it once per font run and caches the result, so a shaper may
/// be expensive; implementations must be pure for equal input.
/// </summary>
public interface ITextShaper
{
    /// <summary>Shapes <c>text[start..(start + length)]</c> with <paramref name="font"/>.</summary>
    /// <param name="font">The font, at its drawing size.</param>
    /// <param name="text">The source the run indexes into, so shapers can look at context.</param>
    /// <param name="start">First UTF-16 offset of the run.</param>
    /// <param name="length">UTF-16 length of the run.</param>
    /// <param name="kerning">Whether pair kerning applies.</param>
    ShapedText Shape(Font font, string text, int start, int length, bool kerning);
}

/// <summary>
/// The built-in shaper used when no full shaper is plugged in: it skips shaping, mapping one glyph per code point
/// from the font's character map with its advance and pair kerning from the font's GPOS or kern table. Ligatures,
/// mark positioning and complex scripts need the <c>Guinevere.TextShaping</c> package.
/// </summary>
public sealed class BasicTextShaper : ITextShaper
{
    /// <summary>The shared instance.</summary>
    public static BasicTextShaper Instance { get; } = new();

    /// <inheritdoc />
    public ShapedText Shape(Font font, string text, int start, int length, bool kerning)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(text);
        var span = text.AsSpan(start, length);
        var glyphs = font.SkFont.GetGlyphs(span);
        var advances = font.SkFont.GetGlyphWidths(glyphs, null);
        var clusters = new int[glyphs.Length];
        var glyph = 0;
        var offset = 0;
        foreach (var rune in span.EnumerateRunes())
        {
            if (glyph < clusters.Length)
            {
                clusters[glyph++] = offset;
            }

            offset += rune.Utf16SequenceLength;
        }

        if (kerning && glyphs.Length > 1)
        {
            OpenTypeKerning.For(font.SkFont.Typeface).Apply(glyphs, advances, font.Size);
        }

        return new ShapedText(glyphs, advances, clusters);
    }
}
