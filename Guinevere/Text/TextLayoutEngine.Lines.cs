namespace Guinevere;

static partial class TextLayoutEngine
{
    /// <summary>A wrapped line with the caret x of each of its offsets, before alignment.</summary>
    readonly record struct PlacedLine(LineRange Range, float[] X)
    {
        internal float Width => X[^1];
    }

    sealed partial class Build
    {
        bool IsParagraphStart(int offset) => offset == 0 || _text[offset - 1] == '\n';

        float Indent(int offset) => (IsParagraphStart(offset) ? _options.FirstLineIndent : _options.HangingIndent)
            * request.Scale;

        /// <summary>
        /// Where a line starting at <paramref name="start"/> ends at <paramref name="end"/>, from the block's start
        /// edge: its indent plus advances, with each tab advancing to the next stop.
        /// </summary>
        float LineX(int start, int end)
        {
            var x = Indent(start);
            var tab = _tabs.BinarySearch(start);
            tab = tab < 0 ? ~tab : tab;
            if (tab >= _tabs.Count || _tabs[tab] >= end)
            {
                return x + _prefix[end] - _prefix[start];
            }

            var from = start;
            for (; tab < _tabs.Count && _tabs[tab] < end; tab++)
            {
                x = NextTabStop(x + _prefix[_tabs[tab]] - _prefix[from]);
                from = _tabs[tab] + 1;
            }

            return x + _prefix[end] - _prefix[from];
        }

        float NextTabStop(float x)
        {
            foreach (var stop in _options.TabStops)
            {
                if (stop * request.Scale > x + 0.01f)
                {
                    return stop * request.Scale;
                }
            }

            var interval = _options.TabSize * _base.SkFont.MeasureText(" ");
            return interval <= 0f ? x : (MathF.Floor((x + 0.01f) / interval) + 1f) * interval;
        }

        /// <summary>The caret x of every offset of a line, so justification can stretch its spaces.</summary>
        PlacedLine Place(LineRange range)
        {
            var x = new float[range.End - range.Start + 1];
            var position = Indent(range.Start);
            for (var offset = range.Start; offset < range.End; offset++)
            {
                x[offset - range.Start] = position;
                position = _text[offset] == '\t' ? NextTabStop(position) : position + _advance[offset];
            }

            x[^1] = position;
            return new PlacedLine(range, x);
        }

        /// <summary>Whether a line ends its paragraph or the text, so justification leaves it alone.</summary>
        bool EndsParagraph(LineRange range) =>
            range.End >= _text.Length || _text[range.End] == '\n' || range.Marker.Length > 0;

        TextAlignment AlignmentOf(LineRange range) => request.Alignment != TextAlignment.Justify
            ? request.Alignment
            : EndsParagraph(range) || request.MaxWidth <= 0f ? _options.LastLineAlignment : TextAlignment.Justify;

        static void Justify(PlacedLine line, string text, float target)
        {
            var spaces = 0;
            for (var offset = line.Range.Start; offset < line.Range.End; offset++)
            {
                spaces += text[offset] == ' ' ? 1 : 0;
            }

            var extra = spaces == 0 ? 0f : (target - line.Width) / spaces;
            if (extra <= 0f)
            {
                return;
            }

            var added = 0f;
            for (var offset = line.Range.Start; offset < line.Range.End; offset++)
            {
                added += text[offset] == ' ' ? extra : 0f;
                line.X[offset - line.Range.Start + 1] += added;
            }
        }

        TextLayout Lay(List<LineRange> lineRanges)
        {
            var placed = new PlacedLine[lineRanges.Count];
            var widest = 0f;
            for (var i = 0; i < placed.Length; i++)
            {
                placed[i] = Place(lineRanges[i]);
                widest = Math.Max(widest, placed[i].Width + MarkerWidth(lineRanges[i]));
            }

            var wraps = request.MaxWidth > 0f && _options.WrapMode != TextWrapMode.None;
            var width = JustifyLines(placed, wraps ? Math.Min(widest, request.MaxWidth) : widest);
            var lines = new TextLayoutLine[placed.Length];
            var runs = new List<TextLayoutRun>();
            var y = 0f;
            var piece = 0;
            for (var index = 0; index < placed.Length; index++)
            {
                var range = placed[index].Range;
                piece = FirstPieceOf(range, piece);
                var line = LayLine(placed[index], piece, runs, y);
                lines[index] = Aligned(line, runs, width, AlignmentOf(range));
                y += line.Height;
            }

            return new TextLayout(_text, lines, [.. runs], request.Content.Links, width, y);
        }

        /// <summary>Stretches justified lines to the wrap width and returns the block width that results.</summary>
        float JustifyLines(PlacedLine[] placed, float width)
        {
            foreach (var line in placed)
            {
                if (AlignmentOf(line.Range) == TextAlignment.Justify)
                {
                    Justify(line, _text, request.MaxWidth);
                    width = Math.Max(width, request.MaxWidth);
                }
            }

            return width;
        }

        /// <summary>The first piece that can reach into a line, moving forward from the previous line's.</summary>
        int FirstPieceOf(LineRange range, int piece)
        {
            while (piece < _pieces.Count && _pieces[piece].End <= range.Start && _pieces[piece].End < _text.Length)
            {
                piece++;
            }

            return piece;
        }

        float MarkerWidth(LineRange range) => range.Marker.Length == 0 ? 0f : Measure(range.End, range.End, range.Marker)
            - LineX(range.End, range.End);

        /// <summary>
        /// Adds one line's runs, then places their baselines on the line box. Until then a run's baseline holds its
        /// shift, which the line box needs first.
        /// </summary>
        TextLayoutLine LayLine(PlacedLine placed, int firstPiece, List<TextLayoutRun> runs, float y)
        {
            var range = placed.Range;
            var first = runs.Count;
            var metrics = new LineMetrics();
            AppendSourceRuns(placed, firstPiece, runs, ref metrics);
            var x = AppendMarkerRuns(range, runs, placed.Width);
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

        void AppendSourceRuns(PlacedLine placed, int firstPiece, List<TextLayoutRun> runs, ref LineMetrics metrics)
        {
            var range = placed.Range;
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
                runs.Add(SourceRun(placed, piece, start, end, styled, shift));
                metrics.Add(_rangeFonts[piece.Range], styled.Image, shift, request);
            }
        }

        TextLayoutRun SourceRun(PlacedLine placed, Piece piece, int start, int end, StyledRange styled, float shift)
        {
            var origin = placed.X[start - placed.Range.Start];
            var offsets = new float[end - start + 1];
            for (var i = 0; i < offsets.Length; i++)
            {
                offsets[i] = placed.X[start + i - placed.Range.Start] - origin;
            }

            var text = piece.Kind switch
            {
                PieceKind.Text => _display[start..end],
                PieceKind.Missing => _options.MissingGlyph!,
                _ => ""
            };
            return new TextLayoutRun(start, end, text, origin, offsets[^1], shift, piece.Font, styled.Style,
                styled.Link, styled.Image, false)
            {
                Offsets = offsets,
                Glyphs = piece.Shaped is { } shaped ? Glyphs(placed, piece, shaped, start, end, origin) : null,
            };
        }

        /// <summary>The piece's glyphs inside <c>start..end</c>, placed at their characters' carets.</summary>
        RunGlyphs Glyphs(PlacedLine placed, Piece piece, ShapedText shaped, int start, int end, float origin)
        {
            var ids = new List<ushort>();
            var x = new List<float>();
            var y = shaped.YOffsets is null ? null : new List<float>();
            var cluster = -1;
            var within = 0f;
            for (var g = 0; g < shaped.Glyphs.Length; g++)
            {
                var offset = piece.Start + shaped.Clusters[g];
                if (!IsVisibleGlyph(offset, start, end))
                {
                    continue;
                }

                within = shaped.Clusters[g] == cluster ? within : 0f;
                cluster = shaped.Clusters[g];
                ids.Add(shaped.Glyphs[g]);
                x.Add(placed.X[offset - placed.Range.Start] - origin + within + (shaped.XOffsets?[g] ?? 0f));
                y?.Add(shaped.YOffsets![g]);
                within += shaped.Advances[g];
            }

            return new RunGlyphs([.. ids], [.. x], y is null ? null : [.. y]);
        }

        /// <summary>Whether a source offset belongs to the visible slice and is drawable text.</summary>
        bool IsVisibleGlyph(int offset, int start, int end) =>
            offset >= start && offset < end && !char.IsControl(_display[offset]);

        float AppendMarkerRuns(LineRange range, List<TextLayoutRun> runs, float x)
        {
            if (range.Marker.Length == 0)
            {
                return x;
            }

            var styleRange = RangeAt(range.End);
            var style = styleRange >= 0 ? _ranges[styleRange].Style : default;
            foreach (var (text, font, shaped) in MarkerRuns(range.Marker, range.End))
            {
                var positions = new float[shaped.Glyphs.Length];
                var width = 0f;
                for (var g = 0; g < positions.Length; g++)
                {
                    positions[g] = width;
                    width += shaped.Advances[g];
                }

                runs.Add(new TextLayoutRun(range.End, range.End, text, x, width, 0f, font, style, -1, null, true)
                {
                    Offsets = [0f],
                    Glyphs = new RunGlyphs(shaped.Glyphs, positions),
                });
                x += width;
            }

            return x;
        }

        static TextLayoutLine Aligned(TextLayoutLine line, List<TextLayoutRun> runs, float width,
            TextAlignment alignment)
        {
            var factor = alignment switch
            {
                TextAlignment.Center => 0.5f,
                TextAlignment.End => 1f,
                _ => 0f
            };
            var dx = Math.Max(0f, (width - line.Width) * factor);
            if (dx == 0f)
            {
                return line;
            }

            for (var i = line.FirstRun; i < line.FirstRun + line.RunCount; i++)
            {
                runs[i] = runs[i] with { X = runs[i].X + dx };
            }

            return line with { X = dx };
        }
    }
}
