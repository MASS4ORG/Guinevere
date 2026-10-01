namespace MASS4.Attributes;

/// <summary>
/// Tells the studio to hide this member from the editor, even when it is public. Unsealed so a consumer
/// attribute that always implies hiding (an injected service, say) can derive from it.
/// </summary>
[AttributeUsage(AttributeTargets.Enum | AttributeTargets.Field | AttributeTargets.Property)]
public class HideInEditorAttribute : Attribute;
