namespace Guinevere;

/// <summary>An immutable postprocess operation, applied in stack order to a composited subtree.</summary>
public abstract record RenderEffect
{
    /// <summary>Blurs the source with a Gaussian standard deviation in screen pixels.</summary>
    public sealed record Blur(float Radius) : RenderEffect;

    /// <summary>Draws a colored blurred silhouette underneath the source.</summary>
    public sealed record Glow(float Radius, Color Color) : RenderEffect;

    /// <summary>Draws an offset blurred silhouette underneath the source.</summary>
    public sealed record DropShadow(float X, float Y, float Radius, Color Color) : RenderEffect;

    /// <summary>Scales color saturation; one preserves the source.</summary>
    public sealed record Saturation(float Amount) : RenderEffect;

    /// <summary>Scales RGB brightness; one preserves the source.</summary>
    public sealed record Brightness(float Amount) : RenderEffect;

    /// <summary>Scales contrast around middle gray; one preserves the source.</summary>
    public sealed record Contrast(float Amount) : RenderEffect;

    /// <summary>Blends RGB toward a color while preserving source alpha.</summary>
    public sealed record Tint(Color Color, float Amount = 1) : RenderEffect;

    /// <summary>Rotates hue by degrees.</summary>
    public sealed record HueRotation(float Degrees) : RenderEffect;

    /// <summary>Blends toward luminance gray; zero preserves the source.</summary>
    public sealed record Grayscale(float Amount = 1) : RenderEffect;

    /// <summary>Blends toward inverted RGB; zero preserves the source.</summary>
    public sealed record Invert(float Amount = 1) : RenderEffect;

    /// <summary>Samples the source in square blocks aligned to screen coordinates.</summary>
    public sealed record Pixelate(float Size) : RenderEffect;

    /// <summary>Offsets red and blue in opposite directions while retaining source alpha.</summary>
    public sealed record ChromaticAberration(float X, float Y = 0) : RenderEffect;

    /// <summary>Applies a four-neighbor sharpening kernel.</summary>
    public sealed record Sharpen(float Amount = 1) : RenderEffect;

    /// <summary>Compiles a SkSL color filter when the integration permits runtime compilation.</summary>
    public sealed record RuntimeColorFilter(string Source) : RenderEffect;

    /// <summary>Uses a caller-owned compiled color filter; keep it alive until the stack is disposed.</summary>
    public sealed record CompiledColorFilter(SKColorFilter Filter) : RenderEffect;
}
