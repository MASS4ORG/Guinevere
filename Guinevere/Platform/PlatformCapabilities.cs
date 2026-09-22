namespace Guinevere;

/// <summary>Typed feature discovery for services supplied by the active platform integration.</summary>
public sealed class PlatformCapabilities
{
    readonly Dictionary<Type, IPlatformCapability> _services = [];

    /// <summary>The capability contracts currently published by the integration.</summary>
    public IReadOnlyCollection<Type> Available => _services.Keys;

    /// <summary>Publishes or replaces a capability implementation.</summary>
    public PlatformCapabilities Register<TCapability>(TCapability capability)
        where TCapability : class, IPlatformCapability
    {
        ArgumentNullException.ThrowIfNull(capability);
        _services[typeof(TCapability)] = capability;
        return this;
    }

    /// <summary>Returns whether a capability is available.</summary>
    public bool Supports<TCapability>() where TCapability : class, IPlatformCapability =>
        _services.ContainsKey(typeof(TCapability));

    /// <summary>Attempts to retrieve an optional capability.</summary>
    public bool TryGet<TCapability>(out TCapability? capability)
        where TCapability : class, IPlatformCapability
    {
        if (_services.TryGetValue(typeof(TCapability), out var service))
        {
            capability = (TCapability)service;
            return true;
        }

        capability = null;
        return false;
    }

    /// <summary>Retrieves a required capability or throws an explicit diagnostic.</summary>
    public TCapability Require<TCapability>() where TCapability : class, IPlatformCapability =>
        TryGet<TCapability>(out var capability)
            ? capability!
            : throw new PlatformCapabilityException(typeof(TCapability));

    /// <summary>Removes a capability, usually while an integration is shutting down.</summary>
    public bool Remove<TCapability>() where TCapability : class, IPlatformCapability =>
        _services.Remove(typeof(TCapability));
}
