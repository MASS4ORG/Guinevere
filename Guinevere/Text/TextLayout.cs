using System.Globalization;

namespace Guinevere;

/// <summary>One visual line of a <see cref="TextLayout"/>, in coordinates relative to the layout's top-left.</summary>
/// <param name="Start">First source offset on the line.</param>
/// <param name="End">Source offset after the line's last drawn character.</param>
/// <param name="X">Left edge after alignment.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Drawn width, including a truncation marker.</param>
/// <param name="Height">Line box height.</param>
/// <param name="Baseline">Baseline distance from the line's top.</param>
/// <param name="FirstRun">Index of the line's first run in <see cref="TextLayout.Runs"/>.</param>
/// <param name="RunCount">Number of runs on the line.</param>
public readonly record struct TextLayoutLine(int Start, int End, float X, float Y, float Width, float Height,
    float Baseline, int FirstRun, int RunCount);

/// <summary>A piece of one line drawn with a single font and style.</summary>
/// <param name="Start">First source offset.</param>
/// <param name="End">Source offset after the run; equals <paramref name="Start"/> for a marker.</param>
/// <param name="Text">Characters drawn; empty for images and hidden characters.</param>
/// <param name="X">Left edge relative to the layout.</param>
/// <param name="Width">Advance width.</param>
/// <param name="Baseline">Baseline relative to the layout's top, including any baseline shift.</param>
/// <param name="Font">Font the run is drawn with, after glyph fallback.</param>
/// <param name="Style">Resolved span style.</param>
/// <param name="LinkIndex">Index into <see cref="TextLayout.Links"/>, or -1.</param>
/// <param name="Image">Inline image drawn instead of text, or null.</param>
/// <param name="IsMarker">Whether the run is a truncation marker rather than source text.</param>
public readonly record struct TextLayoutRun(int Start, int End, string Text, float X, float Width, float Baseline,
    Font Font, TextSpanStyle Style, int LinkIndex, RichTextImage? Image, bool IsMarker)
{
    /// <summary>Caret x of every source offset from <see cref="Start"/> to <see cref="End"/>, relative to the run.</summary>
    internal float[] Offsets { get; init; } = [];

    /// <summary>The positioned glyphs the run draws, or null for images and hidden characters.</summary>
    internal RunGlyphs? Glyphs { get; init; }
}

/// <summary>A run's glyph ids and their positions relative to the run, built into a text blob once.</summary>
/// <param name="ids">Glyph ids.</param>
/// <param name="x">X position of each glyph.</param>
/// <param name="y">Downward shift of each glyph from the baseline, or null when every glyph sits on it.</param>
sealed class RunGlyphs(ushort[] ids, float[] x, float[]? y = null)
{
    SKTextBlob? _blob;

    /// <summary>Glyph ids.</summary>
    internal ushort[] Ids { get; } = ids;

    /// <summary>X position of each glyph relative to the run.</summary>
    internal float[] X { get; } = x;

    /// <summary>Downward shift of each glyph from the baseline, or null.</summary>
    internal float[]? Y { get; } = y;

    /// <summary>The glyphs as a blob with its baseline at zero, or null when there are none.</summary>
    internal SKTextBlob? Blob(SKFont font)
    {
        if (_blob is not null || Ids.Length == 0)
        {
            return _blob;
        }

        using var builder = new SKTextBlobBuilder();
        if (Y is { } y)
        {
            var points = new SKPoint[Ids.Length];
            for (var i = 0; i < points.Length; i++)
            {
                points[i] = new SKPoint(X[i], y[i]);
            }

            builder.AddPositionedRun(Ids, font, points);
        }
        else
        {
            builder.AddHorizontalRun(Ids, font, X, 0f);
        }

        return _blob = builder.Build();
    }
}

/// <summary>
/// The single measured layout of a text block: lines, styled runs and glyph positions. Measuring, drawing, selection
/// and hit testing all read this result, so they agree on line breaks and positions.
/// </summary>
public sealed class TextLayout
{
    readonly TextLayoutLine[] _lines;
    readonly TextLayoutRun[] _runs;
    readonly TextLink[] _links;

    internal TextLayout(string text, TextLayoutLine[] lines, TextLayoutRun[] runs, TextLink[] links, float width,
        float height)
    {
        Text = text;
        _lines = lines;
        _runs = runs;
        _links = links;
        Width = width;
        Height = height;
    }

    /// <summary>The laid-out source text.</summary>
    public string Text { get; }

    /// <summary>Width of the block.</summary>
    public float Width { get; }

    /// <summary>Height of all lines.</summary>
    public float Height { get; }

    /// <summary>Visual lines, top to bottom.</summary>
    public IReadOnlyList<TextLayoutLine> Lines => _lines;

    /// <summary>Styled runs of every line, in drawing order.</summary>
    public IReadOnlyList<TextLayoutRun> Runs => _runs;

    /// <summary>Link ranges of the source.</summary>
    public IReadOnlyList<TextLink> Links => _links;

    /// <summary>Whether lines were removed by the line limit.</summary>
    public bool IsTruncated => _runs.Length > 0 && Array.Exists(_runs, run => run.IsMarker);

    /// <summary>The drawn characters of one line, including a truncation marker.</summary>
    public string LineText(int line)
    {
        var value = _lines[line];
        var marker = "";
        for (var i = value.FirstRun; i < value.FirstRun + value.RunCount; i++)
        {
            if (_runs[i].IsMarker)
            {
                marker += _runs[i].Text;
            }
        }

        return Text[value.Start..value.End] + marker;
    }

    /// <summary>Index of the line at a vertical position, clamped to the first and last lines.</summary>
    public int LineAt(float y)
    {
        var low = 0;
        var high = _lines.Length - 1;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            if (_lines[mid].Y <= y)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return Math.Max(0, low);
    }

    /// <summary>Index of the line holding a source offset; a soft-wrap boundary belongs to the following line.</summary>
    public int LineOf(int offset)
    {
        var low = 0;
        var high = _lines.Length - 1;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            if (_lines[mid].Start <= offset)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return Math.Max(0, low);
    }

    /// <summary>The source offset nearest a point relative to the layout's top-left.</summary>
    public int OffsetAt(Vector2 point)
    {
        if (_lines.Length == 0)
        {
            return 0;
        }

        var line = _lines[LineAt(point.Y)];
        for (var i = line.FirstRun; i < line.FirstRun + line.RunCount; i++)
        {
            var run = _runs[i];
            if (run.IsMarker)
            {
                break;
            }

            if (point.X < run.X + run.Width)
            {
                return point.X <= run.X ? run.Start : OffsetInRun(run, point.X - run.X);
            }
        }

        return line.End;
    }

    /// <summary>The caret rectangle before a source offset, one pixel wide and a line tall.</summary>
    public Rect CaretBounds(int offset)
    {
        if (_lines.Length == 0)
        {
            return new Rect(0, 0, 1, Height);
        }

        var index = LineOf(offset);
        var line = _lines[index];
        return new Rect(XInLine(index, offset), line.Y, 1f, line.Height);
    }

    /// <summary>One rectangle per line covering the source range, as used for selection highlights.</summary>
    public IReadOnlyList<Rect> RangeBounds(int start, int end)
    {
        if (start > end)
        {
            (start, end) = (end, start);
        }

        var rects = new List<Rect>();
        for (var index = 0; index < _lines.Length; index++)
        {
            var line = _lines[index];
            var from = Math.Max(start, line.Start);
            var to = Math.Min(end, line.End);
            if (from >= to)
            {
                continue;
            }

            var x = XInLine(index, from);
            rects.Add(new Rect(x, line.Y, Math.Max(1f, XInLine(index, to) - x), line.Height));
        }

        return rects;
    }

    /// <summary>Bounds of the text element starting at a source offset, or an empty rectangle when none is drawn.</summary>
    public Rect CharacterBounds(int offset)
    {
        if (offset < 0 || offset >= Text.Length)
        {
            return Rect.Zero;
        }

        var bounds = RangeBounds(offset, offset + StringInfo.GetNextTextElementLength(Text, offset));
        return bounds.Count == 0 ? Rect.Zero : bounds[0];
    }

    /// <summary>The link under a point relative to the layout's top-left, or null.</summary>
    public TextLink? LinkAt(Vector2 point)
    {
        if (_links.Length == 0 || point.Y < 0f || point.Y >= Height)
        {
            return null;
        }

        var line = _lines[LineAt(point.Y)];
        for (var i = line.FirstRun; i < line.FirstRun + line.RunCount; i++)
        {
            if (_runs[i].LinkIndex >= 0 && Covers(_runs[i], point.X))
            {
                return _links[_runs[i].LinkIndex];
            }
        }

        return null;
    }

    /// <summary>The inline image under a point relative to the layout's top-left, or null.</summary>
    public RichTextImage? ImageAt(Vector2 point)
    {
        if (point.Y < 0f || point.Y >= Height)
        {
            return null;
        }

        var line = _lines[LineAt(point.Y)];
        for (var i = line.FirstRun; i < line.FirstRun + line.RunCount; i++)
        {
            if (_runs[i].Image is { } image && Covers(_runs[i], point.X))
            {
                return image;
            }
        }

        return null;
    }

    static bool Covers(TextLayoutRun run, float x) => x >= run.X && x < run.X + run.Width;

    float XInLine(int index, int offset)
    {
        var line = _lines[index];
        offset = Math.Clamp(offset, line.Start, line.End);
        for (var i = line.FirstRun; i < line.FirstRun + line.RunCount; i++)
        {
            var run = _runs[i];
            if (!run.IsMarker && offset >= run.Start && offset <= run.End)
            {
                return run.X + PrefixWidth(run, offset - run.Start);
            }
        }

        return line.X;
    }

    static float PrefixWidth(TextLayoutRun run, int length) =>
        length <= 0 ? 0f : length < run.Offsets.Length ? run.Offsets[length] : run.Width;

    int OffsetInRun(TextLayoutRun run, float x)
    {
        if (run.Image is not null)
        {
            return x < run.Width * 0.5f ? run.Start : run.End;
        }

        var previous = run.Start;
        var previousX = 0f;
        while (previous < run.End)
        {
            var next = Math.Min(run.End, previous + StringInfo.GetNextTextElementLength(Text, previous));
            var nextX = PrefixWidth(run, next - run.Start);
            if (x < (previousX + nextX) * 0.5f)
            {
                return previous;
            }

            previous = next;
            previousX = nextX;
        }

        return run.End;
    }
}
