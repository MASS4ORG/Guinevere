namespace Guinevere;

/// <summary>Owns a reusable ordered filter chain and its native paint; dispose after its last frame.</summary>
public sealed class RenderEffectStack : IDisposable
{
    RenderEffect[] _effects;
    readonly List<string> _diagnostics = [];
    SKPaint? _paint;
    SKImageFilter? _filter;
    bool? _runtime;
    bool _renderTargets;
    bool _disposed;

    internal LayoutNodeScopeRenderEffects ScopeValue { get; }

    /// <summary>Copies the effect sequence so a stack remains stable across frames.</summary>
    public RenderEffectStack(params RenderEffect[] effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ScopeValue = new LayoutNodeScopeRenderEffects { Value = this };
        _effects = (RenderEffect[])effects.Clone();
        foreach (var effect in _effects)
        {
            ArgumentNullException.ThrowIfNull(effect);
        }
    }

    /// <summary>Unsupported effects and compilation failures from the latest preparation.</summary>
    public IReadOnlyList<string> Diagnostics => _diagnostics;

    /// <summary>Replaces parameters for animation, retaining native resources when the sequence is equal.</summary>
    public void Update(ReadOnlySpan<RenderEffect> effects)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (effects.SequenceEqual(_effects))
        {
            return;
        }
        foreach (var effect in effects)
        {
            ArgumentNullException.ThrowIfNull(effect);
        }
        _effects = effects.ToArray();
        Release();
    }

    internal SKPaint Paint(bool runtime, float opacity, bool renderTargets = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_paint is null || _runtime != runtime || _renderTargets != renderTargets)
        {
            Prepare(runtime, renderTargets);
        }
        _paint!.Color = new SKColor(255, 255, 255, (byte)(opacity * 255f + 0.5f));
        return _paint;
    }

    void Prepare(bool runtime, bool renderTargets)
    {
        Release();
        _diagnostics.Clear();
        _runtime = runtime;
        _renderTargets = renderTargets;
        if (!renderTargets)
        {
            _diagnostics.Add("The renderer does not support offscreen render targets.");
        }
        foreach (var effect in renderTargets ? _effects : [])
        {
            var next = RenderEffectFilters.Create(effect, _filter, runtime, _diagnostics);
            if (!ReferenceEquals(next, _filter))
            {
                _filter?.Dispose();
                _filter = next;
            }
        }
        _paint = new SKPaint { ImageFilter = _filter };
    }

    void Release()
    {
        _paint?.Dispose();
        _filter?.Dispose();
        _paint = null;
        _filter = null;
    }

    /// <summary>Releases the native filter chain and paint.</summary>
    public void Dispose()
    {
        Release();
        _disposed = true;
    }
}
