namespace Guinevere;

/// <summary>
/// Reuses text layouts across passes and frames. Entries live for the frame that used them and the next, so unchanged
/// text never relayouts while text that disappears is dropped without a size limit to tune.
/// </summary>
sealed class TextLayoutCache
{
    Dictionary<TextLayoutRequest, TextLayout> _current = [];
    Dictionary<TextLayoutRequest, TextLayout> _previous = [];

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

        _current[request] = layout;
        return layout;
    }

    /// <summary>Ages entries: those not used during the frame that is ending are dropped at the next swap.</summary>
    internal void NextFrame()
    {
        (_previous, _current) = (_current, _previous);
        _current.Clear();
    }
}
