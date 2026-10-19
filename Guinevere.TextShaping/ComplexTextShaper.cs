using System.Runtime.CompilerServices;
using HarfBuzzSharp;
using SkiaSharp;
using SkiaSharp.HarfBuzz;
using HarfBuzzFont = HarfBuzzSharp.Font;

namespace Guinevere;

/// <summary>
/// A full OpenType shaper: ligatures, contextual alternates, mark positioning, kerning and complex scripts. Assign
/// it to <see cref="Gui.TextShaper"/>; text layout caches the shaped runs.
/// </summary>
public sealed class ComplexTextShaper : ITextShaper
{
    static readonly Feature[] NoKerning = [new(Tag.Parse("kern"), 0)];

    readonly ConditionalWeakTable<SKTypeface, HarfBuzzFont> _fonts = new();

    /// <summary>The shared instance.</summary>
    public static ComplexTextShaper Instance { get; } = new();

    /// <inheritdoc />
    public ShapedText Shape(Font font, string text, int start, int length, bool kerning)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(text);
        var typeface = font.SkFont.Typeface;
        var shaper = _fonts.GetValue(typeface, CreateFont);
        using var buffer = new HarfBuzzSharp.Buffer();
        buffer.AddUtf16(text, start, length);
        buffer.GuessSegmentProperties();
        shaper.Shape(buffer, kerning ? [] : NoKerning);
        return Result(buffer, start, font.Size / Math.Max(1, typeface.UnitsPerEm));
    }

    static ShapedText Result(HarfBuzzSharp.Buffer buffer, int start, float scale)
    {
        var infos = buffer.GetGlyphInfoSpan();
        var positions = buffer.GetGlyphPositionSpan();
        var glyphs = new ushort[infos.Length];
        var advances = new float[infos.Length];
        var clusters = new int[infos.Length];
        var offsets = new float[infos.Length];
        var rises = new float[infos.Length];
        var shifted = false;
        var raised = false;
        for (var i = 0; i < infos.Length; i++)
        {
            glyphs[i] = (ushort)infos[i].Codepoint;
            clusters[i] = (int)infos[i].Cluster - start;
            advances[i] = positions[i].XAdvance * scale;
            offsets[i] = positions[i].XOffset * scale;
            rises[i] = -positions[i].YOffset * scale;
            shifted |= positions[i].XOffset != 0;
            raised |= positions[i].YOffset != 0;
        }

        return new ShapedText(glyphs, advances, clusters, shifted ? offsets : null, raised ? rises : null);
    }

    /// <summary>A HarfBuzz font in design units, shared by every size of the typeface.</summary>
    static HarfBuzzFont CreateFont(SKTypeface typeface)
    {
        using var stream = typeface.OpenStream(out var index);
        using var blob = stream.ToHarfBuzzBlob();
        var face = new Face(blob, index);
        var font = new HarfBuzzFont(face);
        font.SetFunctionsOpenType();
        var unitsPerEm = Math.Max(1, typeface.UnitsPerEm);
        font.SetScale(unitsPerEm, unitsPerEm);
        return font;
    }
}
