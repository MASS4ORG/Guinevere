namespace Guinevere.Tests.Platform;

public class GameInputTests
{
    [Fact]
    public void PollingCopiesNativeSamplesAndWakesOnlyAfterInput()
    {
        var state = new GameInputState();
        GameInputSample? sample = null;
        var polls = 0;
        var waits = new List<double>();
        GameInputState.WaitForInput(() => state.Changed ? 0 : 1, delay =>
        {
            waits.Add(delay);
            sample = new GameInputSample("pad", "Pad", true, GameInputState.CopyButtons([1, 0]),
                [0, 0], GameInputState.CopyHats([1, 3]));
        }, () =>
        {
            polls++;
            state.Poll(slot => slot == 0 ? sample : null, 2, 0);
        });
        Assert.Equal(1, polls);
        Assert.Equal(0.008, Assert.Single(waits));
        Assert.True(Assert.Single(state.Devices).Buttons[0].Pressed);
        Assert.Equal(new Vector2(1, -1), state.Devices[0].Hats[1]);
        sample!.Buttons[0] = false;
        Assert.True(state.Devices[0].Buttons[0].Down);
        state.NewFrame();
        state.Poll(_ => null, 2, 1);
        Assert.Empty(state.Devices);
        Assert.Equal(new GameDeviceConnection("pad", false), Assert.Single(state.Connections));
        var touch = new TouchInputState();
        var contact = new TouchContact(1, Vector2.Zero, null, TouchPhase.Stationary);
        touch.SynchronizeContacts([contact]);
        Assert.Equal(TouchPhase.Began, Assert.Single(touch.Contacts).Phase);
        Assert.True(touch.HasActiveContacts);
        touch.NewFrame();
        touch.SynchronizeContacts([contact]);
        Assert.False(touch.Changed);
        touch.SynchronizeContacts([contact with { Position = Vector2.One, Pressure = 0.5f }]);
        Assert.Equal(TouchPhase.Moved, Assert.Single(touch.Contacts).Phase);
        touch.NewFrame();
        touch.SynchronizeContacts([]);
        Assert.Equal(TouchPhase.Ended, Assert.Single(touch.Contacts).Phase);
        Assert.False(touch.HasActiveContacts);
    }

    [Fact]
    public void DevicesRetainEdgesUntilRenderedAndDisconnectIndependently()
    {
        var input = new GameInputState();
        input.BeginUpdate();
        input.UpdateDevice("one", "One", true, [true, false], [0.1f, 0.6f], [Vector2.UnitX], 1);
        input.UpdateDevice("two", "Two", false, [false], [], [], 1);
        input.EndUpdate();
        Assert.Equal(2, input.Connections.Count);
        Assert.True(input.AnyButtonDown);
        Assert.True(input.AnyStickDown);
        Assert.Equal(0, input.Devices[0].Axis(0));
        Assert.Equal(0.5f, input.Devices[0].Axis(1), 4);
        Assert.Equal(default, input.Devices[1].Button(GameButton.South));
        Assert.Equal(0, input.Devices[0].Axis(5));
        input.UpdateDevice("one", "One", true, [false, false], [float.NaN, 4], [], 1.1);
        Assert.True(input.Devices[0].Button(GameButton.South).Pressed);
        Assert.True(input.Devices[0].Button(GameButton.South).Released);
        Assert.False(input.AnyButtonDown);
        Assert.Equal(0, input.Devices[0].Axes[0]);
        Assert.Equal(1, input.Devices[0].Axes[1]);
        input.NewFrame();
        Assert.Empty(input.Connections);
        Assert.False(input.Changed);
        Assert.False(input.Devices[0].Button(GameButton.South).Pressed);
        input.BeginUpdate();
        input.UpdateDevice("two", "Two", false, [true], [], [], 2);
        input.EndUpdate();
        Assert.Single(input.Devices);
        Assert.Equal(new GameDeviceConnection("one", false), Assert.Single(input.Connections));
        input.Disconnect("missing");
        input.NewFrame();
        input.UpdateDevice("two", "Two", false, [true], [], [], 2.75);
        Assert.Equal(0.75, input.Devices[0].Buttons[0].HeldSeconds);
        Assert.False(input.Devices[0].Buttons[0].Pressed);
        input.Disconnect("two");
        Assert.Empty(input.Devices);
    }

    [Theory]
    [InlineData(1, 0, -1)]
    [InlineData(2, 1, 0)]
    [InlineData(4, 0, 1)]
    [InlineData(8, -1, 0)]
    [InlineData(3, 1, -1)]
    [InlineData(0, 0, 0)]
    public void HatsUseWindowCoordinates(byte mask, float x, float y) =>
        Assert.Equal(new Vector2(x, y), GameInputState.DecodeHat(mask));

    [Fact]
    public void InvalidDeadZonesAndAxisIndicesAreRejected()
    {
        var input = new GameInputState();
        input.UpdateDevice("pad", "Pad", true, [], [-0.6f], [], 0);
        var pad = input.Devices[0];
        Assert.Equal(-0.5f, pad.Axis(0), 4);
        Assert.Throws<ArgumentOutOfRangeException>(() => pad.Axis(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => pad.Axis(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => pad.Axis(0, float.NaN));
    }
}
