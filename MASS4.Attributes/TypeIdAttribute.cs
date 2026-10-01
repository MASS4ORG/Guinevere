namespace MASS4.Attributes;

/// <summary>
/// Assigns a stable Guid identifier to a class for polymorphic JSON serialization.
/// The id remains stable across renames, namespace changes and assembly moves,
/// allowing serialized data to survive code refactoring.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class TypeIdAttribute : Attribute
{
    /// <summary>
    /// Stable identifier for the annotated class.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Creates a TypeIdAttribute from a Guid string in any standard format.
    /// </summary>
    /// <param name="id">A canonical Guid string (e.g. "11111111-2222-3333-4444-555555555555").</param>
    public TypeIdAttribute(string id)
    {
        Id = Guid.Parse(id);
    }
}
