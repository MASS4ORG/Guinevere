namespace MASS4.Attributes;

/// <summary>
/// Overrides the label an enum member draws as, in a dropdown built by reflection. Without it, the
/// member's own name is humanized instead — which is wrong for a name that is not English at all, such
/// as a language's own endonym.
/// </summary>
/// <param name="label">The label to draw for this member.</param>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class EnumLabelAttribute(string label) : Attribute
{
    /// <summary>The label to draw for this member.</summary>
    public string Label { get; } = label;
}
