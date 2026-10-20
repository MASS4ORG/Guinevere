namespace Guinevere;

/// <summary>Value identity of text; plain cache hits need no rich-content object or range array.</summary>
readonly record struct TextLayoutContent(string Text, RichTextContent? Styled = null)
{
    internal StyledRange[] Ranges => Styled?.Ranges ?? [new StyledRange(0, Text.Length, default, -1, null)];

    internal TextLink[] Links => Styled?.Links ?? [];

    internal long Weight => Text.Length + (Styled?.Ranges.Length ?? 1) * 16L;

    /// <summary>Plain content without allocating a range snapshot.</summary>
    public static implicit operator TextLayoutContent(string text) => new(text);

    /// <summary>Normalizes unstyled rich content to the same key as an ordinary label.</summary>
    public static implicit operator TextLayoutContent(RichTextContent content) =>
        content.Links.Length == 0 && content.Ranges.Length == 1
        && content.Ranges[0] == new StyledRange(0, content.Text.Length, default, -1, null)
            ? new TextLayoutContent(content.Text)
            : new TextLayoutContent(content.Text, content);
}
