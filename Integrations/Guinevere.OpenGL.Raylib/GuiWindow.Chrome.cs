using System.Numerics;
using Raylib_cs;

namespace Guinevere;

public partial class GuiWindow
{
    Vector2? _pendingClientSize;
    readonly DesktopPointer _desktopPointer = new();

    /// <inheritdoc />
    public bool IsMaximized => Raylib.IsWindowMaximized();

    /// <summary>The bundled desktop Raylib backends support window movement, including X11 in a Wayland session.</summary>
    public bool CanMove => true;

    /// <inheritdoc />
    public bool CanResize => true;

    /// <inheritdoc />
    public Vector2 ClientSize
    {
        get => new(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
        set
        {
            if (_gui.Canvas is not null) _pendingClientSize = value;
            else Raylib.SetWindowSize((int)value.X, (int)value.Y);
        }
    }

    void ApplyPendingResize()
    {
        if (_pendingClientSize is not { } size) return;
        _pendingClientSize = null;
        ClientSize = size;
    }

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
