namespace Guinevere;

/// <summary>Line geometry backed by shared shaping, or sparse ASCII metrics for very long source lines.</summary>
sealed class EditorLineLayout
{
    readonly TextLayout? _layout;
    readonly AsciiLineLayout? _ascii;

    internal EditorLineLayout(TextLayout layout) => _layout = layout;
    internal EditorLineLayout(AsciiLineLayout ascii) => _ascii = ascii;
    internal float Width => _layout?.Width ?? _ascii!.Width;
    internal Rect CaretBounds(int offset) => _layout?.CaretBounds(offset)
        ?? new Rect(_ascii!.XAt(offset), 0, 1, _ascii.Height);
    internal int OffsetAt(Vector2 point) => _layout?.OffsetAt(point) ?? _ascii!.OffsetAt(point.X);

    internal IReadOnlyList<Rect> RangeBounds(int start, int end)
    {
        if (_layout is { } layout)
        {
            return layout.RangeBounds(start, end);
        }

        var left = _ascii!.XAt(Math.Min(start, end));
        var right = _ascii.XAt(Math.Max(start, end));
        return right > left ? [new Rect(left, 0, right - left, _ascii.Height)] : [];
    }

    internal void Draw(Gui gui, Vector2 origin, Color color, float left, float width)
    {
        if (_layout is { } layout)
        {
            gui.DrawTextLayout(layout, origin, color);
            return;
        }

        var (visible, x) = _ascii!.Visible(left, width);
        gui.DrawTextLayout(visible, origin + new Vector2(x, 0), color);
    }
}

/// <summary>Retains at most eight million source characters and sixteen long-line indexes per editor.</summary>
sealed class EditorLineCache
{
    internal const int LongLineThreshold = 16_384;
    const int Capacity = 8_000_000;
    readonly Dictionary<int, AsciiLineLayout?> _lines = [];
    ITextBuffer? _buffer;
    FontKey _font;
    AsciiFontMetrics? _metrics;
    float _tabSize;
    float _height;
    int _characters;

    /// <summary>Drops source snapshots immediately when the document changes, including loading shorter text.</summary>
    internal void Invalidate()
    {
        _lines.Clear();
        _characters = 0;
        _buffer = null;
    }

    internal AsciiLineLayout? Get(ITextBuffer buffer, int line, FontKey font, float tabSize, float height)
    {
        if (_metrics is null || !font.Equals(_font))
        {
            _font = font;
            _metrics = new AsciiFontMetrics(font.Resolve());
            _buffer = null;
        }

        if (!ReferenceEquals(buffer, _buffer) || tabSize != _tabSize || height != _height)
        {
            _lines.Clear();
            _characters = 0;
            _buffer = buffer;
            _tabSize = tabSize;
            _height = height;
        }

        return GetLine(buffer, line, height);
    }

    AsciiLineLayout? GetLine(ITextBuffer buffer, int line, float height)
    {
        if (_lines.TryGetValue(line, out var cached))
        {
            return cached;
        }

        var text = buffer.Line(line);
        var layout = AsciiLineLayout.Create(text, _metrics!, _tabSize, height);
        if (text.Length <= Capacity)
        {
            if (_characters + text.Length > Capacity || _lines.Count >= 16)
            {
                _lines.Clear();
                _characters = 0;
            }

            _lines[line] = layout;
            _characters += text.Length;
        }

        return layout;
    }
}
