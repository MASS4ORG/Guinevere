namespace Guinevere;

/// <summary>Fonts and constraints a text block is laid out with; compares by value so it can key the cache.</summary>
/// <param name="Content">The styled source.</param>
/// <param name="Font">Inherited font at its display-scaled size.</param>
/// <param name="Emoji">Last-resort fallback typeface.</param>
/// <param name="Widget">Icon font consulted during fallback.</param>
/// <param name="Scale">Display scale applied to logical sizes in span styles.</param>
/// <param name="MaxWidth">Wrap width, or zero or less for none.</param>
/// <param name="Options">Wrapping and truncation options.</param>
/// <param name="Alignment">Horizontal placement of each line.</param>
/// <param name="FontsVersion">Font registry version, so registering faces relayouts styled spans.</param>
readonly record struct TextLayoutRequest(RichTextContent Content, FontKey Font, SKTypeface Emoji, Font Widget,
    float Scale, float MaxWidth, TextLayoutOptions Options, TextAlignment Alignment, int FontsVersion);

/// <summary>Value identity of a face at a size; fonts are otherwise recreated per call.</summary>
/// <param name="Font">The face, at any size.</param>
/// <param name="Size">The display-scaled size it is laid out at.</param>
readonly record struct FontKey(Font Font, float Size)
{
    /// <summary>The face at <see cref="Size"/>.</summary>
    internal Font Resolve() => Font.Size == Size ? Font : Font.Resized(Size);

    /// <inheritdoc />
    public bool Equals(FontKey other)
    {
        var a = Font.SkFont;
        var b = other.Font.SkFont;
        return Size == other.Size && (ReferenceEquals(Font, other.Font) || (a.Typeface == b.Typeface
            && a.Embolden == b.Embolden && a.SkewX == b.SkewX && Font.Fallbacks == other.Font.Fallbacks));
    }

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Font.SkFont.Typeface, Size);
}

/// <summary>Builds a <see cref="TextLayout"/> from styled content: font selection, advances, wrapping and runs.</summary>
static class TextLayoutEngine
{
    readonly record struct Piece(int Start, int End, Font Font, int Range, bool Hidden);

    sealed class Build(TextLayoutRequest request, FontRegistry? fonts)
    {
        readonly Font _base = request.Font.Resolve();
        readonly string _text = request.Content.Text;
        readonly StyledRange[] _ranges = request.Content.Ranges;
        readonly Dictionary<float, Font> _emoji = [];
        readonly List<Piece> _pieces = [];
        Font[] _rangeFonts = [];
        float[] _prefix = [];

        internal TextLayout Run()
        {
            _rangeFonts = new Font[_ranges.Length];
            for (var i = 0; i < _ranges.Length; i++)
            {
                _rangeFonts[i] = ResolveFont(_ranges[i].Style);
                AppendPieces(i);
            }

            _prefix = Advances();
            var ranges = WrappedTextLayout.WrapRanges(_text, request.MaxWidth, Measure, request.Options);
            return Lay(ranges);
        }

        Font ResolveFont(TextSpanStyle style)
        {
            var font = style.Font ?? _base;
            var size = style.Size is { } logical ? logical * request.Scale : _base.Size;
            if (style.Weight is null && style.Italic is null)
            {
                return font.Size == size ? font : font.Resized(size);
            }

            return Restyle(font, size, Math.Clamp(style.Weight ?? font.Weight, 1, 1000), style.Italic ?? font.Italic);
        }

        /// <summary>A registered face for the weight and slant, or the face synthesized when none is registered.</summary>
        Font Restyle(Font font, float size, int weight, bool italic) =>
            fonts?.Resolve(font.Families ?? $"\"{font.FamilyName}\"", size, weight, italic)
            ?? font.Resized(size).Restyled(weight, italic);

        Font EmojiAt(float size)
        {
            if (!_emoji.TryGetValue(size, out var font))
            {
                _emoji[size] = font = new Font(new SKFont(request.Emoji, size));
            }

            return font;
        }

        void AppendPieces(int index)
        {
            var range = _ranges[index];
            var font = _rangeFonts[index];
            if (range.Image is not null)
            {
                _pieces.Add(new Piece(range.Start, range.End, font, index, false));
                return;
            }

            var fontRanges = new List<FontRange>();
            FontTextLayout.AppendRanges(fontRanges, _text, range.Start, range.End, font, request.Widget,
                EmojiAt(font.Size));
            foreach (var fontRange in fontRanges)
            {
                _pieces.Add(new Piece(fontRange.Start, fontRange.End, fontRange.Font, index, fontRange.Hidden));
            }
        }

        /// <summary>Running advance sums, so any source range measures in constant time.</summary>
        float[] Advances()
        {
            var prefix = new float[_text.Length + 1];
            foreach (var piece in _pieces)
            {
                if (piece.Hidden)
                {
                    continue;
                }

                if (_ranges[piece.Range].Image is { } image)
                {
                    prefix[piece.Start + 1] = image.Width * request.Scale;
                    continue;
                }

                var span = _text.AsSpan(piece.Start, piece.End - piece.Start);
                var widths = piece.Font.SkFont.GetGlyphWidths(span, null);
                var glyph = 0;
                var offset = piece.Start;
                foreach (var rune in span.EnumerateRunes())
                {
                    prefix[offset + 1] = glyph < widths.Length
                        ? widths[glyph]
                        : piece.Font.SkFont.MeasureText(_text.AsSpan(offset, rune.Utf16SequenceLength));
                    glyph++;
                    offset += rune.Utf16SequenceLength;
                }
            }

            for (var i = 1; i < prefix.Length; i++)
            {
                prefix[i] += prefix[i - 1];
            }

            return prefix;
        }

        float Measure(int start, int end, string suffix)
        {
            var width = _prefix[end] - _prefix[start];
            if (suffix.Length == 0)
            {
                return width;
            }

            foreach (var marker in MarkerRuns(suffix, end))
            {
                width += marker.Font.SkFont.MeasureText(marker.Text);
            }

            return width;
        }

        int RangeAt(int offset)
        {
            for (var i = _ranges.Length - 1; i >= 0; i--)
            {
                if (_ranges[i].Start < offset || (_ranges[i].Start == offset && offset == 0))
                {
                    return i;
                }
            }

            return _ranges.Length > 0 ? 0 : -1;
        }

        List<FontRun> MarkerRuns(string marker, int end)
        {
            var range = RangeAt(end);
            var font = range >= 0 ? _rangeFonts[range] : _base;
            return FontTextLayout.Create(marker, font, request.Widget, EmojiAt(font.Size));
        }

        TextLayout Lay(List<LineRange> lineRanges)
        {
            var lines = new TextLayoutLine[lineRanges.Count];
            var runs = new List<TextLayoutRun>();
            var y = 0f;
            var widest = 0f;
            var piece = 0;
            for (var index = 0; index < lineRanges.Count; index++)
            {
                var range = lineRanges[index];
                while (piece < _pieces.Count && _pieces[piece].End <= range.Start && _pieces[piece].End < _text.Length)
                {
                    piece++;
                }

                var line = LayLine(range, piece, runs, y);
                lines[index] = line;
                y += line.Height;
                widest = Math.Max(widest, line.Width);
            }

            var width = request.MaxWidth > 0f && request.Options.WrapMode != TextWrapMode.None
                ? Math.Min(widest, request.MaxWidth)
                : widest;
            Align(lines, runs, width);
            return new TextLayout(_text, lines, [.. runs], request.Content.Links, width, y);
        }

        /// <summary>
        /// Adds one line's runs, then places their baselines on the line box. Until then a run's baseline holds its
        /// shift, which the line box needs first.
        /// </summary>
        TextLayoutLine LayLine(LineRange range, int firstPiece, List<TextLayoutRun> runs, float y)
        {
            var first = runs.Count;
            var metrics = new LineMetrics();
            var x = AppendSourceRuns(range, firstPiece, runs, ref metrics);
            x = AppendMarkerRuns(range, runs, x);
            if (!metrics.Any)
            {
                var fallback = RangeAt(range.Start);
                metrics.Add(fallback >= 0 ? _rangeFonts[fallback] : _base, null, 0f, request);
            }

            for (var i = first; i < runs.Count; i++)
            {
                runs[i] = runs[i] with { Baseline = y + metrics.Above - runs[i].Baseline };
            }

            return new TextLayoutLine(range.Start, range.End, 0f, y, x, metrics.Above + metrics.Below, metrics.Above,
                first, runs.Count - first);
        }

        float AppendSourceRuns(LineRange range, int firstPiece, List<TextLayoutRun> runs, ref LineMetrics metrics)
        {
            var x = 0f;
            for (var p = firstPiece; p < _pieces.Count && _pieces[p].Start < range.End; p++)
            {
                var piece = _pieces[p];
                var start = Math.Max(piece.Start, range.Start);
                var end = Math.Min(piece.End, range.End);
                if (start >= end)
                {
                    continue;
                }

                var styled = _ranges[piece.Range];
                var shift = (styled.Style.BaselineShift ?? 0f) * request.Scale;
                var width = _prefix[end] - _prefix[start];
                var text = piece.Hidden || styled.Image is not null ? "" : _text[start..end];
                runs.Add(new TextLayoutRun(start, end, text, x, width, shift, piece.Font, styled.Style, styled.Link,
                    styled.Image, false));
                metrics.Add(_rangeFonts[piece.Range], styled.Image, shift, request);
                x += width;
            }

            return x;
        }

        float AppendMarkerRuns(LineRange range, List<TextLayoutRun> runs, float x)
        {
            if (range.Marker.Length == 0)
            {
                return x;
            }

            var styleRange = RangeAt(range.End);
            var style = styleRange >= 0 ? _ranges[styleRange].Style : default;
            foreach (var marker in MarkerRuns(range.Marker, range.End))
            {
                var width = marker.Font.SkFont.MeasureText(marker.Text);
                runs.Add(new TextLayoutRun(range.End, range.End, marker.Text, x, width, 0f, marker.Font, style, -1,
                    null, true));
                x += width;
            }

            return x;
        }

        void Align(TextLayoutLine[] lines, List<TextLayoutRun> runs, float width)
        {
            var factor = request.Alignment switch
            {
                TextAlignment.Center => 0.5f,
                TextAlignment.End => 1f,
                _ => 0f
            };
            if (factor == 0f)
            {
                return;
            }

            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                var dx = Math.Max(0f, (width - line.Width) * factor);
                lines[index] = line with { X = dx };
                for (var i = line.FirstRun; i < line.FirstRun + line.RunCount; i++)
                {
                    runs[i] = runs[i] with { X = runs[i].X + dx };
                }
            }
        }
    }

    /// <summary>The line box: the tallest ascent and descent of its runs, each with half-leading.</summary>
    struct LineMetrics
    {
        internal float Above;
        internal float Below;
        internal bool Any;

        internal void Add(Font font, RichTextImage? image, float shift, TextLayoutRequest request)
        {
            float above;
            float below;
            if (image is not null)
            {
                above = image.Height * request.Scale + shift;
                below = -shift;
            }
            else
            {
                var metrics = font.SkFont.Metrics;
                var ascent = -metrics.Ascent;
                var descent = metrics.Descent;
                var leading = (font.Size * request.Options.LineHeight - (ascent + descent)) / 2f;
                above = ascent + leading + shift;
                below = descent + leading - shift;
            }

            Above = Any ? Math.Max(Above, above) : above;
            Below = Any ? Math.Max(Below, below) : below;
            Any = true;
        }
    }

    /// <summary>Lays out <paramref name="request"/>; <paramref name="fonts"/> supplies real bold and italic faces.</summary>
    internal static TextLayout Create(TextLayoutRequest request, FontRegistry? fonts) =>
        new Build(request, fonts).Run();
}
