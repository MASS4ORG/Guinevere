#pragma warning disable CS1591 // Contract members are named and documented by their containing capability.
namespace Guinevere;

/// <summary>Monotonic frame timing supplied to animation and interaction code.</summary>
public interface ITimeCapability : IPlatformCapability
{
    float DeltaTime { get; }
    float Elapsed { get; }
    ulong Frames { get; }
}

/// <summary>Pointer cursor shapes understood across desktop integrations.</summary>
public enum PointerCursor
{
    Default,
    Arrow,
    Text,
    Hand,
    Crosshair,
    ResizeHorizontal,
    ResizeVertical,
    ResizeDiagonalNorthWestSouthEast,
    ResizeDiagonalNorthEastSouthWest,
    NotAllowed,
    Hidden
}

/// <summary>Optional native cursor control.</summary>
public interface ICursorCapability : IPlatformCapability
{
    PointerCursor Cursor { get; set; }
}

/// <summary>Optional display scale and framebuffer information.</summary>
public interface IDisplayCapability : IPlatformCapability
{
    float ScaleFactor { get; }
    Vector2 LogicalSize { get; }
    Vector2 FramebufferSize { get; }
}

/// <summary>Native file-picker kind.</summary>
public enum PlatformFileDialogKind { OpenFile, SaveFile, SelectFolder }

/// <summary>A native file-picker request.</summary>
public sealed record PlatformFileDialogRequest(
    PlatformFileDialogKind Kind,
    string? Title = null,
    string? InitialPath = null,
    IReadOnlyList<string>? Extensions = null,
    bool AllowMultiple = false);

/// <summary>Optional native file-dialog service.</summary>
public interface IPlatformFileDialogCapability : IPlatformCapability
{
    ValueTask<IReadOnlyList<string>> ShowAsync(PlatformFileDialogRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Opaque platform texture handle.</summary>
public readonly record struct PlatformTexture(nint Handle, int Width, int Height);

/// <summary>Optional platform-owned texture lifecycle.</summary>
public interface ITextureCapability : IPlatformCapability
{
    PlatformTexture Create(int width, int height, ReadOnlySpan<byte> rgbaPixels);
    void Update(PlatformTexture texture, ReadOnlySpan<byte> rgbaPixels);
    void Destroy(PlatformTexture texture);
}

/// <summary>Optional GPU effect support advertised by a renderer.</summary>
public interface IGpuEffectsCapability : IPlatformCapability
{
    bool Supports(string effect);
}

/// <summary>Optional accessibility bridge for a complete frame's semantic controls.</summary>
public interface IAccessibilityCapability : IPlatformCapability, IControlSemanticsSink
{
    void BeginFrame();
    void EndFrame();
}
