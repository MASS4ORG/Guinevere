namespace MASS4.Attributes;

/// <summary>Draws the field's editor across the whole row, without its label.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class HideLabelAttribute : Attribute;
