namespace MASS4.Attributes;

/// <summary>
/// Mark importers as the last option for importer
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DefaultOptionAttribute : Attribute;
