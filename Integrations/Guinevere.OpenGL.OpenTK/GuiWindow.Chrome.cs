using System.Numerics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Guinevere;

public unsafe partial class GuiWindow
{
    Vector2? _pendingClientSize;
    bool _nativeMove;
    bool _nativeMovePress;
    bool _nativeX11Move;

    void PlaceInitialWindow()
    {
        if (!CanMove) return;
        PlaceInitialWindow(GetMonitorWorkAreas());
    }

    Rect[] GetMonitorWorkAreas()
    {
        var monitors = GLFW.GetMonitorsRaw(out var count);
        var areas = new Rect[count];
        for (var i = 0; i < count; i++)
        {
            GLFW.GetMonitorWorkarea(monitors[i], out var x, out var y, out var width, out var height);
            areas[i] = new Rect(x, y, width, height);
        }
        return areas;
    }

    void PlaceInitialWindow(Rect[] areas)
    {
        var size = ((IWindowResizeCapability)this).ClientSize;
        var selected = WindowPlacement.SelectMonitor(new Rect(Position.X, Position.Y, size.X, size.Y), areas);
        if (selected < 0) return;
        var placement = WindowPlacement.Center(size, areas[selected]);
        ((IWindowResizeCapability)this).ClientSize = placement.Size;
        Position = placement.Position;
    }

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
        set
        {
            // The desktop hint alone is unreliable under XWayland, so the native move always runs too.
            TryMoveX11(value);
            GLFW.SetWindowPos(WindowPtr, (int)value.X, (int)value.Y);
        }
    }

    bool TryMoveX11(Vector2 position)
    {
        if (!OperatingSystem.IsLinux()) return false;
        return TryMoveOnX11(position);
    }

    bool TryMoveOnX11(Vector2 position)
    {
        if (!CanMove) return false;
        return X11WindowPosition.TrySet(GLFW.GetX11Display(), GLFW.GetX11Window(WindowPtr), position);
    }

    /// <inheritdoc />
    public bool TryBeginMove()
    {
        if (!BeginNativeMove()) return false;
        _nativeMove = true;
        _nativeX11Move = OperatingSystem.IsLinux();
        return true;
    }

    bool BeginNativeMove()
    {
        if (OperatingSystem.IsWindows()) return WindowsWindowMove.TryBegin(GLFW.GetWin32Window(WindowPtr));
        return BeginLinuxMove();
    }

    bool BeginLinuxMove()
    {
        if (!OperatingSystem.IsLinux()) return false;
        return BeginX11Move();
    }

    bool BeginX11Move()
    {
        if (!CanMove) return false;
        return X11WindowPosition.TryBeginMove(GLFW.GetX11Display(), GLFW.GetX11Window(WindowPtr), PointerPosition);
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
