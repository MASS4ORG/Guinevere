using System.Numerics;
using Silk.NET.GLFW;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Guinevere;

public unsafe partial class GuiWindow
{
    Vector2? _pendingClientSize;

    void PlaceInitialWindow()
    {
        if (!CanMove)
        {
            return;
        }

        PlaceInitialWindow(GetMonitorWorkAreas());
    }

    Rect[] GetMonitorWorkAreas()
    {
        var monitors = _glfw.GetMonitors(out var count);
        var areas = new Rect[count];
        for (var i = 0; i < count; i++)
        {
            _glfw.GetMonitorWorkarea(monitors[i], out var x, out var y, out var width, out var height);
            areas[i] = new Rect(x, y, width, height);
        }
        return areas;
    }

    void PlaceInitialWindow(Rect[] areas)
    {
        var selected = WindowPlacement.SelectMonitor(
            new Rect(Position.X, Position.Y, ClientSize.X, ClientSize.Y), areas);
        if (selected < 0)
        {
            return;
        }

        var placement = WindowPlacement.Center(ClientSize, areas[selected]);
        ClientSize = new Vector2(placement.W, placement.H);
        Position = new Vector2(placement.X, placement.Y);
    }

    void ConfigureWindowPlatform()
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("SILKNET_USE_WAYLAND") != "0")
        {
            return;
        }
        // GLFW's platform hint and X11 value are absent from this binding's enums.
        const InitHint platformHint = (InitHint)0x00050003;
        const int x11Platform = 0x00060004;
        _glfw.InitHint(platformHint, x11Platform);
    }

    /// <inheritdoc />
    public bool IsMaximized => CurrentWindowState == WindowState.Maximized;

    /// <inheritdoc />
    public bool CanMove => _window.Native?.Wayland is null;

    /// <inheritdoc />
    public bool CanResize => true;

    /// <inheritdoc />
    public Vector2 ClientSize
    {
        get => new(_window.Size.X, _window.Size.Y);
        set
        {
            if (_gui.Canvas is not null)
            {
                _pendingClientSize = value;
            }
            else
            {
                _window.Size = new Vector2D<int>((int)value.X, (int)value.Y);
            }
        }
    }

    void ApplyPendingResize()
    {
        if (_pendingClientSize is not { } size)
        {
            return;
        }

        _pendingClientSize = null;
        ClientSize = size;
    }

    /// <inheritdoc />
    public Vector2 Position
    {
        get => new(_window.Position.X, _window.Position.Y);
        set
        {
            // The desktop hint alone is unreliable under XWayland, so the native move always runs too.
            TryMoveX11(value);
            _window.Position = new Vector2D<int>((int)value.X, (int)value.Y);
        }
    }

    (nint Display, nuint Window)? NativeX11 => _window.Native?.X11;

    bool TryMoveX11(Vector2 position)
    {
        var native = NativeX11;
        if (!native.HasValue)
        {
            return false;
        }

        return X11WindowPosition.TrySet(native.Value.Display, native.Value.Window, position);
    }

    /// <inheritdoc />
    public bool TryBeginMove()
    {
        if (!BeginNativeMove())
        {
            return false;
        }

        _nativeX11Move = !OperatingSystem.IsWindows();
        _heldButtons.Remove(Silk.NET.Input.MouseButton.Left);
        return true;
    }

    bool BeginNativeMove()
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsWindowMove.TryBegin(_window.Native?.Win32?.Hwnd ?? 0);
        }

        return BeginX11Move();
    }

    bool BeginX11Move()
    {
        if (NativeX11 is not { } x11)
        {
            return false;
        }

        return X11WindowPosition.TryBeginMove(x11.Display, x11.Window, PointerPosition);
    }

    /// <inheritdoc />
    public Vector2 PointerPosition => Position + _mouse.Position;

    /// <inheritdoc />
    public void Minimize()
    {
        if (_window.IsInitialized)
        {
            _glfw.IconifyWindow((WindowHandle*)_window.Handle);
        }
        else
        {
            _window.WindowState = WindowState.Minimized;
        }
    }

    /// <inheritdoc />
    public void Maximize()
    {
        if (_window.IsInitialized)
        {
            _glfw.MaximizeWindow((WindowHandle*)_window.Handle);
        }
        else
        {
            _window.WindowState = WindowState.Maximized;
        }
    }

    /// <inheritdoc />
    public void Restore()
    {
        if (_window.IsInitialized)
        {
            _glfw.RestoreWindow((WindowHandle*)_window.Handle);
        }
        else
        {
            _window.WindowState = WindowState.Normal;
        }
    }

    /// <inheritdoc />
    public void RequestClose()
    {
        if (_close.MayClose())
        {
            Close();
        }
    }
}
