using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Animation;

/// <summary>Checks keyed foreground and ghost timing under repeated damage, recovery and idle frames.</summary>
public class GuiAnimatedRangeTests
{
    /// <summary>The ghost holds after a decrease, then catches up in host units using the supplied easing.</summary>
    [Fact]
    public void GhostHoldsThenCatchesUpInRawUnits()
    {
        using var harness = new FrameHarness();
        var target = 100f;
        var samples = new List<AnimatedRangeValues>();
        void Draw(Gui gui) => samples.Add(gui.AnimatedRange(target, lagDelay: 0.25f,
            lagDuration: 0.5f, easing: Easing.Linear));
        harness.Frame(Draw, 0);
        Assert.Equal(new AnimatedRangeValues(100, 100), samples[^1]);
        target = 60;
        harness.Frame(Draw, 0);
        Assert.Equal(new AnimatedRangeValues(60, 100), samples[^1]);
        Assert.InRange(harness.Gui.FrameWaitSeconds, 0.01, 0.25);
        harness.Frame(Draw, 0.2);
        Assert.Equal(100, samples[^1].Ghost);
        harness.Frame(Draw, 0.15);
        Assert.Equal(samples[^1], samples[^2]);
        Assert.InRange(samples[^1].Ghost, 91.99f, 92.01f);
        Assert.Equal(0, harness.Gui.FrameWaitSeconds);
        harness.Frame(Draw, 0.4);
        Assert.Equal(new AnimatedRangeValues(60, 60), samples[^1]);
    }

    /// <summary>Another decrease restarts the hold without discarding the existing trail.</summary>
    [Fact]
    public void RepeatedDamageRestartsHold()
    {
        using var harness = new FrameHarness();
        var target = 100f;
        AnimatedRangeValues values = default;
        void Draw(Gui gui) => values = gui.AnimatedRange(target, lagDelay: 0.25f,
            lagDuration: 0.5f, easing: Easing.Linear, id: "health");
        harness.Frame(Draw, 0);
        target = 80;
        harness.Frame(Draw, 0);
        target = 60;
        harness.Frame(Draw, 0.2);
        harness.Frame(Draw, 0.2);
        Assert.Equal(100, values.Ghost);
        harness.Frame(Draw, 0.15);
        Assert.InRange(values.Ghost, 91.99f, 92.01f);
    }

    /// <summary>New damage after an idle wait starts its trail from the correctly advanced old ghost.</summary>
    [Fact]
    public void IdleCatchUpPrecedesNewDamage()
    {
        using var harness = new FrameHarness();
        var target = 100f;
        AnimatedRangeValues values = default;
        void Draw(Gui gui) => values = gui.AnimatedRange(target, lagDelay: 0.25f,
            lagDuration: 0.5f, easing: Easing.Linear, id: "health");
        harness.Frame(Draw, 0);
        target = 80;
        harness.Frame(Draw, 0);
        target = 60;
        harness.Frame(Draw, 1);
        Assert.Equal(new AnimatedRangeValues(60, 80), values);
    }

    /// <summary>Recovery resets the ghost and foreground smoothing never lets the ghost fall below the foreground.</summary>
    [Fact]
    public void RecoveryAndForegroundSmoothingRemainOrdered()
    {
        using var harness = new FrameHarness();
        var target = 100f;
        AnimatedRangeValues values = default;
        void Draw(Gui gui) => values = gui.AnimatedRange(target, duration: 1, lagDelay: 0,
            lagDuration: 0.1f, easing: Easing.Linear, id: "health");
        harness.Frame(Draw, 0);
        target = 50;
        harness.Frame(Draw, 0);
        harness.Frame(Draw, 0.2);
        Assert.InRange(values.Value, 89.99f, 90.01f);
        Assert.Equal(values.Value, values.Ghost);
        harness.Frame(Draw, 1);
        target = 80;
        harness.Frame(Draw, 0);
        Assert.Equal(80, values.Ghost);
        harness.Frame(Draw, 1);
        Assert.Equal(new AnimatedRangeValues(80, 80), values);
    }

    /// <summary>Zero catch-up duration snaps after the hold and reduce motion cancels an unchanged pending trail.</summary>
    [Fact]
    public void SnapAndReduceMotionCancelPendingLag()
    {
        using var harness = new FrameHarness();
        var target = 100f;
        AnimatedRangeValues values = default;
        void Draw(Gui gui) => values = gui.AnimatedRange(target, lagDelay: 0.25f, lagDuration: 0, id: "health");
        harness.Frame(Draw, 0);
        target = 60;
        harness.Frame(Draw, 0);
        harness.Frame(Draw, 0.3);
        Assert.Equal(new AnimatedRangeValues(60, 60), values);
        target = 40;
        harness.Frame(Draw, 0);
        Assert.Equal(60, values.Ghost);
        harness.Gui.ReduceMotion = true;
        harness.Frame(Draw, 0);
        Assert.Equal(new AnimatedRangeValues(40, 40), values);
    }

    /// <summary>Item keys preserve independent trails when entities reorder and ClearData resets presentation state.</summary>
    [Fact]
    public void ItemKeysIsolateAndRetainTrails()
    {
        using var harness = new FrameHarness();
        var targets = new Dictionary<string, float> { ["a"] = 100, ["b"] = 200 };
        var order = new[] { "a", "b" };
        var values = new Dictionary<string, AnimatedRangeValues>();
        void Draw(Gui gui)
        {
            foreach (var key in order)
            {
                using var item = gui.ItemKey(key);
                values[key] = gui.AnimatedRange(targets[key], id: "health");
            }
        }
        harness.Frame(Draw, 0);
        targets["a"] = 60;
        harness.Frame(Draw, 0);
        order = ["b", "a"];
        harness.Frame(Draw, 0.1);
        Assert.Equal(new AnimatedRangeValues(60, 100), values["a"]);
        Assert.Equal(new AnimatedRangeValues(200, 200), values["b"]);
        harness.Gui.ClearData();
        harness.Frame(Draw, 0);
        Assert.Equal(new AnimatedRangeValues(60, 60), values["a"]);
    }

    /// <summary>Named calls in both passes use the same first sample even if a later call supplies a different target.</summary>
    [Fact]
    public void SharedIdentitySamplesOncePerFrame()
    {
        using var harness = new FrameHarness();
        harness.Frame(gui =>
        {
            var first = gui.AnimatedRange(-10, id: "shared");
            var second = gui.AnimatedRange(-20, id: "shared");
            Assert.Equal(first, second);
        });
    }

    /// <summary>Non-finite targets and invalid timing cannot poison persistent animation state.</summary>
    [Theory]
    [InlineData(float.NaN, 0, 0, 0)]
    [InlineData(float.PositiveInfinity, 0, 0, 0)]
    [InlineData(100, -1, 0, 0)]
    [InlineData(100, 0, -1, 0)]
    [InlineData(100, 0, 0, -1)]
    [InlineData(100, float.NaN, 0, 0)]
    public void InvalidArgumentsAreRejected(float target, float duration, float delay, float lagDuration)
    {
        using var harness = new FrameHarness();
        harness.Frame(gui => Assert.Throws<ArgumentOutOfRangeException>(() =>
            gui.AnimatedRange(target, duration, delay, lagDuration)));
    }
}
