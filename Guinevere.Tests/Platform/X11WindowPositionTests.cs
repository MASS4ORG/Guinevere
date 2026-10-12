namespace Guinevere.Tests.Platform;

/// <summary>Checks the desktop move message's coordinates, source and native layout.</summary>
public sealed class X11WindowPositionTests
{
    /// <summary>A release received before the desktop grab cancels the pending interactive move.</summary>
    [Fact]
    public void CancelMoveUsesWindowManagerCancellationProtocol()
    {
        var message = X11WindowPosition.CreateCancelMoveMessage(11, 22, 33);
        Assert.Equal(22, message[4]);
        Assert.Equal(33, message[5]);
        Assert.Equal(32, message[6]);
        Assert.Equal(0, message[7]);
        Assert.Equal(0, message[8]);
        Assert.Equal(11, message[9]);
        Assert.Equal(1, message[10]);
        Assert.Equal(1, message[11]);
    }

    /// <summary>Interactive moves identify the root pointer, left button and application source.</summary>
    [Theory]
    [InlineData(-1920, -40)]
    [InlineData(1960, 976)]
    public void InteractiveMoveUsesWindowManagerDragProtocol(int x, int y)
    {
        var message = X11WindowPosition.CreateMoveMessage(11, 22, 33, new Vector2(x, y));
        Assert.Equal(33, message[0]);
        Assert.Equal(1, message[2]);
        Assert.Equal(11, message[3]);
        Assert.Equal(22, message[4]);
        Assert.Equal(33, message[5]);
        Assert.Equal(32, message[6]);
        Assert.Equal(x, message[7]);
        Assert.Equal(y, message[8]);
        Assert.Equal(8, message[9]);
        Assert.Equal(1, message[10]);
        Assert.Equal(1, message[11]);
        for (var i = 12; i < 24; i++) Assert.Equal(nint.Zero, message[i]);
    }

    /// <summary>Moves carry signed virtual-desktop coordinates without resizing or primary-monitor clamping.</summary>
    [Theory]
    [InlineData(-1920, -40)]
    [InlineData(1960, 976)]
    public void MoveMessagePreservesDesktopCoordinates(int x, int y)
    {
        var message = X11WindowPosition.CreateMessage(11, 22, 33, new Vector2(x, y));
        Assert.Equal(33, message[0]);
        Assert.Equal(1, message[2]);
        Assert.Equal(11, message[3]);
        Assert.Equal(22, message[4]);
        Assert.Equal(33, message[5]);
        Assert.Equal(32, message[6]);
        Assert.Equal(0x1300, message[7]);
        Assert.Equal(x, message[8]);
        Assert.Equal(y, message[9]);
        for (var i = 10; i < 24; i++) Assert.Equal(nint.Zero, message[i]);
        Assert.Equal(24 * IntPtr.Size,
            System.Runtime.CompilerServices.Unsafe.SizeOf<X11WindowPosition.ClientMessage>());
    }
}
