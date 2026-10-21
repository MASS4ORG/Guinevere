namespace Guinevere;

/// <summary>Basic-shaper advances for ASCII, including native widths and pair kerning.</summary>
sealed class AsciiFontMetrics
{
    internal Font Font { get; }
    internal ushort[] Glyphs { get; }
    internal float[] Widths { get; }
    readonly float[] _pairs = new float[128 * 128];

    internal AsciiFontMetrics(Font font)
    {
        Font = font;
        var alphabet = new string([.. Enumerable.Range(0, 128).Select(value => (char)value)]);
        Glyphs = font.SkFont.GetGlyphs(alphabet);
        Widths = font.SkFont.GetGlyphWidths(Glyphs, null);
        var kerning = OpenTypeKerning.For(font.SkFont.Typeface);
        if (kerning.HasKerning)
        {
            var scale = font.Size / Math.Max(1, font.SkFont.Typeface.UnitsPerEm);
            for (var left = 32; left < 127; left++)
            {
                for (var right = 0; right < 128; right++)
                {
                    _pairs[left * 128 + right] = kerning.Adjustment(Glyphs[left], Glyphs[right]) * scale;
                }
            }
        }
    }

    internal bool Supports(char value) => value == '\t'
        || (value is >= ' ' and <= '~' && Glyphs[value] != 0);

    internal float Advance(char value, char next) => Widths[value] + _pairs[value * 128 + next];
}

/// <summary>Exact ASCII advances indexed every 256 characters, with glyphs created only for the viewport.</summary>
sealed class AsciiLineLayout
{
    const int Stride = 256;
    readonly string _text;
    readonly AsciiFontMetrics _metrics;
    readonly float[] _checkpoints;
    readonly float _tabInterval;

    internal float Width { get; }
    internal float Height { get; }

    AsciiLineLayout(string text, AsciiFontMetrics metrics, float tabSize, float height)
    {
        _text = text;
        _metrics = metrics;
        Height = height;
        _tabInterval = metrics.Widths[' '] * tabSize;
        _checkpoints = new float[text.Length / Stride + 1];
        var x = 0f;
        for (var i = 0; i < text.Length; i++)
        {
            if (i % Stride == 0)
            {
                _checkpoints[i / Stride] = x;
            }

            x = NextX(i, x);
        }

        if (text.Length % Stride == 0)
        {
            _checkpoints[^1] = x;
        }

        Width = x;
    }

    internal static AsciiLineLayout? Create(string text, AsciiFontMetrics metrics, float tabSize, float height)
    {
        foreach (var value in text)
        {
            if (!metrics.Supports(value))
            {
                return null;
            }
        }

        return new AsciiLineLayout(text, metrics, tabSize, height);
    }

    float NextX(int offset, float x)
    {
        if (_text[offset] == '\t')
        {
            return _tabInterval <= 0 ? x : (MathF.Floor((x + 0.01f) / _tabInterval) + 1f) * _tabInterval;
        }

        var advance = offset + 1 < _text.Length
            ? _metrics.Advance(_text[offset], _text[offset + 1]) : _metrics.Widths[_text[offset]];
        return x + advance;
    }

    internal float XAt(int offset)
    {
        offset = Math.Clamp(offset, 0, _text.Length);
        var block = offset / Stride;
        var x = _checkpoints[block];
        for (var i = block * Stride; i < offset; i++)
        {
            x = NextX(i, x);
        }

        return x;
    }

    internal int OffsetAt(float x)
    {
        var low = 0;
        var high = _checkpoints.Length - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (_checkpoints[middle] <= x)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        var position = _checkpoints[low];
        var offset = low * Stride;
        while (offset < _text.Length)
        {
            var next = NextX(offset, position);
            if (x < (position + next) * 0.5f)
            {
                break;
            }

            position = next;
            offset++;
        }

        return offset;
    }

    internal (TextLayout Layout, float X) Visible(float left, float width)
    {
        var start = Math.Max(0, OffsetAt(left) - 8);
        var end = Math.Min(_text.Length, OffsetAt(left + width) + 8);
        var origin = XAt(start);
        var ids = new List<ushort>();
        var positions = new List<float>();
        var offsets = new float[end - start + 1];
        var x = origin;
        for (var i = start; i < end; i++)
        {
            offsets[i - start] = x - origin;
            if (_text[i] != '\t')
            {
                ids.Add(_metrics.Glyphs[_text[i]]);
                positions.Add(x - origin);
            }

            x = NextX(i, x);
        }

        offsets[^1] = x - origin;
        var font = _metrics.Font;
        var metrics = font.SkFont.Metrics;
        var baseline = (Height - metrics.Descent - metrics.Ascent) * 0.5f;
        var text = _text[start..end];
        var run = new TextLayoutRun(0, text.Length, text, 0, offsets[^1], baseline, font, default, -1, null, false)
        {
            Offsets = offsets,
            Glyphs = new RunGlyphs([.. ids], [.. positions]),
        };
        var line = new TextLayoutLine(0, text.Length, 0, 0, run.Width, Height, baseline, 0, 1);
        return (new TextLayout(text, [line], [run], [], run.Width, Height), origin);
    }
}
