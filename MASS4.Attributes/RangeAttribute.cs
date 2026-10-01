namespace MASS4.Attributes;

/// <summary>
/// Limits the range of a numeric property
/// </summary>
/// <remarks>
/// Limits the range of a numeric property
/// </remarks>
/// <param name="min"></param>
/// <param name="max"></param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true)]
public sealed class RangeAttribute(float min, float max) : Attribute
{
    /// <summary>
    /// Minimum value possible
    /// </summary>
    public float Min { get; init; } = min;

    /// <summary>
    /// Maximum value possible
    /// </summary>
    public float Max { get; init; } = max;
}
