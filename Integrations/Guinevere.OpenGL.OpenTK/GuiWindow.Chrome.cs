using System.Numerics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Guinevere;

public partial class GuiWindow
{
    /// <inheritdoc />
    public bool IsMaximized => WindowState == WindowState.Maximized;

    /// <inheritdoc />
    public bool CanMove => GLFW.GetPlatform() != global::OpenTK.Windowing.GraphicsLibraryFramework.Platform.Wayland;

    /// <inheritdoc />
    public Vector2 Position
    {
        get => new(ClientLocation.X, ClientLocation.Y);
        set => ClientLocation = new((int)value.X, (int)value.Y);
    }

    /// <inheritdoc />
    public Vector2 PointerPosition => Position + MousePosition;

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
