namespace Guinevere;

/// <summary>
/// Reuses text layouts across passes and frames within two bounded generations. Eviction releases cache references;
/// retained layouts and queued draw commands keep their fonts and glyph blobs alive independently.
/// </summary>
sealed class TextLayoutCache
{
    internal const int MaxEntries = 4096;
    internal const long MaxWeight = 524288;

    Dictionary<TextLayoutRequest, TextLayout> _current = [];
    Dictionary<TextLayoutRequest, TextLayout> _previous = [];
    long _currentWeight;

    /// <summary>Number of layouts built rather than reused.</summary>
    internal int Builds { get; private set; }

    internal TextLayout Get(TextLayoutRequest request, FontRegistry fonts)
    {
        if (_current.TryGetValue(request, out var layout))
        {
            return layout;
        }

        if (!_previous.Remove(request, out layout))
        {
            layout = TextLayoutEngine.Create(request, fonts);
            Builds++;
        }

        var weight = request.Weight;
        if (_current.Count < MaxEntries && weight <= MaxWeight - _currentWeight)
        {
            _current[request] = layout;
            _currentWeight += weight;
        }

        return layout;
    }

    /// <summary>Ages entries: those not used during the frame that is ending are dropped at the next swap.</summary>
    internal void NextFrame()
    {
        (_previous, _current) = (_current, _previous);
        _current.Clear();
        _currentWeight = 0;
    }
}
