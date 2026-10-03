namespace Guinevere;

/// <summary>Optional client-area sizing for application-drawn window resize handles.</summary>
public interface IWindowResizeCapability : IPlatformCapability
{
    /// <summary>Whether the backend supports programmatic client-area resizing.</summary>
    bool CanResize { get; }

    /// <summary>The client-area size in logical desktop units, excluding native decorations.</summary>
    /// <remarks>Requests during GUI rendering must be applied after the renderer finishes using its canvas.</remarks>
    Vector2 ClientSize { get; set; }
}
