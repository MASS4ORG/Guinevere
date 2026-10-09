namespace Guinevere;

public static partial class Easing
{
    static readonly Func<float, float> CssEase = CubicBezier(0.25f, 0.1f, 0.25f, 1f);
    static readonly Func<float, float> CssEaseIn = CubicBezier(0.42f, 0f, 1f, 1f);
    static readonly Func<float, float> CssEaseOut = CubicBezier(0f, 0f, 0.58f, 1f);
    static readonly Func<float, float> CssEaseInOut = CubicBezier(0.42f, 0f, 0.58f, 1f);

    static readonly Dictionary<string, Func<float, float>> Named = new(StringComparer.Ordinal)
    {
        ["linear"] = Linear,
        ["ease"] = CssEase,
        ["easein"] = CssEaseIn,
        ["easeout"] = CssEaseOut,
        ["easeinout"] = CssEaseInOut,
        ["step"] = Step,
        ["smoothstep"] = SmoothStep,
        ["smootherstep"] = SmootherStep,
        ["easeinsine"] = SineIn,
        ["easeoutsine"] = SineOut,
        ["easeinoutsine"] = SineInOut,
        ["easeinquad"] = EaseIn,
        ["easeoutquad"] = EaseOut,
        ["easeinoutquad"] = EaseInOut,
        ["easeincubic"] = CubicIn,
        ["easeoutcubic"] = CubicOut,
        ["easeinoutcubic"] = CubicInOut,
        ["easeinquart"] = QuartIn,
        ["easeoutquart"] = QuartOut,
        ["easeinoutquart"] = QuartInOut,
        ["easeinquint"] = QuintIn,
        ["easeoutquint"] = QuintOut,
        ["easeinoutquint"] = QuintInOut,
        ["easeinexpo"] = ExpoIn,
        ["easeoutexpo"] = ExpoOut,
        ["easeinoutexpo"] = ExpoInOut,
        ["easeincirc"] = CircIn,
        ["easeoutcirc"] = CircOut,
        ["easeinoutcirc"] = CircInOut,
        ["easeinback"] = BackIn,
        ["easeoutback"] = BackOut,
        ["easeinoutback"] = BackInOut,
        ["easeinelastic"] = ElasticIn,
        ["easeoutelastic"] = ElasticOut,
        ["easeinoutelastic"] = ElasticInOut,
        ["easeinbounce"] = BounceIn,
        ["easeoutbounce"] = BounceOut,
        ["easeinoutbounce"] = BounceInOut,
    };

    /// <summary>Sinusoidal ease-in, under its easings.net name.</summary>
    public static float EaseInSine(float t) => SineIn(t);

    /// <summary>Sinusoidal ease-out, under its easings.net name.</summary>
    public static float EaseOutSine(float t) => SineOut(t);

    /// <summary>Sinusoidal ease-in-out, under its easings.net name.</summary>
    public static float EaseInOutSine(float t) => SineInOut(t);

    /// <summary>Quadratic ease-in, under its easings.net name.</summary>
    public static float EaseInQuad(float t) => EaseIn(t);

    /// <summary>Quadratic ease-out, under its easings.net name.</summary>
    public static float EaseOutQuad(float t) => EaseOut(t);

    /// <summary>Quadratic ease-in-out, under its easings.net name.</summary>
    public static float EaseInOutQuad(float t) => EaseInOut(t);

    /// <summary>Cubic ease-in, under its easings.net name.</summary>
    public static float EaseInCubic(float t) => CubicIn(t);

    /// <summary>Cubic ease-out, under its easings.net name.</summary>
    public static float EaseOutCubic(float t) => CubicOut(t);

    /// <summary>Cubic ease-in-out, under its easings.net name.</summary>
    public static float EaseInOutCubic(float t) => CubicInOut(t);

    /// <summary>Quartic ease-in, under its easings.net name.</summary>
    public static float EaseInQuart(float t) => QuartIn(t);

    /// <summary>Quartic ease-out, under its easings.net name.</summary>
    public static float EaseOutQuart(float t) => QuartOut(t);

    /// <summary>Quartic ease-in-out, under its easings.net name.</summary>
    public static float EaseInOutQuart(float t) => QuartInOut(t);

    /// <summary>Quintic ease-in, under its easings.net name.</summary>
    public static float EaseInQuint(float t) => QuintIn(t);

    /// <summary>Quintic ease-out, under its easings.net name.</summary>
    public static float EaseOutQuint(float t) => QuintOut(t);

    /// <summary>Quintic ease-in-out, under its easings.net name.</summary>
    public static float EaseInOutQuint(float t) => QuintInOut(t);

    /// <summary>Exponential ease-in, under its easings.net name.</summary>
    public static float EaseInExpo(float t) => ExpoIn(t);

    /// <summary>Exponential ease-out, under its easings.net name.</summary>
    public static float EaseOutExpo(float t) => ExpoOut(t);

    /// <summary>Exponential ease-in-out, under its easings.net name.</summary>
    public static float EaseInOutExpo(float t) => ExpoInOut(t);

    /// <summary>Circular ease-in, under its easings.net name.</summary>
    public static float EaseInCirc(float t) => CircIn(t);

    /// <summary>Circular ease-out, under its easings.net name.</summary>
    public static float EaseOutCirc(float t) => CircOut(t);

    /// <summary>Circular ease-in-out, under its easings.net name.</summary>
    public static float EaseInOutCirc(float t) => CircInOut(t);

    /// <summary>Back ease-in, under its easings.net name.</summary>
    public static float EaseInBack(float t) => BackIn(t);

    /// <summary>Back ease-out, under its easings.net name.</summary>
    public static float EaseOutBack(float t) => BackOut(t);

    /// <summary>Back ease-in-out, under its easings.net name.</summary>
    public static float EaseInOutBack(float t) => BackInOut(t);

    /// <summary>Elastic ease-in, under its easings.net name.</summary>
    public static float EaseInElastic(float t) => ElasticIn(t);

    /// <summary>Elastic ease-out, under its easings.net name.</summary>
    public static float EaseOutElastic(float t) => ElasticOut(t);

    /// <summary>Elastic ease-in-out, under its easings.net name.</summary>
    public static float EaseInOutElastic(float t) => ElasticInOut(t);

    /// <summary>Bounce ease-in, under its easings.net name.</summary>
    public static float EaseInBounce(float t) => BounceIn(t);

    /// <summary>Bounce ease-out, under its easings.net name.</summary>
    public static float EaseOutBounce(float t) => BounceOut(t);

    /// <summary>Bounce ease-in-out, under its easings.net name.</summary>
    public static float EaseInOutBounce(float t) => BounceInOut(t);

    /// <summary>
    /// Finds an easing by a CSS keyword (<c>ease</c>, <c>ease-in</c>, <c>linear</c>, …), an easings.net name in any
    /// case or kebab form (<c>ease-in-out-sine</c>, <c>EaseOutBack</c>), or <c>cubic-bezier(x1, y1, x2, y2)</c>.
    /// </summary>
    /// <param name="name">The name to look up.</param>
    /// <param name="easing">The easing function, or <see cref="Linear"/> when the name is unknown.</param>
    /// <returns>Whether the name was recognized.</returns>
    public static bool TryParse(string? name, out Func<float, float> easing)
    {
        easing = Linear;
        if (string.IsNullOrWhiteSpace(name)) return false;
        var text = name.Trim();
        if (text.StartsWith("cubic-bezier(", StringComparison.OrdinalIgnoreCase) && text.EndsWith(')'))
            return TryParseBezier(text[13..^1], out easing);
        var key = text.Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
        if (!Named.TryGetValue(key, out var found)) return false;
        easing = found;
        return true;
    }

    static bool TryParseBezier(string arguments, out Func<float, float> easing)
    {
        easing = Linear;
        var parts = arguments.Split(',');
        if (parts.Length != 4 || !TryParseNumbers(parts, out var values)) return false;
        if (!IsUnit(values[0]) || !IsUnit(values[2])) return false;
        easing = CubicBezier(values[0], values[1], values[2], values[3]);
        return true;
    }

    static bool IsUnit(float value) => value is >= 0f and <= 1f;

    static bool TryParseNumbers(string[] parts, out float[] values)
    {
        values = new float[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            if (!float.TryParse(parts[i], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out values[i]))
                return false;
        return true;
    }

    /// <summary>A CSS <c>cubic-bezier()</c> timing function through (0, 0) and (1, 1).</summary>
    /// <param name="x1">First control point x, in [0, 1].</param>
    /// <param name="y1">First control point y.</param>
    /// <param name="x2">Second control point x, in [0, 1].</param>
    /// <param name="y2">Second control point y.</param>
    public static Func<float, float> CubicBezier(float x1, float y1, float x2, float y2) => t =>
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        var u = SolveBezierX(t, x1, x2);
        return Bezier(u, y1, y2);
    };

    static float Bezier(float u, float p1, float p2)
    {
        var inverse = 1f - u;
        return 3f * inverse * inverse * u * p1 + 3f * inverse * u * u * p2 + u * u * u;
    }

    /// <summary>Finds the curve parameter whose x is <paramref name="x"/>; x is monotonic for control x in [0, 1].</summary>
    static float SolveBezierX(float x, float x1, float x2)
    {
        var (low, high, u) = (0f, 1f, x);
        for (var i = 0; i < 24; i++)
        {
            var error = Bezier(u, x1, x2) - x;
            if (Math.Abs(error) < 1e-6f) break;
            if (error > 0f) high = u;
            else low = u;
            u = (low + high) * 0.5f;
        }
        return u;
    }
}
