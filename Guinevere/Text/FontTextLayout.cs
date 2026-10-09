using System.Text;

namespace Guinevere;

readonly record struct FontRun(string Text, Font Font);

/// <summary>Splits Unicode scalars into shared drawing and measurement runs without shaping.</summary>
static class FontTextLayout
{
    internal static List<FontRun> Create(string text, Font mainFont, Font widgetFont, Font iconFont)
    {
        text = WithoutVariationSelectors(text);
        if (text.Length == 0) return [];
        Font[]? fallbackFonts = null;
        var runs = new List<FontRun>();
        Font? currentFont = null;
        var start = 0;
        var offset = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var selected = SelectFont(rune, mainFont, ref fallbackFonts, widgetFont, iconFont);
            if (currentFont is not null && selected != currentFont)
            {
                runs.Add(new FontRun(text[start..offset], currentFont));
                start = offset;
            }
            currentFont = selected;
            offset += rune.Utf16SequenceLength;
        }
        if (currentFont is not null) runs.Add(new FontRun(text[start..], currentFont));
        return runs;
    }

    static Font[] FallbackFonts(Font primary, Font widget, Font emoji)
    {
        var fonts = new List<Font>(primary.Fallbacks.Count + widget.Fallbacks.Count + emoji.Fallbacks.Count + 2);
        foreach (var font in primary.Fallbacks) fonts.Add(font.Resized(primary.Size));
        fonts.Add(widget.Resized(primary.Size));
        foreach (var font in widget.Fallbacks) fonts.Add(font.Resized(primary.Size));
        fonts.Add(emoji);
        foreach (var font in emoji.Fallbacks) fonts.Add(font.Resized(primary.Size));
        return [.. fonts];
    }

    static Font SelectFont(Rune rune, Font primary, ref Font[]? fallbacks, Font widget, Font lastResort)
    {
        if (primary.SkFont.GetGlyph(rune.Value) != 0) return primary;
        fallbacks ??= FallbackFonts(primary, widget, lastResort);
        foreach (var fallback in fallbacks)
            if (fallback.SkFont.GetGlyph(rune.Value) != 0) return fallback;
        return lastResort;
    }

    static string WithoutVariationSelectors(string text)
    {
        var first = -1;
        var offset = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (IsVariationSelector(rune)) { first = offset; break; }
            offset += rune.Utf16SequenceLength;
        }
        if (first < 0) return text;
        var builder = new StringBuilder(text[..first]);
        foreach (var rune in text.AsSpan(first).EnumerateRunes())
            if (!IsVariationSelector(rune)) builder.Append(rune.ToString());
        return builder.ToString();
    }

    static bool IsVariationSelector(Rune rune) => rune.Value is >= 0xfe00 and <= 0xfe0f
        or >= 0xe0100 and <= 0xe01ef;

}
