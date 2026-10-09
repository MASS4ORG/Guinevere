namespace Guinevere;

/// <summary>
/// Interpolates a styled node's properties when their values change under a transition annotation. Entering a state
/// uses the new rule's annotation and leaving it the old one's, so a hover eases in and back out; an interrupted
/// transition restarts from the value shown. Each node keeps one track per pass, advanced on the GUI clock.
/// </summary>
sealed class StyleTransitions
{
    readonly Dictionary<(int Node, bool Render), Track> _tracks = [];
    readonly List<string> _finished = [];
    ulong _sweptFrame;

    /// <summary>Number of nodes whose last style is tracked.</summary>
    internal int TrackedCount => _tracks.Count;

    /// <summary>Returns <paramref name="style"/>, or a copy with the properties still in transition interpolated.</summary>
    internal ResolvedStyle Apply(Gui gui, LayoutNode node, ResolvedStyle style)
    {
        var frame = gui.Time.Frames;
        if (frame != _sweptFrame) Sweep(frame);
        var key = (node.Identity, gui.Pass == Pass.Pass2Render);
        if (!_tracks.TryGetValue(key, out var track))
        {
            _tracks.Add(key, new Track(style) { Frame = frame });
            return style;
        }

        track.Frame = frame;
        if (ReferenceEquals(track.Target, style) && track.Properties.Count == 0) return style;
        Retarget(track, style, gui.Time.Elapsed, gui.ReduceMotion);
        track.Target = style;
        return track.Properties.Count == 0 ? style : Blend(gui, track, style);
    }

    /// <summary>Forgets nodes that were not styled in the previous frame.</summary>
    void Sweep(ulong frame)
    {
        _sweptFrame = frame;
        foreach (var (key, track) in _tracks)
            if (track.Frame + 1 < frame) _tracks.Remove(key);
    }

    static void Retarget(Track track, ResolvedStyle style, float now, bool reduceMotion)
    {
        foreach (var (property, value) in style.Declarations)
            Retarget(track, style, property, value, now, reduceMotion);
        foreach (var property in track.Properties.Keys)
            if (!style.Has(property)) track.Properties.Remove(property);
    }

    /// <summary>Starts, redirects or drops one property's transition when its target value changes.</summary>
    static void Retarget(Track track, ResolvedStyle style, string property, string value, float now,
        bool reduceMotion)
    {
        var previous = track.Target.Get(property);
        track.Properties.TryGetValue(property, out var running);
        if (value == (running?.TargetText ?? previous)) return;
        var transition = reduceMotion ? null : style.GetTransition(property) ?? track.Target.GetTransition(property);
        if (!TryAnimate(track, property, running, previous, value, transition, now))
            track.Properties.Remove(property);
    }

    static bool TryAnimate(Track track, string property, PropertyTransition? running, string? previous, string value,
        StyleTransition? transition, float now) =>
        transition is { Duration: > 0f } && StyleValueChannels.TryParse(value, out var shape, out var to)
        && (Redirect(running, shape, to, value, transition, now)
            || Start(track, property, running, previous, shape, to, value, transition, now));

    /// <summary>Points a running transition at a new value of the same shape.</summary>
    static bool Redirect(PropertyTransition? running, string shape, float[] to, string value,
        StyleTransition transition, float now)
    {
        if (running is null || running.Shape != shape || running.Tween.Channels != to.Length) return false;
        running.Tween.Retarget(to, now, transition.Duration, transition.Easing);
        running.TargetText = value;
        return true;
    }

    /// <summary>Starts a transition from the value shown; false when it and the new value differ in shape.</summary>
    static bool Start(Track track, string property, PropertyTransition? running, string? previous, string shape,
        float[] to, string value, StyleTransition transition, float now)
    {
        var shown = running is null ? previous : StyleValueChannels.Format(running.Shape, running.Tween.Current);
        if (shown is null || !StyleValueChannels.TryParse(shown, out var fromShape, out var from)
            || fromShape != shape || from.Length != to.Length)
            return false;
        var tween = new ChannelTween(to.Length);
        tween.Snap(from);
        tween.Retarget(to, now, transition.Duration, transition.Easing);
        track.Properties[property] = new PropertyTransition(shape, tween) { TargetText = value };
        return true;
    }

    ResolvedStyle Blend(Gui gui, Track track, ResolvedStyle style)
    {
        var now = gui.Time.Elapsed;
        var merged = new Dictionary<string, string>(style.DeclarationMap, StringComparer.Ordinal);
        _finished.Clear();
        foreach (var (property, transition) in track.Properties)
        {
            transition.Tween.Sample(now);
            if (transition.Tween.Running)
                merged[property] = StyleValueChannels.Format(transition.Shape, transition.Tween.Current);
            else _finished.Add(property);
        }
        foreach (var property in _finished) track.Properties.Remove(property);
        if (track.Properties.Count > 0) gui.RequestFrame();
        return new ResolvedStyle(merged, style.Bases) { Locals = style.Locals, Transitions = style.Transitions };
    }

    sealed class Track(ResolvedStyle target)
    {
        internal ResolvedStyle Target = target;
        internal readonly Dictionary<string, PropertyTransition> Properties = new(StringComparer.Ordinal);
        internal ulong Frame;
    }

    sealed class PropertyTransition(string shape, ChannelTween tween)
    {
        internal readonly string Shape = shape;
        internal readonly ChannelTween Tween = tween;
        internal required string TargetText;
    }
}
