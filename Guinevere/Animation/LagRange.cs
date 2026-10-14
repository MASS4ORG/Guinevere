namespace Guinevere;

sealed class LagRange
{
    readonly ChannelTween _value = new(1);
    readonly ChannelTween _ghost = new(1);
    float _target;
    float _holdUntil;
    bool _pending;

    internal ulong Frame = ulong.MaxValue;

    internal LagRange(float target)
    {
        _target = target;
        Snap(target);
    }

    internal AnimatedRangeValues Values => new(_value.Current[0], Math.Max(_value.Current[0], _ghost.Current[0]));

    internal void Step(float target, float now, float duration, float lagDelay, float lagDuration,
        Func<float, float> easing, bool reduceMotion)
    {
        if (reduceMotion)
        {
            _target = target;
            Snap(target);
            return;
        }
        _value.Sample(now);
        _ghost.Sample(now);
        AdvanceGhost(now, lagDuration, easing);
        if (target != _target)
        {
            Retarget(target, now, duration, lagDelay, easing);
        }
        AdvanceGhost(now, lagDuration, easing);
    }

    void AdvanceGhost(float now, float lagDuration, Func<float, float> easing)
    {
        if (_pending && now >= _holdUntil)
        {
            _ghost.Retarget([_target], _holdUntil, lagDuration, easing);
            _ghost.Sample(now);
            _pending = false;
        }
    }

    void Retarget(float target, float now, float duration, float lagDelay, Func<float, float> easing)
    {
        _value.Retarget([target], now, duration, easing);
        if (target < _target)
        {
            _ghost.Snap([Math.Max(_ghost.Current[0], _value.Current[0])]);
            _holdUntil = now + lagDelay;
            _pending = true;
        }
        else
        {
            _ghost.Snap([target]);
            _pending = false;
        }
        _target = target;
    }

    void Snap(float target)
    {
        _value.Snap([target]);
        _ghost.Snap([target]);
        _pending = false;
    }

    internal void RequestFrames(Gui gui, float now)
    {
        if (_value.Running || _ghost.Running)
        {
            gui.RequestFrame();
        }
        if (_pending)
        {
            gui.RequestFrameIn(_holdUntil - now);
        }
    }
}
