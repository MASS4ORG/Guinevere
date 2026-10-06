using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("Guinevere.Tests")]

namespace Guinevere;

/// <summary>Requests window movement through the desktop protocol, including beyond reserved work areas.</summary>
static partial class X11WindowPosition
{
    internal static bool TrySet(nint display, nuint window, Vector2 position)
    {
        var atom = XInternAtom(display, "_NET_MOVERESIZE_WINDOW", 1);
        if (atom == 0) return false;
        var message = CreateMessage(display, window, atom, position);
        var result = XSendEvent(display, XDefaultRootWindow(display), 0, (1 << 19) | (1 << 20), ref message);
        XFlush(display);
        return result != 0;
    }

    internal static ClientMessage CreateMessage(nint display, nuint window, nuint atom, Vector2 position)
    {
        var message = new ClientMessage();
        message[0] = 33;
        message[2] = 1;
        message[3] = display;
        message[4] = (nint)window;
        message[5] = (nint)atom;
        message[6] = 32;
        message[7] = (1 << 12) | (1 << 8) | (1 << 9);
        message[8] = (int)position.X;
        message[9] = (int)position.Y;
        return message;
    }

    // XEvent occupies 24 native longs; the client-message fields share their ABI offsets on 32 and 64 bits.
    [InlineArray(24)]
    internal struct ClientMessage
    {
        nint _word;
    }

    [LibraryImport("libX11.so.6", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nuint XInternAtom(nint display, string name, int onlyIfExists);

    [LibraryImport("libX11.so.6")]
    private static partial nuint XDefaultRootWindow(nint display);

    [LibraryImport("libX11.so.6")]
    private static partial int XSendEvent(nint display, nuint window, int propagate, nint mask,
        ref ClientMessage message);

    [LibraryImport("libX11.so.6")]
    private static partial int XFlush(nint display);
}
