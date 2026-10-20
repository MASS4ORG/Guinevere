namespace Guinevere;

sealed partial class TextEditorView
{
    void Draw()
    {
        _gui.DrawRect(Inner, _theme.Background);
        var (first, count) = VisibleRows();
        var document = _state.Document;
        var caretLines = document.Selections.Select(selection => Buffer.LineOf(selection.Caret)).ToHashSet();
        for (var row = first; row < first + count; row++)
        {
            var line = _state.Folds.LineOfRow(row);
            if (_options.HighlightCurrentLine && caretLines.Contains(line) && document.Primary.IsEmpty)
            {
                _gui.DrawRect(new Rect(Inner.X, RowY(row), Inner.W, LineHeight), _theme.CurrentLine);
            }
        }

        DrawFindMatches(first, count);
        DrawSelections(first, count);
        DrawBrackets();
        var origin = TextLeft - _state.ScrollX;
        for (var row = first; row < first + count; row++)
        {
            var line = _state.Folds.LineOfRow(row);
            var layout = LineLayout(line);
            _view.WidestLine = Math.Max(_view.WidestLine, layout.Width);
            _gui.DrawTextLayout(layout, new Vector2(origin, RowY(row)), _theme.Text);
            if (_state.IsFolded(line))
            {
                DrawFoldedMarker(origin + layout.Width, RowY(row));
            }
        }

        DrawDiagnostics(first, count);
        DrawCarets();
        DrawComposition();
        DrawGutter(first, count, caretLines);
        DrawScrollbar();
    }

    /// <summary>Calls <paramref name="draw"/> with the screen rectangles a range covers on the visible rows.</summary>
    void ForRangeOnRows(int start, int end, int first, int count, bool lineBreaks, Action<Rect> draw)
    {
        var firstLine = Buffer.LineOf(Math.Max(start, 0));
        var lastLine = Buffer.LineOf(Math.Min(end, Buffer.Length));
        var origin = TextLeft - _state.ScrollX;
        for (var line = firstLine; line <= lastLine; line++)
        {
            if (_state.Folds.IsHidden(line))
            {
                continue;
            }

            var row = _state.Folds.RowOfLine(line);
            if (row < first || row >= first + count)
            {
                if (row >= first + count)
                {
                    break;
                }

                continue;
            }

            var lineStart = Buffer.LineStart(line);
            var length = Buffer.LineLength(line);
            var from = Math.Clamp(start - lineStart, 0, length);
            var to = Math.Clamp(end - lineStart, 0, length);
            var layout = LineLayout(line);
            foreach (var bounds in layout.RangeBounds(from, to))
            {
                draw(new Rect(origin + bounds.X, RowY(row), bounds.W, LineHeight));
            }

            if (lineBreaks && end > lineStart + length && start <= lineStart + length)
            {
                draw(new Rect(origin + layout.CaretBounds(length).X, RowY(row), DigitWidth * 0.6f, LineHeight));
            }
        }
    }

    void DrawSelections(int first, int count)
    {
        foreach (var selection in _state.Document.Selections)
        {
            if (!selection.IsEmpty)
            {
                ForRangeOnRows(selection.Start, selection.End, first, count, true,
                    rect => _gui.DrawRect(rect, _theme.Selection));
            }
        }
    }

    void DrawFindMatches(int first, int count)
    {
        if (!_state.Find.IsOpen || count == 0)
        {
            return;
        }

        var matches = _state.Find.Matches(_state.Document);
        var from = Buffer.LineStart(_state.Folds.LineOfRow(first));
        var lastLine = _state.Folds.LineOfRow(first + count - 1);
        var to = Buffer.LineStart(lastLine) + Buffer.LineLength(lastLine);
        var index = FirstMatchAtOrAfter(matches, from);
        for (; index < matches.Count && matches[index].Start <= to; index++)
        {
            ForRangeOnRows(matches[index].Start, matches[index].End, first, count, false,
                rect => _gui.DrawRect(rect, _theme.FindMatch, 2f));
        }
    }

    static int FirstMatchAtOrAfter(IReadOnlyList<FindMatch> matches, int offset)
    {
        var low = 0;
        var high = matches.Count;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (matches[mid].End < offset)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }

    void DrawBrackets()
    {
        var caret = _state.Document.Primary.Caret;
        if (_view.Brackets.Version != _state.Document.Version || _view.Brackets.Caret != caret)
        {
            _view.Brackets = (_state.Document.Version, caret, EditorCommands.MatchBracket(Buffer, caret, _state.Language));
        }

        if (_view.Brackets.Pair is not { } pair)
        {
            return;
        }

        var (first, count) = VisibleRows();
        foreach (var offset in (ReadOnlySpan<int>)[pair.Open, pair.Close])
        {
            ForRangeOnRows(offset, offset + 1, first, count, false, rect => _gui.DrawRect(rect, _theme.BracketMatch, 2f));
        }
    }

    void DrawFoldedMarker(float x, float y)
    {
        var width = DigitWidth * 3f;
        var rect = new Rect(x + DigitWidth, y + LineHeight * 0.2f, width, LineHeight * 0.6f);
        _gui.DrawRect(rect, _theme.Selection, 3f);
        for (var i = 0; i < 3; i++)
        {
            _gui.DrawCircleFilled(new Vector2(rect.X + width * (i + 1) / 4f, rect.Y + rect.H * 0.5f),
                Math.Max(1f, LineHeight * 0.06f), _theme.Text);
        }
    }

    /// <summary>A wavy underline below each diagnostic, in the color of its severity.</summary>
    void DrawDiagnostics(int first, int count)
    {
        foreach (var diagnostic in _state.Diagnostics)
        {
            var end = diagnostic.Start + Math.Max(1, diagnostic.Length);
            var color = _theme.ColorOf(diagnostic.Severity);
            ForRangeOnRows(diagnostic.Start, end, first, count, false, rect => Squiggle(rect, color));
        }
    }

    void Squiggle(Rect rect, Color color)
    {
        var step = Math.Max(2f, LineHeight * 0.12f);
        var y = rect.Y + LineHeight - step - 1f;
        var width = Math.Max(rect.W, step * 2f);
        for (var x = 0f; x < width; x += step)
        {
            var up = (int)(x / step) % 2 == 0;
            _gui.DrawLine(new Vector2(rect.X + x, up ? y + step : y), new Vector2(rect.X + x + step, up ? y : y + step),
                color);
        }
    }

    void DrawCarets()
    {
        var primary = _state.Document.Primary;
        if (CaretRect(primary.Caret) is { } primaryRect)
        {
            _view.CaretRect = new Rect(primaryRect.X - Inner.X, primaryRect.Y - Inner.Y, primaryRect.W, primaryRect.H);
        }

        if (!_state.IsFocused || !_view.CaretVisible)
        {
            return;
        }

        foreach (var selection in _state.Document.Selections)
        {
            if (CaretRect(selection.Caret) is { } rect && rect.X >= TextLeft - TextPadding)
            {
                _gui.DrawRect(rect, _theme.Caret);
            }
        }
    }

    /// <summary>Draws the text being composed by an input method at the primary caret, underlined.</summary>
    void DrawComposition()
    {
        if (!_state.IsFocused || !_gui.Platform.TryGet<ITextCompositionCapability>(out var ime) || ime is null
            || CaretRect(_state.Document.Primary.Caret) is not { } caret)
        {
            return;
        }

        ime.SetCompositionArea(_node.LocalToScreen(caret));
        if (ime.CompositionText is not { Length: > 0 } text)
        {
            return;
        }

        var layout = Layout(new RichText(text).Style(0, text.Length,
            new TextSpanStyle { Decoration = TextDecoration.Underline }));
        var origin = new Vector2(caret.X, caret.Y);
        _gui.DrawRect(new Rect(caret.X, caret.Y, layout.Width, LineHeight), _theme.Background);
        _gui.DrawTextLayout(layout, origin, _theme.Text);
        var cursor = layout.CaretBounds(Math.Clamp(ime.CompositionCaret, 0, text.Length));
        _gui.DrawRect(new Rect(origin.X + cursor.X, caret.Y, 2f, LineHeight), _theme.Caret);
    }

    void DrawGutter(int first, int count, HashSet<int> caretLines)
    {
        _gui.DrawRect(new Rect(Inner.X, Inner.Y, GutterWidth, Inner.H), _theme.Gutter);
        var markerWidth = _options.FoldMarkers ? LineHeight : DigitWidth;
        for (var row = first; row < first + count; row++)
        {
            var line = _state.Folds.LineOfRow(row);
            var y = RowY(row);
            if (_options.LineNumbers)
            {
                var number = Layout(new RichText((line + 1).ToString()));
                var color = caretLines.Contains(line) ? _theme.ActiveLineNumber : _theme.LineNumber;
                _gui.DrawTextLayout(number, new Vector2(Inner.X + GutterWidth - markerWidth - number.Width, y), color);
            }

            if (_options.FoldMarkers && _state.Folds.RangeAt(line) is not null)
            {
                DrawFoldMarker(new Vector2(Inner.X + GutterWidth - markerWidth * 0.5f, y + LineHeight * 0.5f),
                    _state.IsFolded(line));
            }
        }
    }

    /// <summary>A triangle pointing right for a folded region and down for an open one.</summary>
    void DrawFoldMarker(Vector2 center, bool folded)
    {
        var r = LineHeight * 0.18f;
        var color = _theme.LineNumber;
        if (folded)
        {
            _gui.DrawTriangleFilled(center + new Vector2(-r * 0.6f, -r), center + new Vector2(-r * 0.6f, r),
                center + new Vector2(r * 0.8f, 0), color);
        }
        else
        {
            _gui.DrawTriangleFilled(center + new Vector2(-r, -r * 0.6f), center + new Vector2(r, -r * 0.6f),
                center + new Vector2(0, r * 0.8f), color);
        }
    }

    void DrawScrollbar()
    {
        if (MaxScrollY > 0f)
        {
            _gui.DrawRect(ThumbRect(), _theme.Scrollbar, 3f);
        }
    }
}
