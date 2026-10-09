namespace Guinevere;

/// <summary>
/// Optional eyedropper: lets the user pick a color from the screen or the application window. Color pickers show
/// their eyedropper button only when an integration or application publishes this capability.
/// </summary>
public interface IColorSamplerCapability : IPlatformCapability
{
    /// <summary>Waits for the user to pick a color; <c>null</c> when they cancel.</summary>
    /// <param name="cancellationToken">Cancels the pick.</param>
    ValueTask<Color?> SampleAsync(CancellationToken cancellationToken = default);
}
