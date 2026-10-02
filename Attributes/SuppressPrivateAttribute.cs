namespace MASS4.Attributes;

/// <summary>
/// Some attributes allow the user to view and modify the value of a private field form the editor.
/// This attribute will suppress that behavior, IF the field uses a attribute that uses this attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SuppressPrivateAttribute : Attribute;
