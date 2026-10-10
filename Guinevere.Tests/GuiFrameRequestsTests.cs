using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>Checks frame requests: pending state, timed requests, cross-thread wake-ups and automatic requests.</summary>
public class GuiFrameRequestsTests
{
    /// <summary>A request stays pending until the next frame starts, which serves it.</summary>
    [Fact]
    public void RequestFrame_IsServedByTheNextFrame()
    {
        using var harness = new FrameHarness();
        Assert.Equal(double.PositiveInfinity, harness.Gui.FrameWaitSeconds);

        harness.Gui.RequestFrame();
        Assert.Equal(0, harness.Gui.FrameWaitSeconds);

        harness.Frame(_ => { });
        Assert.Equal(double.PositiveInfinity, harness.Gui.FrameWaitSeconds);

        harness.Frame(gui => gui.RequestFrame());
        Assert.Equal(0, harness.Gui.FrameWaitSeconds);
    }

    /// <summary>
    /// The earliest timed request wins, survives frames until due, and is dropped once a frame passes it; later
    /// timers are re-requested by the controls that frame builds.
    /// </summary>
    [Fact]
    public void RequestFrameIn_KeepsTheEarliestDeadline()
    {
        using var harness = new FrameHarness();
        var gui = harness.Gui;

        gui.RequestFrameIn(10);
        Assert.InRange(gui.FrameWaitSeconds, 9, 10);
        gui.RequestFrameIn(1);
        gui.RequestFrameIn(5);
        Assert.InRange(gui.FrameWaitSeconds, 0.5, 1);

        harness.Frame(_ => { });
        Assert.InRange(gui.FrameWaitSeconds, 0.5, 1);

        gui.RequestFrameIn(0.001);
        Thread.Sleep(5);
        Assert.Equal(0, gui.FrameWaitSeconds);
        harness.Frame(_ => { });
        Assert.Equal(double.PositiveInfinity, gui.FrameWaitSeconds);

        gui.RequestFrameIn(0);
        Assert.Equal(0, gui.FrameWaitSeconds);
        gui.RequestFrameIn(double.NaN);
        Assert.Equal(0, gui.FrameWaitSeconds);
    }

    /// <summary>Requests from the frame thread do not wake the host; requests from other threads do.</summary>
    [Fact]
    public void Requests_FromOtherThreads_WakeTheHost()
    {
        using var harness = new FrameHarness();
        var wakes = 0;
        harness.Gui.WakeHost = () => Interlocked.Increment(ref wakes);
        harness.Frame(gui => gui.RequestFrame());
        harness.Gui.RequestFrameIn(1);
        Assert.Equal(0, wakes);

        OnOtherThread(() => harness.Gui.RequestFrame());
        OnOtherThread(() => harness.Gui.RequestFrameIn(0.5));
        OnOtherThread(() => harness.Gui.RequestFrameIn(2));
        Assert.Equal(2, wakes);
    }

    static void OnOtherThread(Action action)
    {
        var thread = new Thread(() => action());
        thread.Start();
        thread.Join();
    }

    /// <summary>Boolean animations request frames while moving and stop once settled.</summary>
    [Fact]
    public void Animations_RequestFramesUntilSettled()
    {
        using var harness = new FrameHarness();
        var target = 0f;
        void Draw(Gui gui) => gui.AnimateBool01(target > 0f, 0.1f, Easing.Linear);

        harness.Frame(Draw);
        Assert.Equal(double.PositiveInfinity, harness.Gui.FrameWaitSeconds);

        target = 10f;
        harness.Frame(Draw);
        Assert.Equal(0, harness.Gui.FrameWaitSeconds);

        for (var i = 0; i < 120; i++) harness.Frame(Draw);
        Assert.Equal(double.PositiveInfinity, harness.Gui.FrameWaitSeconds);
    }
}
