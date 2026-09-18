using System.Runtime.CompilerServices;

namespace Guinevere;

/// <summary>
/// Manages animation instances and provides automatic ID generation for boolean animations
/// based on caller location to enable seamless immediate-mode GUI animation usage.
/// </summary>
public class AnimationManager
{
    readonly Dictionary<string, BoolAnimationState> _boolAnimations = new();
    readonly Time _time;

    /// <summary>
    /// Represents the state of a boolean animation including its direction and timing.
    /// </summary>
    class BoolAnimationState
    {
        public AnimationFloat Animation { get; }
        public bool LastTargetState { get; set; }
        [UsedImplicitly] public float Duration { get; set; }
        [UsedImplicitly] public Func<float, float> EasingFunction { get; set; }

        public BoolAnimationState(AnimationFloat animation, bool initialState, float duration,
            Func<float, float> easingFunction)
        {
            Animation = animation;
            LastTargetState = initialState;
            Duration = duration;
            EasingFunction = easingFunction;
        }
    }

    /// <summary>
    /// Initializes a new instance of the AnimationManager with the specified time reference.
    /// </summary>
    /// <param name="time">The time instance to use for all managed animations.</param>
    public AnimationManager(Time time)
    {
        _time = time;
    }

    /// <summary>
    /// Animates a boolean value to a float between 0.0 and 1.0 with automatic ID generation
    /// based on the caller's file path and line number.
    /// </summary>
    /// <param name="targetState">The target boolean state to animate towards.</param>
    /// <param name="duration">The duration of the animation in seconds.</param>
    /// <param name="easingFunction">The easing function to use for the animation.</param>
    /// <param name="callerFilePath">Automatically provided caller file path.</param>
    /// <param name="callerLineNumber">Automatically provided caller line number.</param>
    /// <returns>The current animated value between 0.0 and 1.0.</returns>
    public float AnimateBool01(
        bool targetState,
        float duration,
        Func<float, float> easingFunction,
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        var id = $"{callerFilePath}:{callerLineNumber}";

        if (!_boolAnimations.TryGetValue(id, out var animationState))
        {
            // Create new animation starting from the target state (no initial animation)
            var initialValue = targetState ? 1f : 0f;
            var animation = new AnimationFloat(initialValue, _time);
            animationState = new BoolAnimationState(animation, targetState, duration, easingFunction);
            _boolAnimations[id] = animationState;
        }

        // Check if the target state has changed
        if (animationState.LastTargetState != targetState)
        {
            animationState.LastTargetState = targetState;
            animationState.Duration = duration;
            animationState.EasingFunction = easingFunction;

            var targetValue = targetState ? 1f : 0f;
            animationState.Animation.AnimateTo(targetValue, duration, easingFunction);
        }

        return animationState.Animation.GetValue();
    }

    /// <summary>
    /// Clears all animation instances. This should typically be called
    /// when resetting the GUI state or when cleaning up.
    /// </summary>
    public void Clear()
    {
        _boolAnimations.Clear();
    }

    /// <summary>
    /// Gets the total number of active boolean animations being managed.
    /// </summary>
    public int ActiveAnimationCount => _boolAnimations.Count;

    /// <summary>
    /// Gets the number of currently running boolean animations.
    /// </summary>
    public int RunningAnimationCount => _boolAnimations.Values.Count(state => state.Animation.IsAnimating);
}
