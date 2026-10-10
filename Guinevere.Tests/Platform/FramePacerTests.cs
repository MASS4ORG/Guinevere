namespace Guinevere.Tests.Platform;

/// <summary>Checks when a windowed host waits or renders under each pacing mode.</summary>
public class FramePacerTests
{
    /// <summary>Continuous pacing never waits.</summary>
    [Fact]
    public void Continuous_NeverWaits()
    {
        var pacer = new FramePacer();
        Assert.Equal(0, pacer.WaitSeconds(new Gui(), inputHeld: false));
        Assert.Throws<ArgumentNullException>(() => pacer.WaitSeconds(null!, inputHeld: false));
    }

    /// <summary>On demand waits up to the idle limit, and not at all while input is held or a frame is requested.</summary>
    [Fact]
    public void OnDemand_WaitsUntilSomethingNeedsAFrame()
    {
        var gui = new Gui();
        var pacer = new FramePacer { OnDemand = true, MaxIdleSeconds = 10 };
        pacer.FrameRendered();
        Assert.InRange(pacer.WaitSeconds(gui, inputHeld: false), 9, 10);
        Assert.Equal(0, pacer.WaitSeconds(gui, inputHeld: true));

        gui.RequestFrameIn(0.5);
        Assert.InRange(pacer.WaitSeconds(gui, inputHeld: false), 0.1, 0.5);
        gui.RequestFrame();
        Assert.Equal(0, pacer.WaitSeconds(gui, inputHeld: false));
    }

    /// <summary>Input renders the settle frames, then pacing waits again; the idle limit counts from the last frame.</summary>
    [Fact]
    public void OnDemand_RendersSettleFramesAfterInput()
    {
        var gui = new Gui();
        var pacer = new FramePacer { OnDemand = true, SettleFrames = 2, MaxIdleSeconds = 10 };
        pacer.NotifyInput();
        Assert.Equal(0, pacer.WaitSeconds(gui, inputHeld: false));
        pacer.FrameRendered();
        Assert.Equal(0, pacer.WaitSeconds(gui, inputHeld: false));
        pacer.FrameRendered();
        Assert.True(pacer.WaitSeconds(gui, inputHeld: false) > 9);
        pacer.FrameRendered();
        Assert.True(pacer.WaitSeconds(gui, inputHeld: false) > 9);

        pacer.MaxIdleSeconds = 0.001;
        Thread.Sleep(5);
        Assert.Equal(0, pacer.WaitSeconds(gui, inputHeld: false));
    }
}
