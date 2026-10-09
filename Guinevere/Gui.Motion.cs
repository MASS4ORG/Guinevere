using System.Runtime.CompilerServices;

namespace Guinevere;

public partial class Gui
{
    /// <summary>The <see cref="Smoothdamp(float, float, string?, string, int)"/> speed used when none is given.</summary>
    public const float DefaultSmoothdampSpeed = 20f;

    /// <summary>The longest step, in seconds, a spring advances in one frame.</summary>
    const float MaxMotionStep = 1f / 30f;

    readonly Dictionary<int, SpringMotion> _springs = [];
    readonly Dictionary<int, ChannelTween> _tweens = [];

    /// <summary>
    /// Whether keyed animations, springs, tweens and style transitions jump straight to their targets, for users who
    /// ask the system to reduce motion.
    /// </summary>
    public bool ReduceMotion { get; set; }

    /// <summary>
    /// Follows <paramref name="target"/> with a critically damped spring, keyed by call site, item and data scope like
    /// <see cref="GetData{T}"/>. Frame-rate independent and sampled once per frame, so both passes see one value.
    /// </summary>
    /// <param name="target">The value to follow; the first call starts there.</param>
    /// <param name="speed">Spring frequency in radians per second; higher follows more tightly.</param>
    /// <param name="id">Optional name shared with other call sites in the same data and item context.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>A reference to the smoothed value; writing it moves the spring without changing its target.</returns>
    public ref float Smoothdamp(float target, float speed = DefaultSmoothdampSpeed, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) =>
        ref StepSpring(DataIdentity(id, filePath, lineNumber), [target], speed, 1f).Values[0];

    /// <summary>Follows a vector with a critically damped spring, keyed like the float overload.</summary>
    /// <param name="target">The vector to follow; the first call starts there.</param>
    /// <param name="speed">Spring frequency in radians per second.</param>
    /// <param name="id">Optional shared name.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>The smoothed vector for this frame.</returns>
    public Vector2 Smoothdamp(Vector2 target, float speed = DefaultSmoothdampSpeed, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        var values = StepSpring(DataIdentity(id, filePath, lineNumber), [target.X, target.Y], speed, 1f).Values;
        return new Vector2(values[0], values[1]);
    }

    /// <summary>Follows a color channel by channel with a critically damped spring, keyed like the float overload.</summary>
    /// <param name="target">The color to follow; the first call starts there.</param>
    /// <param name="speed">Spring frequency in radians per second.</param>
    /// <param name="id">Optional shared name.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>The smoothed color for this frame.</returns>
    public Color SmoothdampColor(Color target, float speed = DefaultSmoothdampSpeed, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) =>
        ToColor(StepSpring(DataIdentity(id, filePath, lineNumber), Channels(target), speed, 1f).Values);

    /// <summary>Follows an angle in radians along the shorter way round, keyed like the float overload.</summary>
    /// <param name="target">The angle to follow, in radians.</param>
    /// <param name="speed">Spring frequency in radians per second.</param>
    /// <param name="id">Optional shared name.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>The smoothed angle, which may lie outside one turn.</returns>
    public float SmoothdampAngle(float target, float speed = DefaultSmoothdampSpeed, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        var identity = DataIdentity(id, filePath, lineNumber);
        if (_springs.TryGetValue(identity, out var state) && state.Values.Length == 1)
            target = state.Values[0] + MathF.IEEERemainder(target - state.Values[0], MathF.Tau);
        return StepSpring(identity, [target], speed, 1f).Values[0];
    }

    /// <summary>Follows <paramref name="target"/> with a configurable damped spring, keyed like Smoothdamp.</summary>
    /// <param name="target">The value to follow; the first call starts there.</param>
    /// <param name="frequency">Angular frequency in radians per second.</param>
    /// <param name="dampingRatio">One is critically damped; below one overshoots and oscillates.</param>
    /// <param name="id">Optional shared name.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>The spring's value for this frame.</returns>
    public float AnimateSpring(float target, float frequency, float dampingRatio, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) =>
        StepSpring(DataIdentity(id, filePath, lineNumber), [target], frequency, dampingRatio).Values[0];

    /// <summary>
    /// Eases toward <paramref name="target"/> over <paramref name="duration"/> seconds whenever it changes, keyed
    /// like Smoothdamp. A change mid-way restarts from the value shown, so the motion stays continuous.
    /// </summary>
    /// <param name="target">The value to reach; the first call starts there.</param>
    /// <param name="duration">Seconds per transition; zero jumps.</param>
    /// <param name="easing">The easing function; defaults to <see cref="Easing.EaseInOutSine"/>.</param>
    /// <param name="id">Optional shared name.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>The eased value for this frame.</returns>
    public float AnimateFloat(float target, float duration, Func<float, float>? easing = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) =>
        StepTween(DataIdentity(id, filePath, lineNumber), [target], duration, easing).Current[0];

    /// <summary>Eases toward a color whenever it changes, keyed and continuous like AnimateFloat.</summary>
    /// <param name="target">The color to reach; the first call starts there.</param>
    /// <param name="duration">Seconds per transition; zero jumps.</param>
    /// <param name="easing">The easing function; defaults to <see cref="Easing.EaseInOutSine"/>.</param>
    /// <param name="id">Optional shared name.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>The eased color for this frame.</returns>
    public Color AnimateColor(Color target, float duration, Func<float, float>? easing = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) =>
        ToColor(StepTween(DataIdentity(id, filePath, lineNumber), Channels(target), duration, easing).Current);

    SpringMotion StepSpring(int identity, ReadOnlySpan<float> target, float frequency, float dampingRatio)
    {
        if (!_springs.TryGetValue(identity, out var state) || state.Values.Length != target.Length)
        {
            _springs[identity] = state = new SpringMotion(target.ToArray(), new float[target.Length]);
            state.Frame = _dataFrame;
            return state;
        }
        if (state.Frame == _dataFrame) return state;
        state.Frame = _dataFrame;
        // A frame after an idle wait reports the whole wait; stepping by it would skip motion the input just started.
        var deltaTime = Math.Min(Time.DeltaTime, MaxMotionStep);
        var settled = true;
        for (var i = 0; i < target.Length; i++)
        {
            if (ReduceMotion) (state.Values[i], state.Velocities[i]) = (target[i], 0f);
            else settled &= Spring.Step(ref state.Values[i], ref state.Velocities[i], target[i], deltaTime, frequency,
                dampingRatio);
        }
        if (!settled) RequestFrame();
        return state;
    }

    ChannelTween StepTween(int identity, ReadOnlySpan<float> target, float duration, Func<float, float>? easing)
    {
        if (!_tweens.TryGetValue(identity, out var tween) || tween.Channels != target.Length)
        {
            _tweens[identity] = tween = new ChannelTween(target.Length);
            tween.Snap(target);
        }
        if (tween.Frame == _dataFrame) return tween;
        tween.Frame = _dataFrame;
        tween.Retarget(target, Time.Elapsed, ReduceMotion ? 0f : duration, easing ?? Easing.EaseInOutSine);
        tween.Sample(Time.Elapsed);
        if (tween.Running) RequestFrame();
        return tween;
    }

    static float[] Channels(Color color) => [color.R, color.G, color.B, color.A];

    static Color ToColor(ReadOnlySpan<float> channels) => Color.FromArgb(Channel(channels[3]), Channel(channels[0]),
        Channel(channels[1]), Channel(channels[2]));

    static int Channel(float value) => (int)Math.Clamp(MathF.Round(value), 0f, 255f);

    sealed class SpringMotion(float[] values, float[] velocities)
    {
        internal readonly float[] Values = values;
        internal readonly float[] Velocities = velocities;
        internal ulong Frame;
    }
}
