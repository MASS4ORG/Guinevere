using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Animation;

/// <summary>Checks keyed springs and tweens over real GUI passes.</summary>
public class GuiMotionTests
{
    /// <summary>Smoothdamp starts at its target, follows changes, gives both passes one value and exposes a writable ref.</summary>
    [Fact]
    public void Smoothdamp_FollowsOncePerFrame()
    {
        using var harness = new FrameHarness();
        var target = 0f;
        var build = 0f;
        var value = 0f;
        void Draw(Gui gui)
        {
            ref var smoothed = ref gui.Smoothdamp(target);
            if (gui.Pass == Pass.Pass1Build) build = smoothed;
            else Assert.Equal(build, smoothed);
            value = smoothed;
        }

        harness.Frame(Draw);
        Assert.Equal(0f, value);
        target = 10f;
        harness.Frame(Draw);
        Assert.InRange(value, 0.1f, 9.9f);
        var previous = value;
        harness.Frame(Draw);
        Assert.InRange(value, previous, 10f);

        harness.Frame(gui =>
        {
            ref var smoothed = ref gui.Smoothdamp(target, id: "shared");
            if (gui.Pass == Pass.Pass1Build) smoothed = 3f;
            else Assert.Equal(3f, gui.Smoothdamp(0f, id: "shared"));
        });
    }

    /// <summary>Loop items keep separate springs; vectors, colors and angles follow per channel.</summary>
    [Fact]
    public void Smoothdamp_TypedOverloadsAndItemKeys()
    {
        using var harness = new FrameHarness();
        var targets = new Dictionary<int, float> { [1] = 0f, [2] = 100f };
        var values = new Dictionary<int, float>();
        var vector = Vector2.Zero;
        var color = Color.Black;
        var angle = 0f;
        var angleTarget = 0.1f;
        var far = false;
        void Draw(Gui gui)
        {
            foreach (var item in targets.Keys)
            {
                using var key = gui.ItemKey(item);
                values[item] = gui.Smoothdamp(targets[item]);
            }
            vector = gui.Smoothdamp(far ? new Vector2(10, 20) : Vector2.Zero);
            color = gui.SmoothdampColor(far ? Color.White : Color.Black);
            angle = gui.SmoothdampAngle(angleTarget);
        }

        harness.Frame(Draw);
        Assert.Equal((0f, 100f), (values[1], values[2]));
        far = true;
        angleTarget = MathF.Tau - 0.1f;
        for (var i = 0; i < 3; i++) harness.Frame(Draw);
        Assert.Equal(100f, values[2]);
        Assert.InRange(vector.X, 0.1f, 9.9f);
        Assert.InRange(vector.Y, vector.X, 19.9f);
        Assert.InRange(color.R, (byte)1, (byte)254);
        Assert.InRange(angle, -0.1f, 0.1f);
        for (var i = 0; i < 200; i++) harness.Frame(Draw);
        Assert.Equal(new Vector2(10, 20), vector);
        Assert.Equal(Color.White.R, color.R);
        Assert.Equal(-0.1f, angle, 3);
    }

    /// <summary>An under-damped keyed spring overshoots before settling.</summary>
    [Fact]
    public void AnimateSpring_Overshoots()
    {
        using var harness = new FrameHarness();
        var target = 0f;
        var peak = 0f;
        var value = 0f;
        void Draw(Gui gui) => value = gui.AnimateSpring(target, 20f, 0.2f);
        harness.Frame(Draw);
        target = 1f;
        for (var i = 0; i < 60; i++)
        {
            harness.Frame(Draw);
            peak = Math.Max(peak, value);
        }
        Assert.True(peak > 1.1f);
    }

    /// <summary>Tweens restart from the value shown when interrupted and reach the new target on time.</summary>
    [Fact]
    public void AnimateFloat_InterruptsContinuously()
    {
        using var harness = new FrameHarness();
        var target = 0f;
        var value = 0f;
        var color = Color.Black;
        void Draw(Gui gui)
        {
            value = gui.AnimateFloat(target, 0.16f, Easing.Linear);
            color = gui.AnimateColor(target > 0f ? Color.White : Color.Black, 0.16f);
        }

        harness.Frame(Draw);
        target = 100f;
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Equal(10f, value, 1);
        Assert.InRange(color.R, (byte)1, (byte)254);
        target = 0f;
        harness.Frame(Draw);
        Assert.Equal(10f, value, 1);
        harness.Frame(Draw);
        Assert.Equal(9f, value, 1);
        for (var i = 0; i < 10; i++) harness.Frame(Draw);
        Assert.Equal(0f, value);
        Assert.Equal(Color.Black, color);
    }

    /// <summary>Reduced motion makes every keyed animation jump; clearing forgets their state.</summary>
    [Fact]
    public void ReduceMotion_JumpsAndClearForgets()
    {
        using var harness = new FrameHarness();
        var target = 0f;
        (float Damp, float Tween, float Spring, float Bool) values = default;
        void Draw(Gui gui) => values = (gui.Smoothdamp(target), gui.AnimateFloat(target, 1f),
            gui.AnimateSpring(target, 5f, 0.5f), gui.AnimateBool01(target > 0f, 1f, Easing.Linear));

        harness.Frame(Draw);
        harness.Gui.ReduceMotion = true;
        target = 1f;
        harness.Frame(Draw);
        Assert.Equal((1f, 1f, 1f, 1f), values);

        harness.Gui.ReduceMotion = false;
        harness.Gui.ClearAnimations();
        target = 5f;
        harness.Frame(Draw);
        Assert.Equal((5f, 5f, 5f, 1f), values);
    }
}
