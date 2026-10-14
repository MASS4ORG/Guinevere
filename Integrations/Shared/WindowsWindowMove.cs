using System.Runtime.InteropServices;

namespace Guinevere;

/// <summary>Transfers a client-area drag to the Windows interactive window move loop.</summary>
static partial class WindowsWindowMove
{
    internal static bool TryBegin(nint window)
    {
        if (!OperatingSystem.IsWindows() || window == 0)
        {
            return false;
        }

        ReleaseCapture();
        // GLFW must see a release because the system move loop consumes the physical mouse-up event.
        SendMessageW(window, 0x0202, 0, 0);
        // Posting lets the current render finish before Windows enters its modal move loop.
        return PostMessageW(window, 0x0112, 0xF012, 0);
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ReleaseCapture();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessageW(nint window, uint message, nuint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    private static partial nint SendMessageW(nint window, uint message, nuint wParam, nint lParam);
}
