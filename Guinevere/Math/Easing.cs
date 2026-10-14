namespace Guinevere;

/// <summary>
/// Provides a collection of static methods to compute easing functions commonly used
/// in animation and interpolation. Easing functions modify the progression of a value
/// over time to achieve various motion effects such as acceleration, deceleration,
/// and elastic behaviors.
/// </summary>
public static partial class Easing
{
    /// <summary>
    /// Linear interpolation with no easing.
    /// </summary>
    /// <param name="t">Normalized time (0 to 1)</param>
    /// <returns>Linear interpolated value</returns>
    [PublicAPI]
    public static float Linear(float t)
    {
        return t;
    }


    /// <summary>
    /// Quadratic ease-in: Accelerates from zero velocity.
    /// </summary>
    [PublicAPI]
    public static float EaseIn(float t)
    {
        return t * t;
    }

    /// <summary>
    /// Quadratic ease-out: Decelerates to zero velocity.
    /// </summary>
    [PublicAPI]
    public static float EaseOut(float t)
    {
        return 1 - MathF.Pow(1 - t, 2);
    }

    /// <summary>
    /// Quadratic ease-in-out: Accelerates until halfway, then decelerates.
    /// </summary>
    [PublicAPI]
    public static float EaseInOut(float t)
    {
        return t < 0.5f ? 2 * t * t : 1 - MathF.Pow(-2 * t + 2, 2) / 2;
    }


    /// <summary>
    /// Cubic ease-in: More pronounced acceleration from zero velocity.
    /// </summary>
    [PublicAPI]
    public static float CubicIn(float t)
    {
        return t * t * t;
    }

    /// <summary>
    /// Cubic ease-out: More pronounced deceleration to zero velocity.
    /// </summary>
    [PublicAPI]
    public static float CubicOut(float t)
    {
        return 1 - MathF.Pow(1 - t, 3);
    }

    /// <summary>
    /// Cubic ease-in-out: Stronger acceleration until halfway, then stronger deceleration.
    /// </summary>
    [PublicAPI]
    public static float CubicInOut(float t)
    {
        return t < 0.5f ? 4 * t * t * t : 1 - MathF.Pow(-2 * t + 2, 3) / 2;
    }


    /// <summary>
    /// Quartic ease-in: Very pronounced acceleration from zero velocity.
    /// </summary>
    [PublicAPI]
    public static float QuartIn(float t)
    {
        return t * t * t * t;
    }

    /// <summary>
    /// Quartic ease-out: Very pronounced deceleration to zero velocity.
    /// </summary>
    [PublicAPI]
    public static float QuartOut(float t)
    {
        return 1 - MathF.Pow(1 - t, 4);
    }

    /// <summary>
    /// Quartic ease-in-out: Dramatic acceleration until halfway, then dramatic deceleration.
    /// </summary>
    [PublicAPI]
    public static float QuartInOut(float t)
    {
        return t < 0.5f ? 8 * t * t * t * t : 1 - MathF.Pow(-2 * t + 2, 4) / 2;
    }


    /// <summary>
    /// Quintic ease-in: Extremely pronounced acceleration from zero velocity.
    /// </summary>
    [PublicAPI]
    public static float QuintIn(float t)
    {
        return t * t * t * t * t;
    }

    /// <summary>
    /// Quintic ease-out: Extremely pronounced deceleration to zero velocity.
    /// </summary>
    [PublicAPI]
    public static float QuintOut(float t)
    {
        return 1 - MathF.Pow(1 - t, 5);
    }

    /// <summary>
    /// Quintic ease-in-out: Extreme acceleration until halfway, then extreme deceleration.
    /// </summary>
    [PublicAPI]
    public static float QuintInOut(float t)
    {
        return t < 0.5f ? 16 * t * t * t * t * t : 1 - MathF.Pow(-2 * t + 2, 5) / 2;
    }


    /// <summary>
    /// Sinusoidal ease-in: Gradual acceleration using a sine curve.
    /// </summary>
    [PublicAPI]
    public static float SineIn(float t)
    {
        return 1 - MathF.Cos((t * MathF.PI) / 2);
    }

    /// <summary>
    /// Sinusoidal ease-out: Gradual deceleration using a sine curve.
    /// </summary>
    [PublicAPI]
    public static float SineOut(float t)
    {
        return MathF.Sin((t * MathF.PI) / 2);
    }

    /// <summary>
    /// Sinusoidal ease-in-out: Gentle acceleration and deceleration based on a sine curve.
    /// </summary>
    [PublicAPI]
    public static float SineInOut(float t)
    {
        return -(MathF.Cos(MathF.PI * t) - 1) / 2;
    }


    /// <summary>
    /// Exponential ease-in: Acceleration with an exponential growth curve.
    /// </summary>
    [PublicAPI]
    public static float ExpoIn(float t)
    {
        return t == 0f ? 0f : MathF.Pow(2, 10 * t - 10);
    }

    /// <summary>
    /// Exponential ease-out: Deceleration with an exponential decay curve.
    /// </summary>
    [PublicAPI]
    public static float ExpoOut(float t)
    {
        return ImMath.ApproximatelyEquals(t, 1f) ? 1f : 1 - MathF.Pow(2, -10 * t);
    }

    /// <summary>
    /// Exponential ease-in-out: Exponential acceleration until halfway, then exponential deceleration.
    /// </summary>
    [PublicAPI]
    public static float ExpoInOut(float t)
    {
        return t == 0f ? 0f :
            ImMath.ApproximatelyEquals(t, 1f) ? 1f :
            t < 0.5f ? MathF.Pow(2, 20 * t - 10) / 2 : (2 - MathF.Pow(2, -20 * t + 10)) / 2;
    }


    /// <summary>
    /// Circular ease-in: Acceleration following a quarter-circle curve.
    /// </summary>
    [PublicAPI]
    public static float CircIn(float t)
    {
        return 1 - MathF.Sqrt(1 - MathF.Pow(t, 2));
    }

    /// <summary>
    /// Circular ease-out: Deceleration following a quarter-circle curve.
    /// </summary>
    [PublicAPI]
    public static float CircOut(float t)
    {
        return MathF.Sqrt(1 - MathF.Pow(t - 1, 2));
    }

    /// <summary>
    /// Circular ease-in-out: Acceleration and deceleration following a semicircle curve.
    /// </summary>
    [PublicAPI]
    public static float CircInOut(float t)
    {
        return t < 0.5f
            ? (1 - MathF.Sqrt(1 - MathF.Pow(2 * t, 2))) / 2
            : (MathF.Sqrt(1 - MathF.Pow(-2 * t + 2, 2)) + 1) / 2;
    }


    /// <summary>
    /// Back ease-in: Slight overshoot backward before accelerating forward.
    /// </summary>
    [PublicAPI]
    public static float BackIn(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1;
        return c3 * t * t * t - c1 * t * t;
    }

    /// <summary>
    /// Back ease-out: Acceleration followed by a slight overshoot beyond the final position.
    /// </summary>
    [PublicAPI]
    public static float BackOut(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1;
        return 1 + c3 * MathF.Pow(t - 1, 3) + c1 * MathF.Pow(t - 1, 2);
    }

    /// <summary>
    /// Back ease-in-out: Slight overshoot in both directions.
    /// </summary>
    [PublicAPI]
    public static float BackInOut(float t)
    {
        const float c1 = 1.70158f;
        const float c2 = c1 * 1.525f;
        return t < 0.5f
            ? MathF.Pow(2 * t, 2) * ((c2 + 1) * 2 * t - c2) / 2
            : (MathF.Pow(2 * t - 2, 2) * ((c2 + 1) * (t * 2 - 2) + c2) + 2) / 2;
    }


    /// <summary>
    /// Elastic ease-in: Begins slowly and then accelerates with a spring-like effect.
    /// </summary>
    [PublicAPI]
    public static float ElasticIn(float t)
    {
        const float c4 = 2 * MathF.PI / 3;
        return t == 0f ? 0f :
            ImMath.ApproximatelyEquals(t, 1f) ? 1f :
            -MathF.Pow(2, 10 * t - 10) * MathF.Sin((t * 10 - 10.75f) * c4);
    }

    /// <summary>
    /// Elastic ease-out: Overshoots the destination and then oscillates to the final position.
    /// </summary>
    [PublicAPI]
    public static float ElasticOut(float t)
    {
        const float c4 = 2 * MathF.PI / 3;
        return t == 0f ? 0f :
            ImMath.ApproximatelyEquals(t, 1f) ? 1f :
            MathF.Pow(2, -10 * t) * MathF.Sin((t * 10 - 0.75f) * c4) + 1;
    }

    /// <summary>
    /// Elastic ease-in-out: Oscillating effect at both the beginning and the end.
    /// </summary>
    [PublicAPI]
    public static float ElasticInOut(float t)
    {
        const float c5 = 2 * MathF.PI / 4.5f;
        return t == 0f ? 0f :
            ImMath.ApproximatelyEquals(t, 1f) ? 1f :
            t < 0.5f ? -(MathF.Pow(2, 20 * t - 10) * MathF.Sin((20 * t - 11.125f) * c5)) / 2 :
            MathF.Pow(2, -20 * t + 10) * MathF.Sin((20 * t - 11.125f) * c5) / 2 + 1;
    }


    /// <summary>
    /// Bounce ease-out: Bounces multiple times near the destination before settling.
    /// </summary>
    [PublicAPI]
    public static float BounceOut(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;

        if (t < 1 / d1)
        {
            return n1 * t * t;
        }

        if (t < 2 / d1)
        {
            return n1 * (t -= 1.5f / d1) * t + 0.75f;
        }

        if (t < 2.5 / d1)
        {
            return n1 * (t -= 2.25f / d1) * t + 0.9375f;
        }

        return n1 * (t -= 2.625f / d1) * t + 0.984375f;
    }

    /// <summary>
    /// Bounce ease-in: Bounces multiple times at the start before accelerating.
    /// </summary>
    [PublicAPI]
    public static float BounceIn(float t)
    {
        return 1 - BounceOut(1 - t);
    }

    /// <summary>
    /// Bounce ease-in-out: Bounces at both the beginning and end of the animation.
    /// </summary>
    [PublicAPI]
    public static float BounceInOut(float t)
    {
        return t < 0.5f ? (1 - BounceOut(1 - 2 * t)) / 2 : (1 + BounceOut(2 * t - 1)) / 2;
    }


    /// <summary>
    /// Steps instantly from 0 to 1 at the midpoint.
    /// Useful for binary state transitions.
    /// </summary>
    [PublicAPI]
    public static float Step(float t)
    {
        return t < 0.5f ? 0f : 1f;
    }

    /// <summary>
    /// Smoothstep: Smooth Hermite interpolation.
    /// Provides a smoother transition than EaseInOut with minimal computation.
    /// </summary>
    [PublicAPI]
    public static float SmoothStep(float t)
    {
        // Clamp between 0 and 1
        t = Math.Max(0f, Math.Min(1f, t));
        // Evaluate polynomial
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// SmootherStep: Even smoother Hermite interpolation.
    /// Higher degree polynomial for more continuous derivatives.
    /// </summary>
    [PublicAPI]
    public static float SmootherStep(float t)
    {
        // Clamp between 0 and 1
        t = Math.Max(0f, Math.Min(1f, t));
        // Evaluate higher degree polynomial
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    /// <summary>
    /// Spring function with configurable elasticity.
    /// Returns values that can exceed the 0-1 range during oscillation.
    /// </summary>
    /// <param name="t">Normalized time (0 to 1)</param>
    /// <param name="dampingRatio">Controls oscillation damping (0.1 = lots of oscillation, 1.0 = no oscillation)</param>
    /// <param name="angularFrequency">Controls speed of oscillation (default = 20)</param>
    [PublicAPI]
    public static float Spring(float t, float dampingRatio = 0.5f, float angularFrequency = 20.0f)
    {
        // Clamp to avoid issues
        dampingRatio = Math.Max(0.0001f, dampingRatio);

        // Don't calculate for extremes
        if (t <= 0f)
        {
            return 0f;
        }

        if (t >= 1f)
        {
            return 1f;
        }

        if (dampingRatio < 1.0f) // Under-damped
        {
            // Calculate for oscillation
            var envelope = MathF.Exp(-dampingRatio * angularFrequency * t);
            var exponent = angularFrequency * MathF.Sqrt(1.0f - dampingRatio * dampingRatio) * t;
            return 1.0f - envelope * MathF.Cos(exponent);
        }
        else // Critically damped (no oscillation)
        {
            var envelope = MathF.Exp(-angularFrequency * t);
            return 1.0f - envelope * (1.0f + angularFrequency * t);
        }
    }
}
