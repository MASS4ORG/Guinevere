namespace Guinevere;

sealed partial class TextEditorView
{
    void BuildFind()
    {
        var visible = _view.ShowFind;
        var bar = FindBar(visible);
        if (visible)
        {
            bar.BlockInput();
        }

        using (bar.Enter())
        {
            _gui.SetZIndex(ZIndex);
            _gui.SetEscapesAncestorClips();
            if (!visible)
            {
                return;
            }

            if (_gui.Pass == Pass.Pass2Render)
            {
                _gui.DrawBackgroundRect(_theme.Popup, 4f);
            }

            var find = _state.Find;
            FindRow("find", find.Query, query =>
            {
                find.Query = query;
                _state.Document.SetSelections([TextSelection.At(_state.Document.Primary.Start)]);
                _state.Execute("findNext");
            }, _view.FocusFind, shift => _state.Execute(shift ? "findPrevious" : "findNext"), MatchCount());
            FindActions();
            if (_view.ShowReplace)
            {
                FindRow("replace", find.Replacement, replacement => find.Replacement = replacement, false,
                    shift => _state.Execute(shift ? "replaceAll" : "replaceOne"), "⇧↵ all");
                ReplaceActions();
            }

            if (_gui.Pass == Pass.Pass2Render)
            {
                _view.FocusFind = false;
            }
        }
    }

    LayoutNode FindBar(bool visible)
    {
        var rows = _view.ShowReplace ? 4 : 2;
        var height = rows * (LineHeight + PopupPadding * 2f) + PopupPadding * (rows + 1);
        var padding = visible ? PopupPadding : 0f;
        return _gui.Node(visible ? FindWidth : 0f, visible ? height : 0f)
            .Absolute(Math.Max(0f, Inner.W - FindWidth - ScrollbarWidth - 8f), 4f).Direction(Axis.Vertical)
            .Padding(padding).Gap(padding).HitTestVisible(visible);
    }

    void FindActions()
    {
        using var row = _gui.Node(-1, LineHeight + PopupPadding * 2f).ExpandWidth()
            .Direction(Axis.Horizontal).Gap(PopupPadding).Enter();
        FindCommandButton("Previous", "findPrevious", 60);
        FindCommandButton("Next", "findNext", 42);
        var find = _state.Find;
        find.MatchCase = FindToggle("Aa", "matchCase", find.MatchCase, 32);
        find.WholeWord = FindToggle("Word", "wholeWord", find.WholeWord, 46);
        find.UseRegex = FindToggle(".*", "useRegex", find.UseRegex, 32);
        if (FindButton("Replace", "showReplace", 64))
        {
            find.ShowReplace = !find.ShowReplace;
        }

        if (FindButton("Close", "closeFind", 48))
        {
            find.IsOpen = false;
            _gui.RequestFocus(_node.Id);
        }
    }

    void ReplaceActions()
    {
        using var row = _gui.Node(-1, LineHeight + PopupPadding * 2f).ExpandWidth()
            .Direction(Axis.Horizontal).Gap(PopupPadding).Enter();
        FindCommandButton("Replace", "replaceOne", 76);
        FindCommandButton("Replace all", "replaceAll", 92);
    }

    void FindCommandButton(string label, string command, float width)
    {
        if (FindButton(label, command, width))
        {
            _state.Execute(command);
        }
    }

    bool FindToggle(string label, string id, bool value, float width) =>
        FindButton(value ? $"[{label}]" : label, id, width) ? !value : value;

    bool FindButton(string label, string id, float width) =>
        _gui.Button(label, width, LineHeight + PopupPadding * 2f, _size * 0.85f, id: $"{_node.Id}/{id}");

    string MatchCount()
    {
        var matches = _state.Find.Matches(_state.Document);
        if (_state.Find.Error is not null)
        {
            return "invalid";
        }

        var primary = _state.Document.Primary;
        var index = -1;
        for (var i = 0; i < matches.Count; i++)
        {
            if (matches[i].Start == primary.Start && matches[i].End == primary.End)
            {
                index = i;
                break;
            }
        }

        return matches.Count == 0 ? "no results" : $"{(index < 0 ? "?" : index + 1)} of {matches.Count}";
    }

    /// <summary>
    /// One field of the find bar, edited by the core text-field behavior, with a label after it. Enter runs
    /// <paramref name="enter"/> (with whether Shift is held) and Escape closes the bar.
    /// </summary>
    void FindRow(string name, string value, Action<string> changed, bool focus, Action<bool> enter, string label)
    {
        var fieldWidth = FindWidth - PopupPadding * 2f - DigitWidth * 12f;
        using var row = _gui.Node(-1, LineHeight + PopupPadding * 2f).ExpandWidth().Direction(Axis.Horizontal)
            .Gap(PopupPadding).Enter();
        var field = _gui.Node(fieldWidth, LineHeight + PopupPadding * 2f, $"{_node.Id}/{name}")
            .Padding(PopupPadding, PopupPadding * 2f);
        using (field.Enter())
        {
            var edit = TextEditor.State(_gui, field.Id, value);
            if (_gui.Pass == Pass.Pass2Render)
            {
                TextEditor.Process(_gui, edit, _gui.GetInteractable(field), _size);
                if (focus)
                {
                    _gui.RequestFocus(FocusReason.Keyboard);
                }

                if (!string.Equals(edit.Text, value, StringComparison.Ordinal))
                {
                    changed(edit.Text);
                }

                FindRowKeys(enter);
                DrawFindField(field, edit);
            }
        }

        var counter = _gui.LayoutText(new RichText(label), 0, _size * 0.9f);
        var text = _gui.Node(counter.Width, LineHeight + PopupPadding * 2f);
        if (_gui.Pass == Pass.Pass2Render)
        {
            using (text.Enter())
            {
                _gui.DrawTextLayout(counter, new Vector2(text.Rect.X, text.Rect.Y + PopupPadding), _theme.LineNumber);
            }
        }
    }

    /// <summary>Enter and Escape in the focused field; the field's own Escape handling has already dropped its flag.</summary>
    void FindRowKeys(Action<bool> enter)
    {
        if (!_gui.HasFocus())
        {
            return;
        }

        var input = _gui.Input;
        if (input.IsKeyPressed(KeyboardKey.Enter) || input.IsKeyPressed(KeyboardKey.KeyPadEnter))
        {
            enter((KeyChord.Held(input) & KeyModifiers.Shift) != 0);
        }
        else if (input.IsKeyPressed(KeyboardKey.Escape))
        {
            _state.Find.IsOpen = false;
            _gui.RequestFocus(_node.Id);
        }
    }

    void DrawFindField(LayoutNode field, TextEditState edit)
    {
        var rect = field.Rect;
        _gui.DrawRect(rect, edit.IsFocused ? _theme.PopupSelection : _theme.Background, 3f);
        var inner = field.InnerRect;
        var layout = _gui.LayoutText(new RichText(edit.Text), 0, _size);
        if (edit.HasSelection)
        {
            foreach (var bounds in layout.RangeBounds(edit.SelectionStart, edit.SelectionEnd))
            {
                _gui.DrawRect(new Rect(inner.X + bounds.X, inner.Y, bounds.W, inner.H), _theme.Selection);
            }
        }

        _gui.DrawTextLayout(layout, new Vector2(inner.X, inner.Y), _theme.Text);
        if (edit.IsFocused && edit.ShowCursor)
        {
            var caret = layout.CaretBounds(edit.CursorPosition);
            _gui.DrawRect(new Rect(inner.X + caret.X, inner.Y, 1.5f, inner.H), _theme.Caret);
        }
    }
}
