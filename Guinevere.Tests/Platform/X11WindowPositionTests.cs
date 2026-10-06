namespace Guinevere.Tests.Platform;

/// <summary>Checks the desktop move message's coordinates, source and native layout.</summary>
public sealed class X11WindowPositionTests
{
    /// <summary>Moves carry signed virtual-desktop coordinates without resizing or primary-monitor clamping.</summary>
    [Theory]
    [InlineData(-1920, -40)]
    [InlineData(1960, 976)]
    public void MoveMessagePreservesDesktopCoordinates(int x, int y)
    {
        var message = X11WindowPosition.CreateMessage(11, 22, 33, new Vector2(x, y));
        Assert.Equal((nint)33, message[0]);
        Assert.Equal((nint)1, message[2]);
        Assert.Equal((nint)11, message[3]);
        Assert.Equal((nint)22, message[4]);
        Assert.Equal((nint)33, message[5]);
        Assert.Equal((nint)32, message[6]);
        Assert.Equal((nint)0x1300, message[7]);
        Assert.Equal((nint)x, message[8]);
        Assert.Equal((nint)y, message[9]);
        for (var i = 10; i < 24; i++) Assert.Equal(nint.Zero, message[i]);
        Assert.Equal(24 * IntPtr.Size,
            System.Runtime.CompilerServices.Unsafe.SizeOf<X11WindowPosition.ClientMessage>());
    }
}
