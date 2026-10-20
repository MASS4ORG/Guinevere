namespace Guinevere;

/// <summary>Effect support shared by Skia raster, OpenGL and Vulkan canvases.</summary>
public sealed class SkiaEffectsCapability : IGpuEffectsCapability
{
    /// <summary>The shared Skia effect contract.</summary>
    public static SkiaEffectsCapability Instance { get; } = new();

    /// <inheritdoc />
    public bool SupportsRenderTargets => true;

    /// <inheritdoc />
    public bool SupportsRuntimeShaderCompilation => true;

    /// <inheritdoc />
    public bool Supports(string effect) => effect is "render-target" or "color-filter" or "runtime-color-filter";
}
