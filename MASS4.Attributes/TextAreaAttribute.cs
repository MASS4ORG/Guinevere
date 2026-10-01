namespace MASS4.Attributes;

/// <summary>
/// Edits a string in a multi-line box that grows from <paramref name="minLines"/> to <paramref name="maxLines"/>
/// lines with its text, then scrolls.
/// </summary>
/// <param name="minLines">Lines shown when the text is short.</param>
/// <param name="maxLines">Lines shown before the box scrolls.</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class TextAreaAttribute(int minLines = 3, int maxLines = 10) : Attribute
{
    /// <summary>Lines shown when the text is short.</summary>
    public int MinLines { get; } = Math.Max(1, minLines);

    /// <summary>Lines shown before the box scrolls; never fewer than <see cref="MinLines"/>.</summary>
    public int MaxLines { get; } = Math.Max(Math.Max(1, minLines), maxLines);
}
