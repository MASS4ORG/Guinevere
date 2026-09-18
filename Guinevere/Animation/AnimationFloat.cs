namespace Guinevere;

/// <summary>
/// Represents an animated floating-point value that can smoothly transition between states
/// using easing functions over time.
/// </summary>
public class AnimationFloat
{
    float _currentValue;
    float _targetValue;
    float _startValue;
    float _duration;
    float _startTime;
    Func<float, float> _easingFunction;
    bool _isAnimating;
    readonly Time _time;

    /// <summary>
    /// Initializes a new instance of the AnimationFloat class with the specified initial value and time reference.
    /// </summary>
    /// <param name="initialValue">The initial value of the animation.</param>
    /// <param name="time">The time instance to use for tracking elapsed time.</param>
    public AnimationFloat(float initialValue, Time time)
    {
        _currentValue = initialValue;
        _targetValue = initialValue;
        _startValue = initialValue;
        _duration = 0f;
        _startTime = 0f;
        _easingFunction = Easing.Linear;
        _isAnimating = false;
        _time = time;
    }

    /// <summary>
    /// Gets the current animated value, automatically updating based on elapsed time.
    /// </summary>
    /// <returns>The current interpolated value based on the animation progress.</returns>
    public float GetValue()
    {
        if (_isAnimating)
        {
            var elapsedTime = _time.Elapsed - _startTime;

            if (elapsedTime >= _duration)
            {
                _currentValue = _targetValue;
                _isAnimating = false;
            }
            else
            {
                var normalizedTime = elapsedTime / _duration;
                var easedTime = _easingFunction(normalizedTime);
                _currentValue = ImMath.Lerp(_startValue, _targetValue, easedTime);
            }
        }

        return _currentValue;
    }

    /// <summary>
    /// Gets the target value that the animation is moving towards.
    /// </summary>
    public float TargetValue => _targetValue;

    /// <summary>
    /// Gets a value indicating whether the animation is currently running.
    /// </summary>
    public bool IsAnimating => _isAnimating && _time.Elapsed - _startTime < _duration;

    /// <summary>
    /// Starts an animation to the specified target value.
    /// </summary>
    /// <param name="targetValue">The target value to animate to.</param>
    /// <param name="duration">The duration of the animation in seconds.</param>
    /// <param name="easingFunction">The easing function to use for the animation.</param>
    public void AnimateTo(float targetValue, float duration, Func<float, float> easingFunction)
    {
        if (duration <= 0f)
        {
            SetValue(targetValue);
            return;
        }

        _startValue = GetValue(); // Get current value to start from current position
        _targetValue = targetValue;
        _duration = duration;
        _startTime = _time.Elapsed;
        _easingFunction = easingFunction;
        _isAnimating = true;
    }

    /// <summary>
    /// Immediately sets the value without animation.
    /// </summary>
    /// <param name="value">The value to set.</param>
    public void SetValue(float value)
    {
        _currentValue = value;
        _targetValue = value;
        _startValue = value;
        _isAnimating = false;
    }

    /// <summary>
    /// Implicitly converts an AnimationFloat to a float value.
    /// </summary>
    /// <param name="animationFloat">The AnimationFloat to convert.</param>
    /// <returns>The current value of the animation.</returns>
    public static implicit operator float(AnimationFloat animationFloat) => animationFloat.GetValue();

    /// <summary>
    /// Compares the current value with another float value.
    /// </summary>
    /// <param name="animationFloat"></param>
    /// <param name="value">The value to compare with.</param>
    /// <returns>True if the current value is greater than the specified value; otherwise, false.</returns>
    public static bool operator >(AnimationFloat animationFloat, float value) => animationFloat.GetValue() > value;

    /// <summary>
    /// Compares the current value with another float value.
    /// </summary>
    /// <param name="animationFloat"></param>
    /// <param name="value">The value to compare with.</param>
    /// <returns>True if the current value is less than the specified value; otherwise, false.</returns>
    public static bool operator <(AnimationFloat animationFloat, float value) => animationFloat.GetValue() < value;

    /// <summary>
    /// Compares the current value with another float value.
    /// </summary>
    /// <param name="animationFloat"></param>
    /// <param name="value">The value to compare with.</param>
    /// <returns>True if the current value is greater than or equal to the specified value; otherwise, false.</returns>
    public static bool operator >=(AnimationFloat animationFloat, float value) => animationFloat.GetValue() >= value;

    /// <summary>
    /// Compares the current value with another float value.
    /// </summary>
    /// <param name="animationFloat"></param>
    /// <param name="value">The value to compare with.</param>
    /// <returns>True if the current value is less than or equal to the specified value; otherwise, false.</returns>
    public static bool operator <=(AnimationFloat animationFloat, float value) => animationFloat.GetValue() <= value;
}
