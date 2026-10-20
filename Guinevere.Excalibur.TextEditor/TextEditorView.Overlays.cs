namespace Guinevere;

sealed partial class TextEditorView
{
    const int CompletionRows = 10;
    const float PopupPadding = 4f;
    const float HoverWidth = 380f;
    const float FindWidth = 420f;
    const int ZIndex = 5_000;

    /// <summary>
    /// Fixes which overlays exist this frame in the build pass, so the render pass creates the same nodes even when
    /// its input opens or closes one; the change shows next frame.
    /// </summary>
    void CaptureOverlays()
    {
        var completion = _state.IsFocused ? _state.Completion : null;
        _view.ShowCompletion = completion is { Items.Count: > 0 };
        _view.CompletionItems = completion?.Items ?? [];
        _view.CompletionSelected = completion?.Selected ?? 0;
        _view.HoverText = HoverText();
        _view.ShowFind = _state.Find.IsOpen;
        _view.ShowReplace = _state.Find.ShowReplace;
    }

    void BuildOverlays()
    {
        BuildCompletion();
        BuildHover();
        BuildFind();
    }

    /// <summary>The diagnostics under the pointer and the provider's hover text, or null.</summary>
    string? HoverText()
    {
        if (float.IsNaN(_view.PointerAt.X) || _gui.Clock.Elapsed - _view.PointerMovedAt < HoverDelay)
        {
            return null;
        }

        var offset = OffsetAt(_view.PointerAt);
        var parts = _state.Diagnostics.Where(diagnostic => offset >= diagnostic.Start
                && offset <= diagnostic.Start + Math.Max(1, diagnostic.Length))
            .Select(diagnostic => $"**{diagnostic.Severity}**: {diagnostic.Message}").ToList();
        if (_state.Hover is { } hover)
        {
            parts.Add(hover.Text);
        }

        _view.HoverAt = _view.PointerAt - new Vector2(Inner.X, Inner.Y) + new Vector2(0, LineHeight);
        return parts.Count == 0 ? null : string.Join("\n\n", parts);
    }

    void BuildCompletion()
    {
        var items = _view.ShowCompletion ? _view.CompletionItems : [];
        var rows = Math.Min(items.Count, CompletionRows);
        var width = rows == 0 ? 0f : items.Take(50).Max(item => Layout(Label(item)).Width) + PopupPadding * 4f;
        var height = rows == 0 ? 0f : rows * LineHeight + PopupPadding * 2f;
        var caret = _view.CaretRect;
        var y = caret.Y + caret.H + height > Inner.H && caret.Y > height ? caret.Y - height : caret.Y + caret.H;
        var popup = _gui.Node(width, height).Absolute(Math.Max(0f, caret.X - PopupPadding * 2f), y)
            .HitTestVisible(rows > 0);
        if (rows > 0)
        {
            popup.BlockInput();
        }

        using (popup.Enter())
        {
            _gui.SetZIndex(ZIndex);
            _gui.SetEscapesAncestorClips();
            if (_gui.Pass != Pass.Pass2Render || rows == 0)
            {
                return;
            }

            var rect = popup.Rect;
            _gui.DrawRect(rect, _theme.PopupBorder, 4f);
            _gui.DrawRect(new Rect(rect.X + 1, rect.Y + 1, rect.W - 2, rect.H - 2), _theme.Popup, 4f);
            var first = Math.Clamp(_view.CompletionSelected - rows + 1, 0, Math.Max(0, items.Count - rows));
            for (var i = 0; i < rows; i++)
            {
                var top = rect.Y + PopupPadding + i * LineHeight;
                if (first + i == _view.CompletionSelected)
                {
                    _gui.DrawRect(new Rect(rect.X + 2, top, rect.W - 4, LineHeight), _theme.PopupSelection, 3f);
                }

                _gui.DrawTextLayout(Layout(Label(items[first + i])), new Vector2(rect.X + PopupPadding * 2f, top),
                    _theme.Text);
            }

            if (_gui.GetInteractable(popup).OnClick() && _state.Completion is { } session)
            {
                session.Selected = first + (int)((_gui.LocalMousePosition.Y - rect.Y - PopupPadding) / LineHeight);
                _state.Execute("acceptSuggestion");
                _gui.RequestFocus(_node.Id);
            }
        }
    }

    RichText Label(CompletionItem item)
    {
        var text = item.Detail is null ? item.Label : $"{item.Label}  {item.Detail}";
        var rich = new RichText(text);
        if (_theme.Syntax.StyleOf(item.Kind) is { } style)
        {
            rich.Style(0, item.Label.Length, style);
        }

        if (item.Detail is not null)
        {
            rich.Style(item.Label.Length, text.Length - item.Label.Length, TextSpanStyle.Colored(_theme.LineNumber));
        }

        return rich;
    }

    void BuildHover()
    {
        var text = _view.HoverText;
        var visible = text is not null;
        var hover = _gui.Node(visible ? HoverWidth : 0f, visible ? -1f : 0f)
            .Absolute(Math.Min(_view.HoverAt.X, Math.Max(0f, Inner.W - HoverWidth)), _view.HoverAt.Y)
            .Padding(visible ? 8f : 0f).HitTestVisible(false);
        using (hover.Enter())
        {
            _gui.SetZIndex(ZIndex + 1);
            _gui.SetEscapesAncestorClips();
            if (text is null)
            {
                return;
            }

            if (_gui.Pass == Pass.Pass2Render)
            {
                _gui.DrawBackgroundRect(_theme.PopupBorder, 4f);
                var rect = hover.Rect;
                _gui.DrawRect(new Rect(rect.X + 1, rect.Y + 1, rect.W - 2, rect.H - 2), _theme.Popup, 4f);
            }

            _gui.Markdown(text, style: new MarkdownStyle
            {
                FontSize = _size * 0.95f,
                Text = _theme.Text,
                CodeFont = _font,
                Syntax = _theme.Syntax
            });
        }
    }

}
