namespace Guinevere;

sealed partial class TextEditorView
{
    const float BlinkInterval = 0.5f;
    const double HoverDelay = 0.5;
    const float WheelLines = 3f;

    void HandleInput()
    {
        var input = _gui.Input;
        var interactable = _gui.GetInteractable(_node);
        var hovered = interactable.OnHover();
        if (interactable.OnClick(out var clicks))
        {
            _gui.RequestFocus(FocusReason.Mouse);
            Press(_gui.LocalMousePosition, clicks);
        }

        _state.IsFocused = _gui.HasFocus();
        if (hovered)
        {
            Wheel(input);
            TrackHover();
        }
        else
        {
            _view.PointerAt = new Vector2(float.NaN);
        }

        Drag(input);
        if (!_state.IsFocused)
        {
            return;
        }

        var before = (_state.Document.Version, _state.Document.Primary);
        Keyboard(input);
        Typing(input);
        Blink(before != (_state.Document.Version, _state.Document.Primary));
    }

    void Wheel(IInputHandler input)
    {
        var delta = input.MouseWheelDelta;
        if (delta == 0f)
        {
            return;
        }

        var shift = input.IsKeyDown(KeyboardKey.LeftShift) || input.IsKeyDown(KeyboardKey.RightShift);
        if (shift)
        {
            _state.ScrollX -= delta * DigitWidth * WheelLines * 2f;
        }
        else
        {
            _state.ScrollY -= delta * LineHeight * WheelLines;
        }

        _state.ClearHover();
    }

    /// <summary>A press: on the gutter it folds or selects lines, on the scrollbar it grabs the thumb.</summary>
    void Press(Vector2 point, int clicks)
    {
        var input = _gui.Input;
        if (point.X >= Right - ScrollbarWidth)
        {
            var thumb = ThumbRect();
            _view.ThumbGrab = point.Y >= thumb.Y && point.Y <= thumb.Y + thumb.H ? point.Y - thumb.Y : thumb.H * 0.5f;
            DragThumb(point.Y);
            return;
        }

        _state.CloseCompletion();
        var document = _state.Document;
        var offset = OffsetAt(point);
        if (point.X < Inner.X + GutterWidth)
        {
            PressGutter(offset);
            return;
        }

        var shift = input.IsKeyDown(KeyboardKey.LeftShift) || input.IsKeyDown(KeyboardKey.RightShift);
        var alt = input.IsKeyDown(KeyboardKey.LeftAlt) || input.IsKeyDown(KeyboardKey.RightAlt);
        if (clicks == 2)
        {
            var (start, end) = TextNavigation.WordAt(Buffer, offset);
            document.SetSelections([start == end ? TextSelection.At(offset) : new TextSelection(start, end)]);
        }
        else if (clicks >= 3)
        {
            var line = Buffer.LineOf(offset);
            document.SetSelections([new TextSelection(Buffer.LineStart(line),
                line + 1 < Buffer.LineCount ? Buffer.LineStart(line + 1) : Buffer.Length)]);
        }
        else if (alt)
        {
            document.SetSelections([.. document.Selections, TextSelection.At(offset)]);
        }
        else
        {
            document.SetSelections([shift ? document.Primary.MoveTo(offset, true) : TextSelection.At(offset)]);
            _view.Selecting = true;
        }

        document.BreakUndoGroup();
        RestartBlink();
    }

    /// <summary>A gutter press toggles the fold starting on the line, or selects the line.</summary>
    void PressGutter(int offset)
    {
        var line = Buffer.LineOf(offset);
        if (_options.FoldMarkers && _state.ToggleFold(line))
        {
            return;
        }

        _state.Document.SetSelections([new TextSelection(Buffer.LineStart(line),
            line + 1 < Buffer.LineCount ? Buffer.LineStart(line + 1) : Buffer.Length)]);
    }

    /// <summary>Extends the selection or moves the thumb while the button stays down, scrolling at the edges.</summary>
    void Drag(IInputHandler input)
    {
        if (!input.IsMouseButtonDown(MouseButton.Left))
        {
            _view.Selecting = false;
            _view.ThumbGrab = null;
            return;
        }

        var point = _gui.LocalMousePosition;
        if (_view.ThumbGrab is not null)
        {
            DragThumb(point.Y);
            return;
        }

        if (!_view.Selecting)
        {
            return;
        }

        if (point.Y < Inner.Y)
        {
            _state.ScrollY -= LineHeight;
        }
        else if (point.Y > Bottom)
        {
            _state.ScrollY += LineHeight;
        }

        var document = _state.Document;
        document.SetSelections([document.Primary.MoveTo(OffsetAt(point), true)]);
        _gui.RequestFrame();
    }

    void DragThumb(float y)
    {
        var thumb = ThumbRect();
        var travel = Math.Max(1f, Inner.H - thumb.H);
        var top = Math.Clamp(y - (_view.ThumbGrab ?? 0f) - Inner.Y, 0f, travel);
        _state.ScrollY = top / travel * MaxScrollY;
    }

    /// <summary>Runs the command bound to each key pressed this frame; an open completion list takes keys first.</summary>
    void Keyboard(IInputHandler input)
    {
        var modifiers = KeyChord.Held(input);
        if (_state.Completion is not null && modifiers == KeyModifiers.None && CompletionKeys(input))
        {
            return;
        }

        foreach (var key in _state.Keymap.Bindings.Keys.Select(chord => chord.Key).Distinct())
        {
            if (input.IsKeyPressed(key) && _state.Keymap.CommandFor(new KeyChord(key, modifiers)) is { } command)
            {
                _state.Execute(command);
                if (command is "find" or "replace")
                {
                    _view.FocusFind = true;
                }
            }
        }
    }

    bool CompletionKeys(IInputHandler input)
    {
        var command = input.IsKeyPressed(KeyboardKey.Down) ? "nextSuggestion"
            : input.IsKeyPressed(KeyboardKey.Up) ? "previousSuggestion"
            : input.IsKeyPressed(KeyboardKey.Enter) || input.IsKeyPressed(KeyboardKey.Tab) ? "acceptSuggestion"
            : input.IsKeyPressed(KeyboardKey.Escape) ? "cancel"
            : null;
        return command is not null && _state.Execute(command);
    }

    /// <summary>Types committed characters; control characters come through key commands instead.</summary>
    void Typing(IInputHandler input)
    {
        var modifiers = KeyChord.Held(input);
        if ((modifiers & KeyModifiers.Control) != 0 && (modifiers & KeyModifiers.Alt) == 0)
        {
            return;
        }

        var typed = input.GetTypedCharacters();
        if (string.IsNullOrEmpty(typed))
        {
            return;
        }

        var text = new string([.. typed.Where(c => c >= ' ' && c != '\x7f')]);
        if (text.Length > 0)
        {
            _state.Type(text);
        }
    }

    void Blink(bool moved)
    {
        if (moved)
        {
            RestartBlink();
            return;
        }

        _view.BlinkTimer += _gui.Clock.DeltaTime;
        if (_view.BlinkTimer >= BlinkInterval)
        {
            _view.BlinkTimer = 0;
            _view.CaretVisible = !_view.CaretVisible;
        }

        _gui.RequestFrameIn(BlinkInterval - _view.BlinkTimer);
    }

    void RestartBlink()
    {
        _view.BlinkTimer = 0;
        _view.CaretVisible = true;
    }

    /// <summary>Asks for hover information once the pointer has rested over text.</summary>
    void TrackHover()
    {
        var point = _gui.LocalMousePosition;
        var now = _gui.Clock.Elapsed;
        if (Vector2.DistanceSquared(point, _view.PointerAt) > 4f || float.IsNaN(_view.PointerAt.X))
        {
            _view.PointerAt = point;
            _view.PointerMovedAt = now;
            _state.ClearHover();
            return;
        }

        if (now - _view.PointerMovedAt < HoverDelay)
        {
            _gui.RequestFrameIn(HoverDelay - (now - _view.PointerMovedAt));
            return;
        }

        if (point.X >= TextLeft - TextPadding && point.X < Right - ScrollbarWidth)
        {
            _state.RequestHover(OffsetAt(point));
        }
    }

    /// <summary>The thumb of the vertical scrollbar.</summary>
    Rect ThumbRect()
    {
        var content = MaxScrollY + Inner.H;
        var height = Math.Clamp(Inner.H * Inner.H / Math.Max(content, 1f), 20f, Inner.H);
        var top = MaxScrollY <= 0f ? 0f : _state.ScrollY / MaxScrollY * (Inner.H - height);
        return new Rect(Right - ScrollbarWidth, Inner.Y + top, ScrollbarWidth, height);
    }
}
