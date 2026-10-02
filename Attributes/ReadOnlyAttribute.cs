namespace MASS4.Attributes;

/// <summary>
/// Make it read-only in the inspector
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ReadOnlyAttribute : Attribute { }
