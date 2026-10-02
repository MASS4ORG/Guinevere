namespace MASS4.Attributes;

/// <summary>Requests a numeric editor with increment and decrement controls.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class NumericUpDownAttribute : Attribute { }
