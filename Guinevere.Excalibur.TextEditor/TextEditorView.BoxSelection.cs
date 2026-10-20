namespace Guinevere;

sealed partial class TextEditorView
{
    /// <summary>Selects text at a pointer press, or begins a rectangular selection with Alt+Shift.</summary>
    void SelectAt(Vector2 point, int offset, int clicks)
    {
        var modifiers = KeyChord.Held(_gui.Input);
        if ((modifiers & (KeyModifiers.Alt | KeyModifiers.Shift)) == (KeyModifiers.Alt | KeyModifiers.Shift))
        {
            _view.BoxAnchor = (Buffer.LineOf(offset), point.X - TextLeft + _state.ScrollX, _state.Document.Version);
            _view.Selecting = true;
            _state.Document.SetSelections([TextSelection.At(offset)]);
        }
        else if (clicks == 2)
        {
            var (start, end) = TextNavigation.WordAt(Buffer, offset);
            _state.Document.SetSelections([new TextSelection(start, end)]);
        }
        else if (clicks >= 3)
        {
            SelectPointerLine(offset);
        }
        else
        {
            SelectPointerCaret(offset, modifiers);
        }
    }

    void SelectPointerLine(int offset)
    {
        var line = Buffer.LineOf(offset);
        _state.Document.SetSelections([new TextSelection(Buffer.LineStart(line),
            line + 1 < Buffer.LineCount ? Buffer.LineStart(line + 1) : Buffer.Length)]);
    }

    void SelectPointerCaret(int offset, KeyModifiers modifiers)
    {
        var document = _state.Document;
        if ((modifiers & KeyModifiers.Alt) != 0)
        {
            document.SetSelections([.. document.Selections, TextSelection.At(offset)]);
            return;
        }

        document.SetSelections([(modifiers & KeyModifiers.Shift) != 0
            ? document.Primary.MoveTo(offset, true) : TextSelection.At(offset)]);
        _view.Selecting = true;
    }

    void EndSelectionDrag()
    {
        _view.Selecting = false;
        _view.BoxAnchor = null;
    }

    void DragTextSelection(Vector2 point)
    {
        ScrollSelection(point, horizontal: false);
        var document = _state.Document;
        document.SetSelections([document.Primary.MoveTo(OffsetAt(point), true)]);
    }

    /// <summary>Extends the box across visible rows; document edits invalidate the fixed anchor.</summary>
    void DragBoxSelection(Vector2 point, (int Line, float X, int Version) anchor)
    {
        if (anchor.Version != _state.Document.Version || _gui.Input.IsKeyPressed(KeyboardKey.Escape))
        {
            EndSelectionDrag();
            return;
        }

        ScrollSelection(point, horizontal: true);
        var row = Math.Clamp((int)Math.Floor((point.Y - Inner.Y + _state.ScrollY) / LineHeight), 0, RowCount - 1);
        var anchorRow = _state.Folds.RowOfLine(anchor.Line);
        var first = Math.Min(anchorRow, row);
        var last = Math.Max(anchorRow, row);
        var x = point.X - TextLeft + _state.ScrollX;
        var selections = new TextSelection[last - first + 1];
        for (var current = first; current <= last; current++)
        {
            var line = _state.Folds.LineOfRow(current);
            var start = Buffer.LineStart(line);
            var layout = LineLayout(line);
            var y = LineHeight * 0.5f;
            selections[current - first] = new TextSelection(start + layout.OffsetAt(new Vector2(anchor.X, y)),
                start + layout.OffsetAt(new Vector2(x, y)));
        }

        _state.Document.SetSelections(selections, row - first);
    }

    /// <summary>Moves the viewport at its edges while keeping the selection anchor in document coordinates.</summary>
    void ScrollSelection(Vector2 point, bool horizontal)
    {
        if (point.Y < Inner.Y)
        {
            _state.ScrollY -= LineHeight;
        }
        else if (point.Y > Bottom)
        {
            _state.ScrollY += LineHeight;
        }

        _state.ScrollY = Math.Clamp(_state.ScrollY, 0f, MaxScrollY);
        if (!horizontal)
        {
            return;
        }

        if (point.X < TextLeft)
        {
            _state.ScrollX -= DigitWidth * 2f;
        }
        else if (point.X > Right - ScrollbarWidth)
        {
            _state.ScrollX += DigitWidth * 2f;
        }

        _state.ScrollX = Math.Clamp(_state.ScrollX, 0f, MaxScrollX);
    }
}
