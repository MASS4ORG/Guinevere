namespace MASS4.Attributes;

/// <summary>Draws an enum field as a horizontal group of buttons, with independent toggles for flags.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class EnumButtonsAttribute : Attribute;
