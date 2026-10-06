namespace Guinevere;

/// <summary>Optional native window identity shown in taskbars and application switchers.</summary>
public interface IWindowIdentityCapability : IPlatformCapability
{
    /// <summary>The title displayed by the operating system.</summary>
    string Title { get; set; }

    /// <summary>Sets an application icon from unpremultiplied RGBA pixels.</summary>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="rgbaPixels">Four channels per pixel, in row order.</param>
    void SetIcon(int width, int height, ReadOnlySpan<byte> rgbaPixels);
}
