namespace MASS4.Attributes;

/// <summary>
/// Tells the studio to use a custom editor for this type.
/// </summary>
/// <remarks>
///
/// </remarks>
/// <param name="editorType"></param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class CustomEditorAttribute(Type editorType) : Attribute
{
    /// <summary>
    /// The type that a given property editor deals with.
    /// </summary>
    public Type EditorType { get; } = editorType;
}
