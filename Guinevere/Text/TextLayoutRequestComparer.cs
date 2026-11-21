namespace Guinevere;

/// <summary>
/// Compares layout requests by value and lets the cache look up plain text by its characters; both forms of one
/// request hash alike and a styled request never matches a plain one.
/// </summary>
sealed class TextLayoutRequestComparer : IEqualityComparer<TextLayoutRequest>,
    IAlternateEqualityComparer<PlainTextRequest, TextLayoutRequest>
{
    /// <summary>The shared comparer.</summary>
    internal static TextLayoutRequestComparer Instance { get; } = new();

    /// <inheritdoc />
    public bool Equals(TextLayoutRequest x, TextLayoutRequest y) => x.Equals(y);

    /// <inheritdoc />
    public int GetHashCode(TextLayoutRequest obj) => Hash(obj.Content.Text, obj.Content.Styled, obj.Font, obj.Scale,
        obj.MaxWidth, obj.Options, obj.FontsVersion);

    /// <inheritdoc />
    public bool Equals(PlainTextRequest alternate, TextLayoutRequest other) => other.Content.Styled is null
        && alternate.Text.SequenceEqual(other.Content.Text) && alternate.Font.Equals(other.Font)
        && alternate.Emoji.Equals(other.Emoji) && alternate.Widget.Equals(other.Widget)
        && alternate.Scale.Equals(other.Scale) && alternate.MaxWidth.Equals(other.MaxWidth)
        && alternate.Options.Equals(other.Options) && alternate.Alignment == other.Alignment
        && alternate.FontsVersion == other.FontsVersion && Equals(alternate.Shaper, other.Shaper);

    /// <inheritdoc />
    public int GetHashCode(PlainTextRequest alternate) => Hash(alternate.Text, null, alternate.Font, alternate.Scale,
        alternate.MaxWidth, alternate.Options, alternate.FontsVersion);

    /// <inheritdoc />
    public TextLayoutRequest Create(PlainTextRequest alternate) => alternate.ToRequest();

    static int Hash(ReadOnlySpan<char> text, RichTextContent? styled, FontKey font, float scale, float maxWidth,
        TextLayoutOptions options, int fontsVersion) =>
        HashCode.Combine(string.GetHashCode(text), styled, font, scale, maxWidth, options, fontsVersion);
}
