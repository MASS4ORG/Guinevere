using System.Text;

namespace Guinevere;

readonly record struct FontRun(string Text, Font Font);

/// <summary>
/// A source range drawn with one font; hidden ranges are kept for offsets but never drawn, and missing ranges hold
/// characters no font can render.
/// </summary>
readonly record struct FontRange(int Start, int End, Font Font, bool Hidden, bool Missing = false);

/// <summary>Splits Unicode scalars into shared drawing and measurement runs without shaping.</summary>
static class FontTextLayout
{
    internal static List<FontRun> Create(string text, Font mainFont, Font widgetFont, Font iconFont)
    {
        text = WithoutVariationSelectors(text);
        if (text.Length == 0)
        {
            return [];
        }

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
        if (currentFont is not null)
        {
            runs.Add(new FontRun(text[start..], currentFont));
        }

        return runs;
    }

    /// <summary>
    /// Splits <c>text[start..end]</c> by fallback font while keeping source offsets: variation selectors become
    /// hidden zero-width ranges instead of being removed, and characters no font has become missing ranges.
    /// </summary>
    internal static void AppendRanges(List<FontRange> ranges, string text, int start, int end, Font mainFont,
        Font widgetFont, Font iconFont)
    {
        Font[]? fallbackFonts = null;
        FontRange? current = null;
        var offset = start;
        foreach (var rune in text.AsSpan(start, end - start).EnumerateRunes())
        {
            var next = Classify(rune, offset, current?.Font ?? mainFont, mainFont, ref fallbackFonts, widgetFont,
                iconFont);
            current = Extend(ranges, current, next);
            offset += rune.Utf16SequenceLength;
        }

        if (current is { } last)
        {
            ranges.Add(last with { End = end });
        }
    }

    /// <summary>The empty range a rune would start: its font, and whether it is hidden or missing.</summary>
    static FontRange Classify(Rune rune, int offset, Font previous, Font mainFont, ref Font[]? fallbacks,
        Font widgetFont, Font iconFont)
    {
        if (IsVariationSelector(rune))
        {
            return new FontRange(offset, offset, previous, true);
        }

        var selected = SelectFont(rune, mainFont, ref fallbacks, widgetFont, iconFont);
        var missing = !Rune.IsControl(rune) && selected.SkFont.GetGlyph(rune.Value) == 0;
        return new FontRange(offset, offset, selected, false, missing);
    }

    /// <summary>Keeps the current range when the next rune matches it, else closes it and starts the next.</summary>
    static FontRange Extend(List<FontRange> ranges, FontRange? current, FontRange next)
    {
        if (current is not { } run)
        {
            return next;
        }

        if (SameKind(run, next))
        {
            return run;
        }

        ranges.Add(run with { End = next.Start });
        return next;
    }

    static bool SameKind(FontRange run, FontRange next) =>
        run.Font == next.Font && run.Hidden == next.Hidden && run.Missing == next.Missing;

    static Font[] FallbackFonts(Font primary, Font widget, Font emoji)
    {
        var fonts = new List<Font>(primary.Fallbacks.Count + widget.Fallbacks.Count + emoji.Fallbacks.Count + 2);
        foreach (var font in primary.Fallbacks)
        {
            fonts.Add(font.Resized(primary.Size));
        }

        fonts.Add(widget.Resized(primary.Size));
        foreach (var font in widget.Fallbacks)
        {
            fonts.Add(font.Resized(primary.Size));
        }

        fonts.Add(emoji);
        foreach (var font in emoji.Fallbacks)
        {
            fonts.Add(font.Resized(primary.Size));
        }

        return [.. fonts];
    }

    static Font SelectFont(Rune rune, Font primary, ref Font[]? fallbacks, Font widget, Font lastResort)
    {
        if (primary.SkFont.GetGlyph(rune.Value) != 0)
        {
            return primary;
        }

        fallbacks ??= FallbackFonts(primary, widget, lastResort);
        foreach (var fallback in fallbacks)
        {
            if (fallback.SkFont.GetGlyph(rune.Value) != 0)
            {
                return fallback;
            }
        }

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
        if (first < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text[..first]);
        foreach (var rune in text.AsSpan(first).EnumerateRunes())
        {
            if (!IsVariationSelector(rune))
            {
                builder.Append(rune.ToString());
            }
        }

        return builder.ToString();
    }

    static bool IsVariationSelector(Rune rune) => rune.Value is >= 0xfe00 and <= 0xfe0f
        or >= 0xe0100 and <= 0xe01ef;

}
