using System.Numerics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Guinevere;

public unsafe partial class GuiWindow
{
    Vector2? _pendingClientSize;

    /// <inheritdoc />
    public bool IsMaximized => WindowState == WindowState.Maximized;

    /// <inheritdoc />
    public bool CanMove => GLFW.GetPlatform() != global::OpenTK.Windowing.GraphicsLibraryFramework.Platform.Wayland;

    /// <inheritdoc />
    public bool CanResize => true;

    /// <inheritdoc />
    Vector2 IWindowResizeCapability.ClientSize
    {
        get
        {
            GLFW.GetWindowSize(WindowPtr, out var width, out var height);
            return new Vector2(width, height);
        }
        set
        {
            if (_gui.Canvas is not null) _pendingClientSize = value;
            else GLFW.SetWindowSize(WindowPtr, (int)value.X, (int)value.Y);
        }
    }

    void ApplyPendingResize()
    {
        if (_pendingClientSize is not { } size) return;
        _pendingClientSize = null;
        ((IWindowResizeCapability)this).ClientSize = size;
    }

    /// <inheritdoc />
    public Vector2 Position
    {
        get
        {
            GLFW.GetWindowPos(WindowPtr, out var x, out var y);
            return new Vector2(x, y);
        }
        set => GLFW.SetWindowPos(WindowPtr, (int)value.X, (int)value.Y);
    }

    /// <inheritdoc />
    public Vector2 PointerPosition
    {
        get
        {
            GLFW.GetCursorPos(WindowPtr, out var x, out var y);
            return Position + new Vector2((float)x, (float)y);
        }
    }

    /// <inheritdoc />
    public void Minimize() => WindowState = WindowState.Minimized;

    /// <inheritdoc />
    public void Maximize() => WindowState = WindowState.Maximized;

    /// <inheritdoc />
    public void Restore() => WindowState = WindowState.Normal;

    /// <inheritdoc />
    public void RequestClose()
    {
        if (_close.MayClose()) Close();
    }
}
