namespace Guinevere;

/// <summary>
/// A damped spring solved in closed form, so the result depends only on elapsed time and not on how it is split into
/// frames. Interrupting it with a new target keeps position and velocity continuous.
/// </summary>
public static class Spring
{
    const float Epsilon = 0.0001f;
    const float SettleDistance = 0.0001f;
    const float SettleSpeed = 0.001f;

    /// <summary>Advances <paramref name="value"/> and <paramref name="velocity"/> toward the target.</summary>
    /// <param name="value">The current value, replaced by the value after <paramref name="deltaTime"/>.</param>
    /// <param name="velocity">The current velocity in units per second, replaced like the value.</param>
    /// <param name="target">The rest position.</param>
    /// <param name="deltaTime">Elapsed seconds; zero or less leaves the state unchanged.</param>
    /// <param name="frequency">Angular frequency in radians per second; higher settles faster.</param>
    /// <param name="dampingRatio">One is critically damped; below one overshoots, above one approaches slower.</param>
    /// <returns>Whether the spring has settled on the target, which it then holds exactly.</returns>
    public static bool Step(ref float value, ref float velocity, float target, float deltaTime, float frequency,
        float dampingRatio = 1f)
    {
        if (deltaTime > 0f && frequency > 0f)
        {
            var (positionGain, velocityGain, velocityPosition, velocityVelocity) =
                Coefficients(deltaTime, frequency, Math.Max(0f, dampingRatio));
            var offset = value - target;
            value = offset * positionGain + velocity * velocityGain + target;
            velocity = offset * velocityPosition + velocity * velocityVelocity;
        }
        if (Math.Abs(value - target) > SettleDistance || Math.Abs(velocity) > SettleSpeed)
        {
            return false;
        }

        value = target;
        velocity = 0f;
        return true;
    }

    /// <summary>Maps the offset and velocity at the start of a step to the offset and velocity at its end.</summary>
    static (float PosPos, float PosVel, float VelPos, float VelVel) Coefficients(float t, float omega, float zeta)
    {
        if (zeta > 1f + Epsilon)
        {
            return OverDamped(t, omega, zeta);
        }

        if (zeta < 1f - Epsilon)
        {
            return UnderDamped(t, omega, zeta);
        }

        var exp = MathF.Exp(-omega * t);
        var timeExp = t * exp;
        var timeExpFrequency = timeExp * omega;
        return (timeExpFrequency + exp, timeExp, -omega * timeExpFrequency, exp - timeExpFrequency);
    }

    static (float, float, float, float) OverDamped(float t, float omega, float zeta)
    {
        var za = -omega * zeta;
        var zb = omega * MathF.Sqrt(zeta * zeta - 1f);
        var (z1, z2) = (za - zb, za + zb);
        var e1 = MathF.Exp(z1 * t) / (2f * zb);
        var e2 = MathF.Exp(z2 * t) / (2f * zb);
        var (z1E1, z2E2) = (z1 * e1, z2 * e2);
        return (e1 * z2 - z2E2 + e2 * 2f * zb, -e1 + e2, (z1E1 - z2E2 + e2 * 2f * zb) * z2, -z1E1 + z2E2);
    }

    static (float, float, float, float) UnderDamped(float t, float omega, float zeta)
    {
        var omegaZeta = omega * zeta;
        var alpha = omega * MathF.Sqrt(1f - zeta * zeta);
        var exp = MathF.Exp(-omegaZeta * t);
        var (sin, cos) = MathF.SinCos(alpha * t);
        var (expSin, expCos) = (exp * sin, exp * cos);
        var expOmegaZetaSinOverAlpha = exp * omegaZeta * sin / alpha;
        return (expCos + expOmegaZetaSinOverAlpha, expSin / alpha,
            -expSin * alpha - omegaZeta * expOmegaZetaSinOverAlpha, expCos - expOmegaZetaSinOverAlpha);
    }
}
