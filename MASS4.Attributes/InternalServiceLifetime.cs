namespace MASS4.Attributes;

/// <summary>
/// Specifies the service lifetime for a class in the DI container.
/// </summary>
public enum InternalServiceLifetime
{
    /// <summary>
    /// Specifies that a new instance of the service will be created each time it is requested.
    /// Like, each panel have a service underneath that will be created multiple times if 
    /// necessary.
    /// </summary>
    Transient,

    /// <summary>
    /// Specifies that a single instance of the service will be created and shared among all requests.
    /// </summary>
    Singleton
}
