using System.Numerics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Guinevere;

public unsafe partial class GuiWindow
{
    /// <inheritdoc />
    public bool IsMaximized => WindowState == WindowState.Maximized;

    /// <inheritdoc />
    public bool CanMove => GLFW.GetPlatform() != global::OpenTK.Windowing.GraphicsLibraryFramework.Platform.Wayland;

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
