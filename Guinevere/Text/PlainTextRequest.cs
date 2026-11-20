namespace Guinevere;

/// <summary>
/// A layout request for plain text that borrows its characters, so the cache can be asked about text held in a
/// buffer without creating a string; it identifies the same layout as the equivalent <see cref="TextLayoutRequest"/>.
/// </summary>
readonly ref struct PlainTextRequest
{
    internal PlainTextRequest(ReadOnlySpan<char> text, FontKey font, FontKey emoji, FontKey widget, float scale,
        float maxWidth, TextLayoutOptions options, TextAlignment alignment, int fontsVersion, ITextShaper shaper)
    {
        Text = text;
        Font = font;
        Emoji = emoji;
        Widget = widget;
        Scale = scale;
        MaxWidth = maxWidth;
        Options = options;
        Alignment = alignment;
        FontsVersion = fontsVersion;
        Shaper = shaper;
    }

    /// <summary>The characters to lay out.</summary>
    internal ReadOnlySpan<char> Text { get; }

    /// <summary>Inherited font at its display-scaled size.</summary>
    internal FontKey Font { get; }

    /// <summary>Last-resort fallback font.</summary>
    internal FontKey Emoji { get; }

    /// <summary>Icon font consulted during fallback.</summary>
    internal FontKey Widget { get; }

    /// <summary>Display scale applied to logical sizes.</summary>
    internal float Scale { get; }

    /// <summary>Wrap width, or zero or less for none.</summary>
    internal float MaxWidth { get; }

    /// <summary>Wrapping and truncation options.</summary>
    internal TextLayoutOptions Options { get; }

    /// <summary>Horizontal placement of each line.</summary>
    internal TextAlignment Alignment { get; }

    /// <summary>Font registry version.</summary>
    internal int FontsVersion { get; }

    /// <summary>Turns font runs into glyphs.</summary>
    internal ITextShaper Shaper { get; }

    /// <summary>The cache budget this request takes, as the matching <see cref="TextLayoutRequest"/> does.</summary>
    internal long Weight =>
        TextLayoutContent.WeightOf(Text.Length, 1) + Options.TabStops.Count * 2L + Font.Weight + Emoji.Weight
        + Widget.Weight;

    /// <summary>This request with another wrap width.</summary>
    internal PlainTextRequest WithMaxWidth(float maxWidth) =>
        new(Text, Font, Emoji, Widget, Scale, maxWidth, Options, Alignment, FontsVersion, Shaper);

    /// <summary>The request for <paramref name="content"/> under this request's fonts and constraints.</summary>
    internal TextLayoutRequest ToRequest(TextLayoutContent content) =>
        new(content, Font, Emoji, Widget, Scale, MaxWidth, Options, Alignment, FontsVersion, Shaper);

    /// <summary>The request with its characters copied into one string.</summary>
    internal TextLayoutRequest ToRequest() => ToRequest(new TextLayoutContent(Text.ToString()));
}
