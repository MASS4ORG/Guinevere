namespace MASS4.Attributes;

/// <summary>Adds previous and next buttons to an enum dropdown, cycling through declared values.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class EnumPagingAttribute : Attribute;
