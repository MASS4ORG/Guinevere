using System.Runtime.CompilerServices;

namespace Guinevere;

public partial class Gui
{
    AnimationManager? _animationManager;
    readonly Dictionary<int, ScopedBoolAnimation> _scopedAnimations = new();

    AnimationManager AnimationManager => _animationManager ??= new AnimationManager(Time);

    /// <summary>
    /// Gets a new AnimationFloat instance with the specified initial value.
    /// Each call creates a new independent animation instance.
    /// </summary>
    /// <param name="initialValue">The initial value of the animation. Default is 0.</param>
    /// <returns>A new AnimationFloat instance.</returns>
    [PublicAPI]
    public AnimationFloat GetAnimationFloat(float initialValue = 0f)
    {
        return new AnimationFloat(initialValue, Time);
    }

    /// <summary>
    /// Animates a boolean using shared node, item and data identity, sampling once per frame in both passes.
    /// Calls outside a frame retain caller-location identity.
    /// </summary>
    /// <param name="targetState">The target boolean state to animate towards.</param>
    /// <param name="duration">The duration of the animation in seconds.</param>
    /// <param name="easingFunction">The easing function to use for the animation.</param>
    /// <param name="callerFilePath">Automatically provided caller file path.</param>
    /// <param name="callerLineNumber">Automatically provided caller line number.</param>
    /// <returns>The current animated value between 0.0 and 1.0.</returns>
    [PublicAPI]
    public float AnimateBool01(
        bool targetState,
        float duration,
        Func<float, float> easingFunction,
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        if (Canvas is null)
            return AnimationManager.AnimateBool01(targetState, duration, easingFunction, callerFilePath, callerLineNumber);
        return AnimateScopedBool(DataIdentity(null, callerFilePath, callerLineNumber),
            targetState, duration, easingFunction);
    }

    /// <summary>Animates a boolean shared by a named ID within the current data and item context.</summary>
    public float AnimateBool01(string id, bool targetState, float duration, Func<float, float> easingFunction) =>
        AnimateScopedBool(DataIdentity(id, "", 0), targetState, duration, easingFunction);

    float AnimateScopedBool(int identity, bool target, float duration, Func<float, float> easing)
    {
        ArgumentNullException.ThrowIfNull(easing);
        if (!_scopedAnimations.TryGetValue(identity, out var state))
            _scopedAnimations.Add(identity, state = new ScopedBoolAnimation(new AnimationFloat(target ? 1f : 0f, Time)));
        if (state.Frame != _dataFrame)
        {
            if (state.Animation.TargetValue != (target ? 1f : 0f))
                state.Animation.AnimateTo(target ? 1f : 0f, duration, easing);
            state.Value = state.Animation.GetValue();
            state.Frame = _dataFrame;
        }
        return state.Value;
    }

    sealed class ScopedBoolAnimation(AnimationFloat animation)
    {
        internal readonly AnimationFloat Animation = animation;
        internal ulong Frame = ulong.MaxValue;
        internal float Value;
    }

    /// <summary>
    /// Gets the total number of active boolean animations being managed.
    /// </summary>
    [PublicAPI]
    public int ActiveAnimationCount => (_animationManager?.ActiveAnimationCount ?? 0) + _scopedAnimations.Count;

    /// <summary>
    /// Gets the number of currently running boolean animations.
    /// </summary>
    [PublicAPI]
    public int RunningAnimationCount => (_animationManager?.RunningAnimationCount ?? 0)
        + _scopedAnimations.Values.Count(state => state.Animation.IsAnimating);

    /// <summary>
    /// Clears all animation instances. This should typically be called
    /// when resetting the GUI state or when cleaning up.
    /// </summary>
    [PublicAPI]
    public void ClearAnimations()
    {
        _animationManager?.Clear();
        _scopedAnimations.Clear();
    }
}
