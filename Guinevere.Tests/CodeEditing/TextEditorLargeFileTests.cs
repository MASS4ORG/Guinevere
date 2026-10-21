using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.CodeEditing;

/// <summary>Large-file frames keep work proportional to the visible text and a bounded highlighting budget.</summary>
public sealed class TextEditorLargeFileTests
{
    static readonly TextEditorOptions Options = new() { FontSize = 14 };

    /// <summary>An unchanged minified line does not rebuild or queue a million glyphs every frame.</summary>
    [Fact]
    public void MillionCharacterLineHasBoundedIdleFrameAllocations()
    {
        using var harness = new FrameHarness(1000, 600);
        var state = new TextEditorState(new string('x', 1_000_000), new LanguageServices());
        void Frame() => harness.Frame(gui => gui.TextEditor(state, Options, 1000, 600));
        Frame();
        Frame();
        var before = GC.GetAllocatedBytesForCurrentThread();
        Frame();
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0, 500_000);
        state.Execute("cursorBottom");
        Frame();
        state.Type("y");
        Frame();
        Assert.Equal(1_000_001, state.Document.Primary.Caret);
        Assert.True(state.Execute("undo"));
        Frame();
        Assert.Equal(1_000_000, state.Document.Primary.Caret);
    }

    /// <summary>A deep jump limits source characters passed to the synchronous highlighter per frame.</summary>
    [Fact]
    public void DeepSyntaxCatchupUsesACharacterBudget()
    {
        using var harness = new FrameHarness(1000, 600);
        var highlighter = new CountingHighlighter();
        var state = new TextEditorState(string.Join('\n', Enumerable.Repeat(new string('x', 1000), 30_000)),
            new LanguageServices { Highlighter = highlighter });
        state.Execute("cursorBottom");
        harness.Frame(gui => gui.TextEditor(state, Options, 1000, 600));
        Assert.InRange(highlighter.Characters, 1, 100_000);
    }

    /// <summary>Scrolling away from a caret does not lay out the large line that contains it.</summary>
    [Fact]
    public void OffscreenCaretDoesNotReadItsLine()
    {
        using var harness = new FrameHarness(1000, 600);
        var buffer = new CountingBuffer(new SumTreeTextBuffer(string.Concat(Enumerable.Repeat("short\n", 100))
            + new string('x', 1_000_000)));
        var state = new TextEditorState(buffer, new LanguageServices());
        state.Document.SetSelections([TextSelection.At(buffer.Length)]);
        harness.Frame(gui => gui.TextEditor(state, Options, 1000, 600));
        Assert.InRange(buffer.LargestRead, 0, 1000);
    }

    /// <summary>Custom shapers continue to see the complete source and are never replaced by ASCII metrics.</summary>
    [Fact]
    public void CustomShaperRetainsFullLineLayout()
    {
        using var harness = new FrameHarness(1000, 600);
        var shaper = new CountingShaper();
        harness.Gui.TextShaper = shaper;
        var state = new TextEditorState(new string('x', 20_000), new LanguageServices());
        harness.Frame(gui => gui.TextEditor(state, Options, 1000, 600));
        Assert.True(shaper.LargestRun >= 20_000);
    }

    /// <summary>Pointer hit tests reach the real end of a very long line after horizontal caret reveal.</summary>
    [Fact]
    public void LongLinePointerSelectionAndDeleteAreUndoable()
    {
        using var harness = new FrameHarness(1000, 600);
        var state = new TextEditorState(new string('x', 100_000), new LanguageServices());
        state.Execute("cursorBottom");
        harness.Frame(gui => gui.TextEditor(state, Options, 1000, 600));
        Assert.True(state.ScrollX > 0);
        var at = new Vector2(state.View.CaretRect.X, state.View.CaretRect.Y + state.View.CaretRect.H * 0.5f);
        harness.Click(gui => gui.TextEditor(state, Options, 1000, 600), at);
        Assert.Equal(100_000, state.Document.Primary.Caret);
        state.Execute("cursorLeftSelect");
        state.Execute("deleteLeft");
        Assert.Equal(99_999, state.Document.Buffer.Length);
        state.Execute("undo");
        Assert.Equal(new TextSelection(100_000, 99_999), state.Document.Primary);
    }

    /// <summary>Diagnostics spanning a minified line queue underlines only inside the viewport.</summary>
    [Fact]
    public void LongDiagnosticsHaveBoundedFrameAllocations()
    {
        using var harness = new FrameHarness(1000, 600);
        var state = new TextEditorState(new string('x', 1_000_000),
            new LanguageServices { Diagnostics = new WideDiagnostics() });
        state.Update(0);
        state.Update(1);
        Assert.Equal(2, state.Diagnostics.Count);
        void Frame() => harness.Frame(gui => gui.TextEditor(state, Options, 1000, 600));
        Frame();
        Frame();
        var before = GC.GetAllocatedBytesForCurrentThread();
        Frame();
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0, 500_000);
    }

    sealed class WideDiagnostics : IDiagnosticsProvider
    {
        /// <inheritdoc />
        public Task<IReadOnlyList<TextDiagnostic>> GetDiagnosticsAsync(TextSnapshot snapshot,
            CancellationToken cancellation) => Task.FromResult<IReadOnlyList<TextDiagnostic>>([
                new TextDiagnostic(0, snapshot.Buffer.Length, DiagnosticSeverity.Error, "wide"),
                new TextDiagnostic(snapshot.Buffer.Length - 10, 10, DiagnosticSeverity.Warning, "offscreen")]);
    }

    sealed class CountingShaper : ITextShaper
    {
        internal int LargestRun;

        /// <inheritdoc />
        public ShapedText Shape(Font font, string text, int start, int length, bool kerning)
        {
            LargestRun = Math.Max(LargestRun, length);
            return BasicTextShaper.Instance.Shape(font, text, start, length, kerning);
        }
    }

    sealed class CountingHighlighter : ISyntaxHighlighter
    {
        internal int Characters;

        /// <inheritdoc />
        public int Tokenize(ReadOnlySpan<char> line, int state, List<SyntaxToken> tokens)
        {
            Characters += line.Length;
            return state;
        }
    }

    sealed class CountingBuffer(ITextBuffer source) : ITextBuffer
    {
        internal int LargestRead;

        /// <inheritdoc />
        public int Length => source.Length;

        /// <inheritdoc />
        public int LineCount => source.LineCount;

        /// <inheritdoc />
        public char this[int offset] => source[offset];

        /// <inheritdoc />
        public int LineStart(int line) => source.LineStart(line);

        /// <inheritdoc />
        public int LineLength(int line) => source.LineLength(line);

        /// <inheritdoc />
        public int LineOf(int offset) => source.LineOf(offset);

        /// <inheritdoc />
        public string GetText(int start, int length)
        {
            LargestRead = Math.Max(LargestRead, length);
            return source.GetText(start, length);
        }

        /// <inheritdoc />
        public ITextBuffer Replace(int start, int length, string text) => source.Replace(start, length, text);
    }
}
