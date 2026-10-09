namespace Guinevere.Tests;

/// <summary>Checks easings.net aliases, CSS keywords and <c>cubic-bezier()</c> parsing.</summary>
public class EasingNamesTests
{
    /// <summary>Every alias evaluates like the function it names.</summary>
    [Fact]
    public void Aliases_MatchTheirFunctions()
    {
        (Func<float, float> Alias, Func<float, float> Original)[] pairs =
        [
            (Easing.EaseInSine, Easing.SineIn), (Easing.EaseOutSine, Easing.SineOut),
            (Easing.EaseInOutSine, Easing.SineInOut), (Easing.EaseInQuad, Easing.EaseIn),
            (Easing.EaseOutQuad, Easing.EaseOut), (Easing.EaseInOutQuad, Easing.EaseInOut),
            (Easing.EaseInCubic, Easing.CubicIn), (Easing.EaseOutCubic, Easing.CubicOut),
            (Easing.EaseInOutCubic, Easing.CubicInOut), (Easing.EaseInQuart, Easing.QuartIn),
            (Easing.EaseOutQuart, Easing.QuartOut), (Easing.EaseInOutQuart, Easing.QuartInOut),
            (Easing.EaseInQuint, Easing.QuintIn), (Easing.EaseOutQuint, Easing.QuintOut),
            (Easing.EaseInOutQuint, Easing.QuintInOut), (Easing.EaseInExpo, Easing.ExpoIn),
            (Easing.EaseOutExpo, Easing.ExpoOut), (Easing.EaseInOutExpo, Easing.ExpoInOut),
            (Easing.EaseInCirc, Easing.CircIn), (Easing.EaseOutCirc, Easing.CircOut),
            (Easing.EaseInOutCirc, Easing.CircInOut), (Easing.EaseInBack, Easing.BackIn),
            (Easing.EaseOutBack, Easing.BackOut), (Easing.EaseInOutBack, Easing.BackInOut),
            (Easing.EaseInElastic, Easing.ElasticIn), (Easing.EaseOutElastic, Easing.ElasticOut),
            (Easing.EaseInOutElastic, Easing.ElasticInOut), (Easing.EaseInBounce, Easing.BounceIn),
            (Easing.EaseOutBounce, Easing.BounceOut), (Easing.EaseInOutBounce, Easing.BounceInOut),
        ];
        foreach (var (alias, original) in pairs)
            Assert.Equal(original(0.3f), alias(0.3f));
    }

    /// <summary>CSS keywords use the CSS curves; easings.net names work in kebab or Pascal case.</summary>
    [Theory]
    [InlineData("ease-in-out-sine", 0.3f)]
    [InlineData("EaseInOutSine", 0.3f)]
    [InlineData("linear", 0.3f)]
    [InlineData(" smooth-step ", 0.3f)]
    public void TryParse_FindsNames(string name, float t)
    {
        Assert.True(Easing.TryParse(name, out var easing));
        Assert.InRange(easing(t), 0f, 1f);
        Assert.Equal(0f, easing(0f), 4);
        Assert.Equal(1f, easing(1f), 4);
    }

    /// <summary>CSS <c>ease-in</c> is the CSS curve, which differs from the quadratic <see cref="Easing.EaseIn"/>.</summary>
    [Fact]
    public void TryParse_CssKeywordsFollowCss()
    {
        Assert.True(Easing.TryParse("ease-in", out var cssIn));
        Assert.Equal(0.3153f, cssIn(0.5f), 3);
        Assert.NotEqual(Easing.EaseIn(0.5f), cssIn(0.5f), 2);
        Assert.True(Easing.TryParse("ease", out var ease));
        Assert.Equal(0.8024f, ease(0.5f), 3);
    }

    /// <summary><c>cubic-bezier()</c> parses four numbers with x inside [0, 1]; anything else is rejected.</summary>
    [Fact]
    public void TryParse_CubicBezier()
    {
        Assert.True(Easing.TryParse("cubic-bezier(0, 0, 1, 1)", out var linear));
        Assert.Equal(0.4f, linear(0.4f), 3);
        Assert.False(Easing.TryParse("cubic-bezier(0, 0, 1)", out _));
        Assert.False(Easing.TryParse("cubic-bezier(2, 0, 1, 1)", out _));
        Assert.False(Easing.TryParse("cubic-bezier(a, 0, 1, 1)", out _));
        Assert.False(Easing.TryParse("wobble", out var fallback));
        Assert.Equal(0.7f, fallback(0.7f));
        Assert.False(Easing.TryParse(null, out _));
    }
}
