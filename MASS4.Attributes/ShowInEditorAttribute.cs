namespace MASS4.Attributes;

/// <summary>
/// Tells the studio to show this property in the editor, even it is not public.
/// </summary>
[AttributeUsage(AttributeTargets.Enum | AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ShowInEditorAttribute : Attribute;
