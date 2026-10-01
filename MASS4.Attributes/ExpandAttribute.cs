namespace MASS4.Attributes;

/// <summary>
/// Set the initial expandaded state of the group in the inspector
/// </summary>
/// <remarks>
/// Set the initial expandaded state of the group in the inspector
/// </remarks>
/// <param name="defaultExpanded"></param>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Field,
    AllowMultiple = false,
    Inherited = true
)]
public sealed class ExpandAttribute(bool defaultExpanded = true) : Attribute
{
    /// <summary>
    /// The initial expandaded state of the group in the inspector
    /// </summary>
    public bool DefaultExpanded { get; set; } = defaultExpanded;
}
