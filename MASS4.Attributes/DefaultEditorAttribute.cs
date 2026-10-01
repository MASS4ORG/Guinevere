namespace MASS4.Attributes;

/// <summary>
/// Tells the studio to use a custom editor for this type only in case there is no other editor for this type
/// </summary>
/// <seealso cref="CustomEditorAttribute"/>
[AttributeUsage(AttributeTargets.Class)]
public sealed class DefaultEditorAttribute : Attribute
{
    /// <summary>
    /// The contructor.
    /// </summary>
    public DefaultEditorAttribute() { }
}
