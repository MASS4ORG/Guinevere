using System.Numerics;
using Raylib_cs;

namespace Guinevere;

public partial class GuiWindow
{
    readonly DesktopPointer _desktopPointer = new();

    /// <inheritdoc />
    public bool IsMaximized => Raylib.IsWindowMaximized();

    /// <summary>The bundled desktop Raylib backends support window movement, including X11 in a Wayland session.</summary>
    public bool CanMove => true;

    /// <inheritdoc />
    public Vector2 Position
    {
        get => Raylib.GetWindowPosition();
        set => Raylib.SetWindowPosition((int)value.X, (int)value.Y);
    }

    /// <inheritdoc />
    public Vector2 PointerPosition => _desktopPointer.Position;

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
