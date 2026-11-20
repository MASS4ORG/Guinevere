namespace Guinevere;

/// <summary>
/// Reuses text layouts across passes and frames within two bounded generations. Eviction releases cache references;
/// retained layouts and queued draw commands keep their fonts and glyph blobs alive independently.
/// </summary>
sealed class TextLayoutCache
{
    internal const int MaxEntries = 4096;
    internal const long MaxWeight = 524288;

    Dictionary<TextLayoutRequest, TextLayout> _current = new(TextLayoutRequestComparer.Instance);
    Dictionary<TextLayoutRequest, TextLayout> _previous = new(TextLayoutRequestComparer.Instance);
    long _currentWeight;

    /// <summary>Number of layouts built rather than reused.</summary>
    internal int Builds { get; private set; }

    internal TextLayout Get(TextLayoutRequest request, FontRegistry fonts, FontCache? cache = null)
    {
        if (_current.TryGetValue(request, out var layout))
        {
            return layout;
        }

        if (!_previous.Remove(request, out layout))
        {
            layout = TextLayoutEngine.Create(request, fonts, cache);
            Builds++;
        }

        Admit(request, layout, request.Weight);
        return layout;
    }

    /// <summary>
    /// Looks plain text up by its characters; a hit creates no string, and a miss creates the one string the layout and
    /// its cache entry keep.
    /// </summary>
    internal TextLayout Get(PlainTextRequest plain, FontRegistry fonts, FontCache? cache = null)
    {
        if (_current.GetAlternateLookup<PlainTextRequest>().TryGetValue(plain, out var layout))
        {
            return layout;
        }

        if (_previous.GetAlternateLookup<PlainTextRequest>().Remove(plain, out var request, out layout))
        {
            Admit(request, layout, plain.Weight);
            return layout;
        }

        request = plain.ToRequest();
        layout = TextLayoutEngine.Create(request, fonts, cache);
        Builds++;
        Admit(request, layout, plain.Weight);
        return layout;
    }

    /// <summary>Ages entries: those not used during the frame that is ending are dropped at the next swap.</summary>
    internal void NextFrame()
    {
        (_previous, _current) = (_current, _previous);
        _current.Clear();
        _currentWeight = 0;
    }

    void Admit(TextLayoutRequest request, TextLayout layout, long weight)
    {
        if (_current.Count < MaxEntries && weight <= MaxWeight - _currentWeight)
        {
            _current[request] = layout;
            _currentWeight += weight;
        }
    }
}
