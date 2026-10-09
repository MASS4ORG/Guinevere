using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Animation;

/// <summary>Checks automatic animation identity and frame-stable values with real GUI passes.</summary>
public class GuiScopedAnimationTests
{
    /// <summary>Animations at the same loop call site retain separate values for each keyed item.</summary>
    [Fact]
    public void LoopAnimationsUseSharedItemIdentity()
    {
        using var harness = new FrameHarness();
        int[] order = [1, 2];
        var targets = new Dictionary<int, bool> { [1] = false, [2] = true };
        var values = new Dictionary<int, float>();
        void Draw(Gui gui)
        {
            foreach (var item in order)
            {
                using var key = gui.ItemKey(item);
                values[item] = gui.AnimateBool01(targets[item], 1, Easing.Linear);
            }
        }
        harness.Frame(Draw);
        Assert.Equal(0, values[1]);
        Assert.Equal(1, values[2]);
        targets[1] = true;
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.InRange(values[1], .015f, .017f);
        Assert.Equal(1, values[2]);
        Assert.Equal(2, harness.Gui.ActiveAnimationCount);
        Assert.Equal(1, harness.Gui.RunningAnimationCount);
        order = [2];
        harness.Frame(Draw);
        harness.Gui.Time.Update(2);
        order = [2, 1];
        harness.Frame(Draw);
        Assert.Equal(1, values[1]);
        Assert.Equal(0, harness.Gui.RunningAnimationCount);
        harness.Gui.ClearAnimations();
        Assert.Equal(0, harness.Gui.ActiveAnimationCount);
    }

    /// <summary>Render-time target changes cannot alter the build result, and named keys share only within their scope.</summary>
    [Fact]
    public void NamedAnimationsSampleOncePerFrameWithinEachScope()
    {
        using var harness = new FrameHarness();
        var target = false;
        var build = 0f;
        var samples = 0;
        float Ease(float value) { samples++; return value; }
        void Draw(Gui gui)
        {
            using (gui.EnterDataScope("popup"))
            {
                var value = gui.AnimateBool01("visibility", target, 1, Ease);
                if (gui.Pass == Pass.Pass1Build) build = value;
                else Assert.Equal(build, value);
                Assert.Equal(value, gui.AnimateBool01("visibility", !target, 0, Ease));
            }
            using (gui.EnterDataScope("tooltip"))
                Assert.Equal(1, gui.AnimateBool01("visibility", true, 0, Easing.Linear));
        }
        harness.Frame(Draw);
        target = true;
        harness.Frame(Draw);
        var afterStart = samples;
        harness.Frame(Draw);
        Assert.Equal(afterStart + 1, samples);
        Assert.InRange(build, .015f, .017f);
        Assert.Equal(2, harness.Gui.ActiveAnimationCount);
        harness.Frame(gui => Assert.Throws<ArgumentNullException>(() =>
            gui.AnimateBool01("bad", true, 1, null!)));
    }
}
