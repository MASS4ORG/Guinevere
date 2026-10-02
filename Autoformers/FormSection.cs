namespace Autoformers;

/// <summary>A group of fields drawn under one heading, such as an object's own members.</summary>
/// <param name="Title">The heading.</param>
/// <param name="Target">The object the fields belong to.</param>
/// <param name="Fields">The editable members, in inspector order.</param>
/// <param name="Removable">Whether the shell may offer to remove this section's target.</param>
public sealed record FormSection(
    string Title,
    object Target,
    IReadOnlyList<FormField> Fields,
    bool Removable = false)
{
    /// <summary>The <c>[Button]</c> methods to draw after the fields, in declaration order.</summary>
    public IReadOnlyList<Button> Buttons { get; init; } = [];

    /// <summary>
    /// The target's own on/off switch, chosen by the consumer, so a shell can draw it as a checkbox in the
    /// section heading instead of as a row among the other members.
    /// </summary>
    public FormField? EnabledField { get; init; }

    /// <summary>The fields to draw in the body — everything except what the heading already shows.</summary>
    public IReadOnlyList<FormField> BodyFields =>
        EnabledField is null ? Fields : [.. Fields.Where(candidate => !ReferenceEquals(candidate, EnabledField))];
}
