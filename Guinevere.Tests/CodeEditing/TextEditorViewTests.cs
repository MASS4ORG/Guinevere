using Guinevere.Tests.Mocks;
using static Guinevere.Tests.CodeEditing.EditorTestText;

namespace Guinevere.Tests.CodeEditing;

/// <summary>The editor control driven through whole frames with scripted input.</summary>
public sealed class TextEditorViewTests : IDisposable
{
    readonly FrameHarness _harness = new(600, 400);
    LayoutNode? _node;

    static readonly TextEditorOptions Options = new() { FontSize = 14 };

    public void Dispose() => _harness.Dispose();

    void Frame(TextEditorState state, double deltaTime = 0.016) =>
        _harness.Frame(gui => _node = gui.TextEditor(state, Options, 600, 400).Node, deltaTime);

    /// <summary>The screen point of an offset, from the frame's own layout.</summary>
    Vector2 PointOf(TextEditorState state, int offset)
    {
        Vector2 point = default;
        _harness.Frame(gui =>
        {
            var node = gui.TextEditor(state, Options, 600, 400).Node;
            _node = node;
            if (gui.Pass == Pass.Pass2Render)
            {
                using (node.Enter())
                {
                    var view = new TextEditorView(gui, state, Options, node);
                    view.Measure();
                    var rect = view.CaretRect(offset)!.Value;
                    point = new Vector2(rect.X + 1f, rect.Y + rect.H * 0.5f);
                }
            }
        });
        return point;
    }

    void Click(TextEditorState state, Vector2 at)
    {
        _harness.Input.MoveTo(at);
        _harness.Input.PressButton();
        Frame(state);
        _harness.Input.ReleaseButton();
        Frame(state);
    }

    void Press(TextEditorState state, KeyboardKey key, params KeyboardKey[] modifiers)
    {
        foreach (var modifier in modifiers)
        {
            _harness.Input.PressKey(modifier);
        }

        _harness.Input.PressKey(key);
        Frame(state);
        _harness.Input.ReleaseKey(key);
        foreach (var modifier in modifiers)
        {
            _harness.Input.ReleaseKey(modifier);
        }

        Frame(state);
    }

    void Focus(TextEditorState state)
    {
        Frame(state);
        Click(state, PointOf(state, state.Document.Primary.Caret));
    }

    [Fact]
    public void ClickingPlacesTheCaretOnTheGlyphAndTypingEditsThere()
    {
        var state = Editor("first line\nsecond line\nthird");
        Frame(state);

        Click(state, PointOf(state, 14));
        _harness.Input.TypeText("X\t\n");
        Frame(state);
        Frame(state);

        Assert.True(state.IsFocused);
        Assert.Equal("first line\nsecX|ond line\nthird", Show(state));
    }

    [Fact]
    public void KeysRunTheirBoundCommandsAndTabStaysInTheEditor()
    {
        var state = Editor("ab|c");
        _harness.Frame(gui =>
        {
            using (gui.Node(-1, -1).Direction(Axis.Vertical).Expand().Enter())
            {
                _node = gui.TextEditor(state, Options, 600, 300).Node;
                using (gui.Node(100, 20).Enter())
                {
                    gui.RegisterFocusable();
                }
            }
        });
        Focus(state);
        var focused = _harness.Gui.Focus.CurrentFocusedId;

        Press(state, KeyboardKey.Backspace);
        Press(state, KeyboardKey.Tab);
        Press(state, KeyboardKey.Z, KeyboardKey.LeftControl);

        Assert.Equal(focused, _harness.Gui.Focus.CurrentFocusedId);
        Assert.Equal("a|c", Show(state));
    }

    [Fact]
    public void DoubleAndTripleClicksSelectWordsAndLines()
    {
        var state = Editor("alpha beta\ngamma");
        Frame(state);
        var at = PointOf(state, 8);

        _harness.Input.MoveTo(at);
        for (var i = 0; i < 2; i++)
        {
            _harness.Input.PressButton();
            Frame(state);
            _harness.Input.ReleaseButton();
            Frame(state);
        }

        Assert.Equal("alpha «beta»\ngamma", Show(state));
        _harness.Input.PressButton();
        Frame(state);
        _harness.Input.ReleaseButton();
        Frame(state);
        Assert.Equal("«alpha beta\n»gamma", Show(state));
    }

    [Fact]
    public void DraggingExtendsTheSelection()
    {
        var state = Editor("hello world");
        Frame(state);
        var from = PointOf(state, 0);
        var to = PointOf(state, 5);

        _harness.Input.MoveTo(from);
        _harness.Input.PressButton();
        Frame(state);
        _harness.Input.MoveTo(to);
        Frame(state);
        _harness.Input.ReleaseButton();
        Frame(state);

        Assert.Equal("«hello» world", Show(state));
    }

    [Fact]
    public void TheWheelScrollsWithinTheDocumentAndCaretMovesRevealThemselves()
    {
        var state = Editor("|" + string.Join("\n", Enumerable.Range(0, 500).Select(i => $"line {i}")));
        Frame(state);
        _harness.Input.MoveTo(new Vector2(300, 200));

        _harness.Input.Scroll(-5);
        Frame(state);
        Assert.True(state.ScrollY > 0);
        _harness.Input.Scroll(1000);
        Frame(state);
        Assert.Equal(0, state.ScrollY);

        state.Execute("cursorBottom");
        Frame(state);
        Assert.True(state.ScrollY > 400 * 14);
    }

    [Fact]
    public void LargeDocumentsLayOutOnlyTheVisibleLines()
    {
        var text = string.Join("\n", Enumerable.Range(0, 100_000).Select(i => $"int value{i} = {i} * 2; // n"));
        var state = new TextEditorState(text, LanguageServices.ForLanguage("csharp"));
        Frame(state);
        state.ScrollY = 50_000 * 20;
        Frame(state);
        var builds = _harness.Gui.TextLayoutBuilds;

        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 20; i++)
        {
            state.Document.SetSelections([TextSelection.At(state.Document.Buffer.LineStart(50_010))]);
            state.Type("x");
            Frame(state);
        }

        watch.Stop();
        Assert.InRange(_harness.Gui.TextLayoutBuilds - builds, 1, 20 * 5);
        Assert.True(watch.ElapsedMilliseconds < 3000, $"20 edited frames took {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void ClickingAFoldMarkerHidesTheRegion()
    {
        var state = Editor("{\n  a\n  b\n}\nend", LanguageServices.ForLanguage("csharp"));
        for (var i = 0; i < 20 && state.FoldingRanges.Count == 0; i++)
        {
            Frame(state, 0.25);
            Thread.Sleep(5);
        }

        Assert.NotEmpty(state.FoldingRanges);
        var first = PointOf(state, 0);
        var rowHeight = PointOf(state, 2).Y - first.Y;
        Click(state, new Vector2(_node!.Rect.X + 30, first.Y));

        Assert.True(state.IsFolded(0));
        var closing = PointOf(state, state.Document.Buffer.LineStart(3));
        Assert.Equal(first.Y + rowHeight, closing.Y, 0.5f);
    }

    [Fact]
    public void CompletionPopupShowsAndAClickAcceptsAnItem()
    {
        var state = Editor("counter\nco|", settings: new TextEditorSettings());
        Focus(state);
        _harness.Input.TypeText("u");
        Frame(state);
        for (var i = 0; i < 50 && state.Completion is null; i++)
        {
            Thread.Sleep(5);
            Frame(state);
        }

        Frame(state);
        var popup = FindNode(_node!, node => node.Rect.W > 0 && node.Rect.H > 0 && node != _node && node.Parent == _node);
        Assert.NotNull(popup);
        Click(state, FrameHarness.Center(popup!));

        Assert.Equal("counter\ncounter|", Show(state));
    }

    [Fact]
    public void TheFindBarTakesTypingAndEnterMovesToTheNextMatch()
    {
        var state = Editor("|cat dog cat");
        Focus(state);

        Press(state, KeyboardKey.F, KeyboardKey.LeftControl);
        Frame(state);
        _harness.Input.TypeText("cat");
        Frame(state);
        Frame(state);
        Assert.Equal("«cat» dog cat", Show(state));
        Press(state, KeyboardKey.Enter);
        Assert.Equal("cat dog «cat»", Show(state));
        Press(state, KeyboardKey.Escape);
        Assert.False(state.Find.IsOpen);
    }

    void ClickFindButton(TextEditorState state, string command)
    {
        Frame(state);
        var button = FindNode(_node!, node => node.Id.EndsWith($"/{command}", StringComparison.Ordinal));
        Assert.NotNull(button);
        Click(state, FrameHarness.Center(button));
    }

    [Fact]
    public void FindQuerySelectionCanBeReplacedAndInvalidRegexDoesNotEditTheDocument()
    {
        var state = Editor("|cat dog cat");
        Focus(state);
        Press(state, KeyboardKey.F, KeyboardKey.LeftControl);
        _harness.Input.TypeText("cat");
        Frame(state);
        Press(state, KeyboardKey.A, KeyboardKey.LeftControl);
        Frame(state);
        _harness.Input.TypeText("dog");
        Frame(state);
        Assert.Equal("dog", state.Find.Query);
        Assert.Equal("cat «dog» cat", Show(state));

        state.Find.Query = "[";
        state.Find.UseRegex = true;
        Frame(state);
        ClickFindButton(state, "findNext");
        Assert.NotNull(state.Find.Error);
        Assert.Equal("cat dog cat", state.Text);
    }

    [Fact]
    public void FindButtonsNavigateMatchesAndCloseTheBar()
    {
        var state = Editor("|cat dog cat");
        state.Find.Query = "cat";
        state.Execute("find");

        ClickFindButton(state, "findNext");
        Assert.Equal("«cat» dog cat", Show(state));
        ClickFindButton(state, "findNext");
        Assert.Equal("cat dog «cat»", Show(state));
        ClickFindButton(state, "findPrevious");
        Assert.Equal("«cat» dog cat", Show(state));
        ClickFindButton(state, "closeFind");
        Assert.False(state.Find.IsOpen);
        Assert.Equal(_node!.Id, _harness.Gui.Focus.CurrentFocusedId);
    }

    [Fact]
    public void FindButtonsToggleCaseWordAndRegexMatching()
    {
        var state = Editor("|cat CAT cats c.t");
        state.Find.Query = "cat";
        state.Execute("find");

        ClickFindButton(state, "matchCase");
        Assert.True(state.Find.MatchCase);
        Assert.Equal(2, state.Find.Matches(state.Document).Count);
        ClickFindButton(state, "wholeWord");
        Assert.True(state.Find.WholeWord);
        Assert.Single(state.Find.Matches(state.Document));
        state.Find.Query = "c.t";
        ClickFindButton(state, "useRegex");
        Assert.True(state.Find.UseRegex);
        Assert.Equal(2, state.Find.Matches(state.Document).Count);
        ClickFindButton(state, "useRegex");
        Assert.False(state.Find.UseRegex);
        Assert.Single(state.Find.Matches(state.Document));
    }

    [Fact]
    public void ReplaceButtonsEditMatchesAndUndoRestoresTheDocument()
    {
        var state = Editor("|cat dog cat cat");
        state.Find.Query = "cat";
        state.Find.Replacement = "kitten";
        state.Execute("find");

        ClickFindButton(state, "showReplace");
        Assert.True(state.Find.ShowReplace);
        ClickFindButton(state, "findNext");
        ClickFindButton(state, "replaceOne");
        Assert.Equal("kitten dog «cat» cat", Show(state));
        ClickFindButton(state, "replaceAll");
        Assert.Equal("kitten dog kitten kitten", state.Text);
        state.Execute("undo");
        Assert.Equal("kitten dog cat cat", state.Text);
        ClickFindButton(state, "showReplace");
        Assert.False(state.Find.ShowReplace);
    }

    sealed class Composition : ITextCompositionCapability
    {
        internal Rect? Area;

        public string CompositionText => "かな";

        public int CompositionCaret => 1;

        public void SetCompositionArea(Rect caret) => Area = caret;
    }

    [Fact]
    public void InputMethodsLearnTheCaretAreaAndTheCompositionIsNotInserted()
    {
        var ime = new Composition();
        _harness.Gui.Platform.Register<ITextCompositionCapability>(ime);
        var state = Editor("ab|");
        Focus(state);
        Frame(state);

        Assert.NotNull(ime.Area);
        Assert.True(ime.Area!.Value.H > 0);
        Assert.Equal("ab|", Show(state));
    }

    [Fact]
    public void RestingThePointerShowsDiagnosticsAndHoverText()
    {
        var hover = new HoverInfo(0, 5, "the **alpha**");
        var state = Editor("alpha beta", LanguageServices.PlainText with { Hover = new StaticHover(hover) });
        Frame(state);
        var at = PointOf(state, 2);
        _harness.Input.MoveTo(at);
        Frame(state);
        for (var i = 0; i < 10; i++)
        {
            Frame(state, 0.2);
        }

        Assert.Equal(hover, state.Hover);
        Frame(state);
        Assert.NotNull(FindNode(_node!, node => node.Rect.W >= 300));
    }

    sealed class StaticHover(HoverInfo info) : IHoverProvider
    {
        public Task<HoverInfo?> GetHoverAsync(TextSnapshot snapshot, int offset, CancellationToken cancellation) =>
            Task.FromResult<HoverInfo?>(info);
    }

    [Fact]
    public void SelectionsAndCaretsPaintTheirThemeColors()
    {
        var theme = TextEditorTheme.Dark with { Selection = Color.FromArgb(255, 255, 0, 255) };
        var state = Editor("«wide selection»");
        _harness.Frame(gui => _node = gui.TextEditor(state, Options with { Theme = theme }, 600, 400).Node);

        using var image = _harness.Snapshot();
        using var pixmap = image.PeekPixels();
        var space = PointOf(state, 4);
        var color = pixmap.GetPixelColor((int)space.X + 2, (int)space.Y);
        Assert.True(color.Red > 200 && color.Blue > 200 && color.Green < 120, color.ToString());
    }

    static LayoutNode? FindNode(LayoutNode root, Func<LayoutNode, bool> match)
    {
        foreach (var child in root.Children)
        {
            if (match(child))
            {
                return child;
            }

            if (FindNode(child, match) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
