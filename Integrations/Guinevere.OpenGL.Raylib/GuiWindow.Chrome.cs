using System.Numerics;
using Raylib_cs;

namespace Guinevere;

public partial class GuiWindow
{
    /// <inheritdoc />
    public bool IsMaximized => Raylib.IsWindowMaximized();

    /// <inheritdoc />
    public bool CanMove => !OperatingSystem.IsLinux()
        || Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") != "wayland";

    /// <inheritdoc />
    public Vector2 Position
    {
        get => Raylib.GetWindowPosition();
        set => Raylib.SetWindowPosition((int)value.X, (int)value.Y);
    }

    /// <inheritdoc />
    public Vector2 PointerPosition => Position + Raylib.GetMousePosition();

    /// <inheritdoc />
    public void Minimize() => Raylib.MinimizeWindow();

    /// <inheritdoc />
    public void Maximize() => Raylib.MaximizeWindow();

    /// <inheritdoc />
    public void Restore() => Raylib.RestoreWindow();

    /// <inheritdoc />
    public void RequestClose()
    {
        if (_close.MayClose()) Close();
    }
}
