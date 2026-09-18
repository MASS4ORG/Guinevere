using System.Runtime.CompilerServices;

namespace Guinevere;

public partial class Gui
{
    AnimationManager? _animationManager;

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
    /// Animates a boolean value to a float between 0.0 and 1.0 with automatic ID generation
    /// based on the caller's file path and line number. This method both starts and processes
    /// the animation on each call, maintaining state internally.
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
        return AnimationManager.AnimateBool01(targetState, duration, easingFunction, callerFilePath, callerLineNumber);
    }

    /// <summary>
    /// Gets the total number of active boolean animations being managed.
    /// </summary>
    [PublicAPI]
    public int ActiveAnimationCount => _animationManager?.ActiveAnimationCount ?? 0;

    /// <summary>
    /// Gets the number of currently running boolean animations.
    /// </summary>
    [PublicAPI]
    public int RunningAnimationCount => _animationManager?.RunningAnimationCount ?? 0;

    /// <summary>
    /// Clears all animation instances. This should typically be called
    /// when resetting the GUI state or when cleaning up.
    /// </summary>
    [PublicAPI]
    public void ClearAnimations()
    {
        _animationManager?.Clear();
    }
}
