namespace MASS4.Attributes;

/// <summary>
/// Attribute to annotate services for automatic registration in the Dependency Injection
/// system (also called Inversion of Control).
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="InternalServiceAttribute"/> class.
/// </remarks>
/// <param name="lifetime">The service lifetime. Defaults to <see cref="InternalServiceLifetime.Transient"/>.</param>
/// <param name="serviceType">The type of the service to be registered. If null, the decorated class type will be used.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class InternalServiceAttribute(InternalServiceLifetime lifetime = InternalServiceLifetime.Transient, Type? serviceType = null) : Attribute
{
    /// <summary>
    /// Gets the service lifetime (e.g., Transient or Singleton).
    /// </summary>
    public InternalServiceLifetime Lifetime { get; } = lifetime;

    /// <summary>
    /// Gets the type of the service to be registered. If null, the decorated class type will be used.
    /// </summary>
    public Type? ServiceType { get; } = serviceType;
}
