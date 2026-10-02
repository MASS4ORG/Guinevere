namespace MASS4.Attributes;

/// <summary>
/// Shows an error under the field while its value is missing: null, an empty string or an empty collection. Only
/// a message; it never blocks a write.
/// </summary>
/// <param name="message">The error text; null for a default naming the field.</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class RequiredAttribute(string? message = null) : Attribute
{
    /// <summary>The error text, or null for the default.</summary>
    public string? Message { get; } = message;
}
