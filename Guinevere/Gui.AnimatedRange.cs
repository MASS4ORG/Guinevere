using System.Runtime.CompilerServices;

namespace Guinevere;

public partial class Gui
{
    /// <summary>Animates a raw foreground value and holds its ghost after decreases before easing it toward the target.</summary>
    /// <remarks>Increases reset the ghost to the new target; decreases restart the hold, and ReduceMotion snaps both values.</remarks>
    /// <param name="target">A finite value in host units; first use initializes both layers here.</param>
    /// <param name="duration">Foreground transition duration in seconds; zero updates immediately.</param>
    /// <param name="lagDelay">Seconds to hold the ghost after the latest decrease.</param>
    /// <param name="lagDuration">Ghost catch-up duration in seconds; zero snaps after the hold.</param>
    /// <param name="easing">Transition curve, defaulting to the shared sine easing.</param>
    /// <param name="id">Optional shared name, scoped like GetData and AnimateFloat.</param>
    /// <param name="filePath">Compiler-supplied call-site identity.</param>
    /// <param name="lineNumber">Compiler-supplied call-site identity.</param>
    public AnimatedRangeValues AnimatedRange(float target, float duration = 0f, float lagDelay = 0.25f,
        float lagDuration = 0.5f, Func<float, float>? easing = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ValidateRangeArgument(target, nameof(target), duration: false);
        ValidateRangeArgument(duration, nameof(duration));
        ValidateRangeArgument(lagDelay, nameof(lagDelay));
        ValidateRangeArgument(lagDuration, nameof(lagDuration));
        ref var state = ref GetData<LagRange?>(null, id, filePath, lineNumber);
        state ??= new LagRange(target);
        if (state.Frame != _dataFrame)
        {
            state.Step(target, Time.Elapsed, duration, lagDelay, lagDuration, easing ?? Easing.EaseInOutSine, ReduceMotion);
            state.Frame = _dataFrame;
            state.RequestFrames(this, Time.Elapsed);
        }
        return state.Values;
    }

    static void ValidateRangeArgument(float value, string name, bool duration = true)
    {
        if (!float.IsFinite(value) || duration && value < 0f)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}
