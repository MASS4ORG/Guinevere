namespace Guinevere;

/// <summary>Marker for a service supplied by a Guinevere host or integration.</summary>
public interface IPlatformCapability;

/// <summary>Thrown when an application requires a capability its integration did not publish.</summary>
public sealed class PlatformCapabilityException(Type capabilityType)
    : InvalidOperationException($"The platform does not provide {capabilityType.Name}.")
{
    /// <summary>The missing capability contract.</summary>
    public Type CapabilityType { get; } = capabilityType;
}
