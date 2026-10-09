namespace Guinevere.Tests.Animation;

/// <summary>Checks the closed-form <see cref="Spring"/> solver.</summary>
public class SpringTests
{
    static (float Value, float Velocity) Run(float dampingRatio, float total, int steps)
    {
        var (value, velocity) = (0f, 0f);
        for (var i = 0; i < steps; i++) Spring.Step(ref value, ref velocity, 1f, total / steps, 12f, dampingRatio);
        return (value, velocity);
    }

    /// <summary>The same elapsed time lands on the same state however it is split into frames.</summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(0.3f)]
    [InlineData(2.5f)]
    public void Step_IsFrameRateIndependent(float dampingRatio)
    {
        var coarse = Run(dampingRatio, 0.12f, 1);
        var fine = Run(dampingRatio, 0.12f, 12);
        Assert.Equal(coarse.Value, fine.Value, 3);
        Assert.Equal(coarse.Velocity, fine.Velocity, 2);
        Assert.InRange(coarse.Value, 0.01f, 1.5f);
    }

    /// <summary>Under-damped springs overshoot; critically and over-damped ones do not.</summary>
    [Fact]
    public void Step_OvershootsOnlyWhenUnderDamped()
    {
        float Peak(float zeta)
        {
            var (value, velocity, peak) = (0f, 0f, 0f);
            for (var i = 0; i < 200; i++)
            {
                Spring.Step(ref value, ref velocity, 1f, 0.01f, 12f, zeta);
                peak = Math.Max(peak, value);
            }
            return peak;
        }

        Assert.True(Peak(0.2f) > 1.2f);
        Assert.True(Peak(1f) <= 1f);
        Assert.True(Peak(3f) <= 1f);
    }

    /// <summary>A settled spring holds its target exactly; zero time and zero frequency change nothing.</summary>
    [Fact]
    public void Step_SettlesExactlyAndIgnoresEmptySteps()
    {
        var (value, velocity) = (0.25f, 2f);
        Assert.False(Spring.Step(ref value, ref velocity, 1f, 0f, 12f));
        Assert.Equal((0.25f, 2f), (value, velocity));
        Assert.False(Spring.Step(ref value, ref velocity, 1f, 0.1f, 0f));
        Assert.Equal((0.25f, 2f), (value, velocity));

        var settled = false;
        for (var i = 0; i < 500 && !settled; i++) settled = Spring.Step(ref value, ref velocity, 1f, 0.016f, 12f);
        Assert.True(settled);
        Assert.Equal((1f, 0f), (value, velocity));
    }
}
