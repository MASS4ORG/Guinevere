namespace Guinevere.Forms;

/// <summary>
/// Everything an inspector needs to draw an object: its sections, each holding editable fields. The
/// shell walks this and picks a drawer per <see cref="FormField.ValueType"/>; it never reflects.
/// </summary>
/// <param name="Target">The object being inspected.</param>
/// <param name="Sections">The groups to draw, in order.</param>
public sealed record FormModel(object Target, IReadOnlyList<FormSection> Sections)
{
    /// <summary>A model with nothing to show.</summary>
    public static FormModel Empty { get; } = new(new object(), []);
}
