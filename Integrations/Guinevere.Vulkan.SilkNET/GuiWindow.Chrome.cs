using System.Numerics;
using Silk.NET.GLFW;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Guinevere;

public unsafe partial class GuiWindow
{
    void ConfigureWindowPlatform()
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("SILKNET_USE_WAYLAND") != "0") return;
        // GLFW's platform hint and X11 value are absent from this binding's enums.
        const InitHint platformHint = (InitHint)0x00050003;
        const int x11Platform = 0x00060004;
        _glfw.InitHint(platformHint, x11Platform);
    }

    /// <inheritdoc />
    public bool IsMaximized => _window.WindowState == WindowState.Maximized;

    /// <inheritdoc />
    public bool CanMove => _window.Native?.Wayland is null;

    /// <inheritdoc />
    public Vector2 Position
    {
        get => new(_window.Position.X, _window.Position.Y);
        set => _window.Position = new Vector2D<int>((int)value.X, (int)value.Y);
    }

    /// <inheritdoc />
    public Vector2 PointerPosition => Position + _mouse.Position;

    /// <inheritdoc />
    public void Minimize()
    {
        if (_window.IsInitialized) _glfw.IconifyWindow((WindowHandle*)_window.Handle);
        else _window.WindowState = WindowState.Minimized;
    }

    /// <inheritdoc />
    public void Maximize()
    {
        if (_window.IsInitialized) _glfw.MaximizeWindow((WindowHandle*)_window.Handle);
        else _window.WindowState = WindowState.Maximized;
    }

    /// <inheritdoc />
    public void Restore()
    {
        if (_window.IsInitialized) _glfw.RestoreWindow((WindowHandle*)_window.Handle);
        else _window.WindowState = WindowState.Normal;
    }

    /// <inheritdoc />
    public void RequestClose()
    {
        if (_close.MayClose()) Close();
    }
}
