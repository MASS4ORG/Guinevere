namespace MASS4.Attributes;

/// <summary>Shows explanatory text when an inspector field is hovered.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true)]
public sealed class TooltipAttribute(string text) : Attribute
{
    /// <summary>The text displayed on hover.</summary>
    public string Text { get; } = text;
}
