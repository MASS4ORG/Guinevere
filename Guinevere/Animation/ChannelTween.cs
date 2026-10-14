namespace Guinevere;

/// <summary>
/// An eased transition over a fixed number of float channels. Retargeting restarts from the value it currently shows,
/// so an interrupted transition continues without a jump. Shared by keyed GUI tweens and style transitions.
/// </summary>
sealed class ChannelTween(int channels)
{
    readonly float[] _from = new float[channels];
    readonly float[] _to = new float[channels];
    readonly float[] _current = new float[channels];
    float _start;
    float _duration;
    Func<float, float> _easing = Easing.Linear;

    /// <summary>The frame that last sampled this tween, so every pass of a frame sees one value.</summary>
    internal ulong Frame = ulong.MaxValue;

    /// <summary>Whether the transition has not yet reached its target.</summary>
    internal bool Running { get; private set; }

    /// <summary>The number of channels.</summary>
    internal int Channels => _current.Length;

    /// <summary>The value shown by the latest sample.</summary>
    internal ReadOnlySpan<float> Current => _current;

    /// <summary>Jumps to <paramref name="value"/> and stops.</summary>
    internal void Snap(ReadOnlySpan<float> value)
    {
        value.CopyTo(_from);
        value.CopyTo(_to);
        value.CopyTo(_current);
        Running = false;
    }

    /// <summary>Starts toward <paramref name="target"/> from the current value unless it is already the target.</summary>
    /// <returns>Whether the target changed.</returns>
    internal bool Retarget(ReadOnlySpan<float> target, float now, float duration, Func<float, float> easing)
    {
        if (target.SequenceEqual(_to))
        {
            return false;
        }

        if (duration <= 0f)
        {
            Snap(target);
            return true;
        }
        _current.CopyTo(_from);
        target.CopyTo(_to);
        (_start, _duration, _easing, Running) = (now, duration, easing, true);
        return true;
    }

    /// <summary>Updates <see cref="Current"/> for the time <paramref name="now"/>.</summary>
    internal void Sample(float now)
    {
        if (!Running)
        {
            return;
        }

        var progress = (now - _start) / _duration;
        if (progress >= 1f)
        {
            _to.CopyTo(_current);
            Running = false;
            return;
        }
        var eased = _easing(Math.Max(0f, progress));
        for (var i = 0; i < _current.Length; i++)
        {
            _current[i] = _from[i] + (_to[i] - _from[i]) * eased;
        }
    }
}
