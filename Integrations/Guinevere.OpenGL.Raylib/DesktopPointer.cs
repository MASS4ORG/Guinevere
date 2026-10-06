using System.Numerics;
using System.Runtime.InteropServices;

namespace Guinevere;

/// <summary>Polls desktop pointer coordinates without Raylib's cached window-relative input.</summary>
sealed partial class DesktopPointer : IDisposable
{
    nint _display;

    internal Vector2 Position => OperatingSystem.IsWindows() ? WindowsPosition() : UnixPosition();

    internal bool TryMoveWindow(nuint window, Vector2 position)
    {
        if (!OperatingSystem.IsLinux()) return false;
        OpenDisplay();
        return X11WindowPosition.TrySet(_display, window, position);
    }

    static Vector2 WindowsPosition()
    {
        if (!GetCursorPos(out var point)) throw new InvalidOperationException("Cannot query desktop pointer.");
        return new Vector2(point.X, point.Y);
    }

    Vector2 UnixPosition()
    {
        if (OperatingSystem.IsLinux()) return LinuxPosition();
        if (OperatingSystem.IsMacOS()) return MacPosition();
        throw new PlatformNotSupportedException("Desktop pointer polling is unavailable on this platform.");
    }

    void OpenDisplay()
    {
        if (_display != 0) return;
        _display = XOpenDisplay(0);
        if (_display == 0) throw new InvalidOperationException("Cannot open the X11 desktop display.");
    }

    Vector2 LinuxPosition()
    {
        OpenDisplay();
        if (XQueryPointer(_display, XDefaultRootWindow(_display), out _, out _, out var x, out var y,
                out _, out _, out _) == 0)
            throw new InvalidOperationException("Cannot query the X11 desktop pointer.");
        return new Vector2(x, y);
    }

    static Vector2 MacPosition()
    {
        var nativeEvent = CGEventCreate(0);
        if (nativeEvent == 0) throw new InvalidOperationException("Cannot query the macOS desktop pointer.");
        try
        {
            var point = CGEventGetLocation(nativeEvent);
            return new Vector2((float)point.X, (float)point.Y);
        }
        finally
        {
            CFRelease(nativeEvent);
        }
    }

    /// <summary>Closes the lazily opened desktop display connection.</summary>
    public void Dispose()
    {
        if (_display == 0) return;
        XCloseDisplay(_display);
        _display = 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct NativePoint(int X, int Y);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct NativePointD(double X, double Y);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out NativePoint point);

    [LibraryImport("libX11.so.6")]
    private static partial nint XOpenDisplay(nint name);

    [LibraryImport("libX11.so.6")]
    private static partial nuint XDefaultRootWindow(nint display);

    [LibraryImport("libX11.so.6")]
    private static partial int XQueryPointer(nint display, nuint window, out nuint root, out nuint child,
        out int rootX, out int rootY, out int windowX, out int windowY, out uint mask);

    [LibraryImport("libX11.so.6")]
    private static partial int XCloseDisplay(nint display);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial nint CGEventCreate(nint source);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial NativePointD CGEventGetLocation(nint nativeEvent);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial void CFRelease(nint value);
}
