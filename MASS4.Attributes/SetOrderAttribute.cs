namespace MASS4.Attributes;

/// <summary>Orders an inspector member before or after members with the default order (zero).</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
public sealed class SetOrderAttribute(int priority) : Attribute
{
    /// <summary>Lower priorities appear first; equal priorities retain reflection order.</summary>
    public int Priority { get; } = priority;
}
