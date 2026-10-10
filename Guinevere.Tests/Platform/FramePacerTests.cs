namespace Guinevere.Tests.Platform;

/// <summary>Checks when a windowed host waits or renders under each pacing mode.</summary>
public class FramePacerTests
{
    /// <summary>Pacing is on demand by default; continuous pacing never waits.</summary>
    [Fact]
    public void Continuous_NeverWaits()
    {
        Assert.True(new FramePacer().OnDemand);
        var pacer = new FramePacer { OnDemand = false };
        Assert.Equal(0, pacer.WaitSeconds(new Gui(), inputHeld: false));
        Assert.Throws<ArgumentNullException>(() => pacer.WaitSeconds(null!, inputHeld: false));
    }

    /// <summary>The run-time override switches pacing only for its two known values.</summary>
    [Theory]
    [InlineData("on-demand", false, true)]
    [InlineData("continuous", true, false)]
    [InlineData(null, true, true)]
    [InlineData("bogus", false, false)]
    public void ApplyOverride_SwitchesOnlyForKnownValues(string? value, bool before, bool after)
    {
        var pacer = new FramePacer { OnDemand = before };
        pacer.ApplyOverride(value);
        Assert.Equal(after, pacer.OnDemand);
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
