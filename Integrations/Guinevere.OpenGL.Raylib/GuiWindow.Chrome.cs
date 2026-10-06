using System.Numerics;
using Raylib_cs;

namespace Guinevere;

public partial class GuiWindow
{
    Vector2? _pendingClientSize;
    readonly DesktopPointer _desktopPointer = new();

    void PlaceInitialWindow()
    {
        if (!CanMove) return;
        var count = Raylib.GetMonitorCount();
        if (count <= 0) return;
        var areas = new Rect[count];
        for (var i = 0; i < count; i++)
        {
            var pos = Raylib.GetMonitorPosition(i);
            areas[i] = new Rect(pos.X, pos.Y, Raylib.GetMonitorWidth(i), Raylib.GetMonitorHeight(i));
        }
        var size = ClientSize;
        var posCur = Position;
        var selected = WindowPlacement.SelectMonitor(new Rect(posCur.X, posCur.Y, size.X, size.Y), areas);
        if (selected < 0) return;
        var placement = WindowPlacement.Center(size, areas[selected]);
        ClientSize = placement.Size;
        Position = placement.Position;
    }

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
    public unsafe Vector2 Position
    {
        get => Raylib.GetWindowPosition();
        set
        {
            if (_desktopPointer.TryMoveWindow((nuint)Raylib.GetWindowHandle(), value)) return;
            Raylib.SetWindowPosition((int)value.X, (int)value.Y);
        }
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
