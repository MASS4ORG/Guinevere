namespace MASS4.Attributes;

/// <summary>A heading drawn above the field, optionally with a subtitle and a separating line.</summary>
/// <param name="title">The heading text.</param>
/// <param name="subtitle">Smaller text under the heading, or null.</param>
/// <param name="bold">Whether the heading is bold.</param>
/// <param name="horizontalLine">Whether a line separates the heading from the field.</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class TitleAttribute(string title, string? subtitle = null, bool bold = true,
    bool horizontalLine = true) : Attribute
{
    /// <summary>The heading text.</summary>
    public string Title { get; } = title;

    /// <summary>Smaller text under the heading, or null.</summary>
    public string? Subtitle { get; } = subtitle;

    /// <summary>Whether the heading is bold.</summary>
    public bool Bold { get; } = bold;

    /// <summary>Whether a line separates the heading from the field.</summary>
    public bool HorizontalLine { get; } = horizontalLine;
}
