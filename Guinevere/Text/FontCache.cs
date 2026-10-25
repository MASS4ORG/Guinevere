namespace Guinevere;

/// <summary>
/// Reuses native fonts within two bounded admission generations. Eviction drops references; borrowed wrappers and draw
/// commands retain their native font independently, so native finalization happens after the last user releases it.
/// </summary>
sealed class FontCache
{
    internal const int MaxEntries = 1024;

    Dictionary<(FontKey.Face Face, float Size), SKFont> _current = [];
    Dictionary<(FontKey.Face Face, float Size), SKFont> _previous = [];

    internal int Builds { get; private set; }
    internal int Count => _current.Count + _previous.Count;

    internal SKFont Get(FontKey.Face face, float size)
    {
        var key = (face, size);
        if (_current.TryGetValue(key, out var font))
        {
            return font;
        }

        if (!_previous.Remove(key, out font))
        {
            font = face.Create(size);
            Builds++;
        }

        if (_current.Count == MaxEntries)
        {
            (_previous, _current) = (_current, _previous);
            _current.Clear();
        }

        _current.Add(key, font);
        return font;
    }
}
