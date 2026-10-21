namespace Guinevere;

/// <summary>
/// One frame of a text editor: metrics, the visible rows, input and drawing. Built fresh every pass from the
/// persistent <see cref="TextEditorState"/>.
/// </summary>
sealed partial class TextEditorView
{
    const float TextPadding = 6f;
    const float ScrollbarWidth = 10f;
    const int TokenBudget = 4_096;

    readonly Gui _gui;
    readonly TextEditorState _state;
    readonly TextEditorOptions _options;
    readonly TextEditorTheme _theme;
    readonly LayoutNode _node;
    readonly TextEditorViewState _view;
    readonly List<SyntaxToken> _tokens = [];
    readonly Dictionary<int, EditorLineLayout> _lineLayouts = [];
    TextLayoutOptions _layoutOptions = new();
    Font _font = null!;
    float _size;
    int _tokenBudget = TokenBudget;
    int _layoutsVersion = -1;

    internal TextEditorView(Gui gui, TextEditorState state, TextEditorOptions options, LayoutNode node)
    {
        _gui = gui;
        _state = state;
        _options = options;
        _theme = options.Theme;
        _node = node;
        _view = state.View;
    }

    ITextBuffer Buffer => _state.Document.Buffer;

    /// <summary>Height of every row.</summary>
    internal float LineHeight { get; private set; }

    /// <summary>Width of a digit, for the gutter.</summary>
    float DigitWidth { get; set; }

    /// <summary>Width of the line-number and fold-marker gutter.</summary>
    internal float GutterWidth { get; private set; }

    Rect Inner => _node.InnerRect;

    float Right => Inner.X + Inner.W;

    float Bottom => Inner.Y + Inner.H;

    /// <summary>Left edge of column zero before horizontal scrolling.</summary>
    float TextLeft => Inner.X + GutterWidth + TextPadding;

    /// <summary>Width the text can use, left of the scrollbar.</summary>
    float TextWidth => Math.Max(0f, Inner.W - GutterWidth - TextPadding - ScrollbarWidth);

    int RowCount => _state.Folds.RowCount(Buffer.LineCount);

    float MaxScrollY => Math.Max(0f, (RowCount - 1) * LineHeight);

    float MaxScrollX => Math.Max(0f, _view.WidestLine + DigitWidth * 2f - TextWidth);

    internal void Frame()
    {
        _gui.RegisterFocusable(claimsArrowKeys: true);
        _gui.Focus.RegisterTextInput(_node.Id);
        _gui.Focus.SetClaimsTab(_node.Id, true);
        _state.Clipboard ??= _gui.Platform.TryGet<IClipboard>(out var clipboard) ? clipboard : null;
        _state.Tokens.Use(_state.Language.Highlighter);
        Measure();
        if (_gui.Pass == Pass.Pass1Build)
        {
            CaptureOverlays();
        }
        else if (_gui.Pass == Pass.Pass2Render)
        {
            _gui.ClipContent();
            HandleInput();
            _state.Update(_gui.Clock.Elapsed);
            if (_state.IsWaitingForProviders)
            {
                _gui.RequestFrameIn(0.05);
            }

            Reveal();
            Draw();
        }

        BuildOverlays();
    }

    internal void Measure()
    {
        var scope = _gui.CurrentNodeScope;
        _size = _options.FontSize > 0f ? _options.FontSize : scope.Get<LayoutNodeScopeTextSize>().Value;
        _font = _options.Font ?? _gui.Fonts.ResolveRole(FontRole.Code) ?? scope.Get<LayoutNodeScopeTextFont>().Value;
        _layoutOptions = new TextLayoutOptions
        {
            WrapMode = TextWrapMode.None,
            LineHeight = _options.LineHeight,
            TabSize = _state.Settings.TabSize,
        };
        var digits = Layout(new RichText("0"));
        LineHeight = Math.Max(1f, digits.Height);
        DigitWidth = digits.Width;
        var numbers = _options.LineNumbers ? Math.Max(3, Buffer.LineCount.ToString().Length) * DigitWidth : 0f;
        GutterWidth = numbers + (_options.FoldMarkers ? LineHeight : DigitWidth);
        _state.PageRows = Math.Max(1, (int)(Inner.H / LineHeight));
    }

    TextLayout Layout(RichText text) => _gui.LayoutText(text, 0, _size, _font, _layoutOptions);

    /// <summary>The first row drawn and the number of rows that fit.</summary>
    (int First, int Count) VisibleRows()
    {
        var first = Math.Max(0, (int)(_state.ScrollY / LineHeight));
        var count = Math.Min(RowCount - first, (int)Math.Ceiling(Inner.H / LineHeight) + 1);
        return (first, Math.Max(0, count));
    }

    /// <summary>Top of a row on screen.</summary>
    float RowY(int row) => Inner.Y + row * LineHeight - _state.ScrollY;

    /// <summary>
    /// The layout of a line with its syntax colors, or a plain layout while the highlighter is still catching up
    /// within its per-frame budget. Very long ASCII lines use sparse geometry without syntax styling.
    /// </summary>
    internal EditorLineLayout LineLayout(int line)
    {
        if (_layoutsVersion != _state.Document.Version)
        {
            _lineLayouts.Clear();
            _layoutsVersion = _state.Document.Version;
        }

        if (_lineLayouts.TryGetValue(line, out var cached))
        {
            return cached;
        }

        var layout = LargeLine(line) ?? StyledLine(line);
        _lineLayouts[line] = layout;
        return layout;
    }

    EditorLineLayout? LargeLine(int line)
    {
        if (Buffer.LineLength(line) > EditorLineCache.LongLineThreshold
            && _gui.TextShaper == BasicTextShaper.Instance
            && _view.Lines.Get(Buffer, line, new FontKey(_font, _size * _gui.FontScale),
                _layoutOptions.TabSize, LineHeight) is { } large)
        {
            return new EditorLineLayout(large);
        }

        return null;
    }

    EditorLineLayout StyledLine(int line)
    {
        var text = Buffer.Line(line);
        var rich = new RichText(text);
        StyleLine(rich, text, line);
        return new EditorLineLayout(Layout(rich));
    }

    void StyleLine(RichText rich, string text, int line)
    {
        _tokens.Clear();
        if (_state.Tokens.TryTokenize(Buffer, line, ref _tokenBudget, _tokens))
        {
            foreach (var token in _tokens)
            {
                if (token.Length > 0 && token.Start + token.Length <= text.Length
                    && _theme.Syntax.StyleOf(token.Kind) is { } style)
                {
                    rich.Style(token.Start, token.Length, style);
                }
            }
        }
        else
        {
            _gui.RequestFrame();
        }
    }

    /// <summary>The offset under a point relative to the node's coordinate space, clamped to the text.</summary>
    internal int OffsetAt(Vector2 point)
    {
        var row = (int)Math.Floor((point.Y - Inner.Y + _state.ScrollY) / LineHeight);
        if (row < 0)
        {
            return 0;
        }

        if (row >= RowCount)
        {
            return Buffer.Length;
        }

        var line = _state.Folds.LineOfRow(row);
        var layout = LineLayout(line);
        var column = layout.OffsetAt(new Vector2(point.X - TextLeft + _state.ScrollX, LineHeight * 0.5f));
        return Buffer.LineStart(line) + Math.Min(column, Buffer.LineLength(line));
    }

    /// <summary>The caret rectangle of an offset on screen, or null when its line is folded away.</summary>
    internal Rect? CaretRect(int offset)
    {
        var line = Buffer.LineOf(offset);
        if (_state.Folds.IsHidden(line))
        {
            return null;
        }

        var row = _state.Folds.RowOfLine(line);
        if (RowY(row) + LineHeight < Inner.Y || RowY(row) > Bottom)
        {
            return null;
        }

        var caret = LineLayout(line).CaretBounds(offset - Buffer.LineStart(line));
        return new Rect(TextLeft - _state.ScrollX + caret.X, RowY(row), 2f, LineHeight);
    }

    /// <summary>Scrolls the primary caret into view after a command moved it, unfolding a region hiding it.</summary>
    void Reveal()
    {
        if (_state.RevealCaret)
        {
            _state.RevealCaret = false;
            var caret = _state.Document.Primary.Caret;
            _state.Folds.Reveal(Buffer.LineOf(caret));
            var row = _state.Folds.RowOfLine(Buffer.LineOf(caret));
            var top = row * LineHeight;
            if (top < _state.ScrollY)
            {
                _state.ScrollY = top;
            }
            else if (top + LineHeight > _state.ScrollY + Inner.H)
            {
                _state.ScrollY = top + LineHeight - Inner.H;
            }

            if (CaretRect(caret) is { } rect)
            {
                var x = rect.X - TextLeft + _state.ScrollX;
                if (x < _state.ScrollX)
                {
                    _state.ScrollX = Math.Max(0f, x - DigitWidth * 4f);
                }
                else if (x > _state.ScrollX + TextWidth - DigitWidth)
                {
                    _state.ScrollX = x - TextWidth + DigitWidth * 4f;
                }
            }

            _view.WidestLine = Math.Max(_view.WidestLine, _state.ScrollX + TextWidth);
        }

        _state.ScrollY = Math.Clamp(_state.ScrollY, 0f, MaxScrollY);
        _state.ScrollX = Math.Clamp(_state.ScrollX, 0f, MaxScrollX);
    }
}
