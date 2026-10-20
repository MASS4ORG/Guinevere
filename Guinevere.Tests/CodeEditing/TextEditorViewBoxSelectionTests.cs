using Guinevere.Tests.Mocks;
using static Guinevere.Tests.CodeEditing.EditorTestText;

namespace Guinevere.Tests.CodeEditing;

/// <summary>Rectangular pointer selections use rendered columns and the normal multi-caret editing commands.</summary>
public sealed class TextEditorViewBoxSelectionTests : IDisposable
{
    readonly FrameHarness _harness = new(600, 400);
    readonly TextEditorOptions _options = new() { FontSize = 14 };

    /// <summary>Releases the test drawing surface.</summary>
    public void Dispose() => _harness.Dispose();

    void Frame(TextEditorState state, double deltaTime = 0.016) =>
        _harness.Frame(gui => gui.TextEditor(state, _options, 600, 400), deltaTime);

    Vector2 PointOf(TextEditorState state, int offset)
    {
        Vector2 point = default;
        _harness.Frame(gui =>
        {
            var node = gui.TextEditor(state, _options, 600, 400).Node;
            if (gui.Pass == Pass.Pass2Render)
            {
                using var scope = node.Enter();
                var view = new TextEditorView(gui, state, _options, node);
                view.Measure();
                var caret = view.CaretRect(offset)!.Value;
                point = new Vector2(caret.X, caret.Y + caret.H * 0.5f);
            }
        });
        return point;
    }

    void Begin(TextEditorState state, Vector2 point, bool rightModifiers = false)
    {
        _harness.Input.PressKey(rightModifiers ? KeyboardKey.RightAlt : KeyboardKey.LeftAlt);
        _harness.Input.PressKey(rightModifiers ? KeyboardKey.RightShift : KeyboardKey.LeftShift);
        _harness.Input.MoveTo(point);
        _harness.Input.PressButton();
        Frame(state);
    }

    void End(TextEditorState state)
    {
        _harness.Input.ReleaseButton();
        _harness.Input.ReleaseKey(KeyboardKey.LeftAlt);
        _harness.Input.ReleaseKey(KeyboardKey.RightAlt);
        _harness.Input.ReleaseKey(KeyboardKey.LeftShift);
        _harness.Input.ReleaseKey(KeyboardKey.RightShift);
        Frame(state);
    }

    void Box(TextEditorState state, Vector2 from, Vector2 to, bool rightModifiers = false)
    {
        Begin(state, from, rightModifiers);
        _harness.Input.MoveTo(to);
        Frame(state);
        End(state);
    }

    /// <summary>Every drag direction preserves per-row anchors and follows the row at the pointer.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void BoxSelectionCreatesOneSelectionPerRowInEitherDirection(bool leftward, bool upward)
    {
        var state = Editor("012345\n012345\n012345", LanguageServices.PlainText);
        var anchor = (upward ? 14 : 0) + (leftward ? 4 : 1);
        var caret = (upward ? 0 : 14) + (leftward ? 1 : 4);
        var from = PointOf(state, anchor);
        var to = PointOf(state, caret);
        Box(state, from, to, rightModifiers: upward);

        Assert.Equal("0«123»45\n0«123»45\n0«123»45", Show(state));
        Assert.Equal(caret, state.Document.Primary.Caret);
        Assert.All(state.Document.Selections, selection => Assert.Equal(leftward, selection.Caret < selection.Anchor));
        var selections = state.Document.Selections.ToArray();
        var primary = state.Document.PrimaryIndex;
        _harness.Input.TypeText("X");
        Frame(state);
        Assert.Equal("0X45\n0X45\n0X45", state.Text);
        Assert.True(state.Execute("undo"));
        Assert.Equal(selections, state.Document.Selections);
        Assert.Equal(primary, state.Document.PrimaryIndex);
        Assert.True(state.Execute("redo"));
        Assert.Equal("0X45\n0X45\n0X45", state.Text);
    }

    /// <summary>Short and empty lines clamp to their ends without inserting virtual padding.</summary>
    [Fact]
    public void BoxSelectionIncludesShortAndEmptyRows()
    {
        var state = Editor("012345\n0\n\n012345", LanguageServices.PlainText);
        var from = PointOf(state, 2);
        var to = PointOf(state, state.Text.Length - 2);
        Box(state, from, to);

        Assert.Equal("01«23»45\n0|\n|\n01«23»45", Show(state));
        state.Type("X");
        Assert.Equal("01X45\n0X\nX\n01X45", state.Text);
        state.Execute("undo");
        Assert.Equal("012345\n0\n\n012345", state.Text);
    }

    /// <summary>Copied rectangles keep trailing empty rows when pasted into the same number of selections.</summary>
    [Fact]
    public void RectangularCopyAndPastePreserveAnEmptyLastRow()
    {
        var state = Editor("012345\n012345\n0", LanguageServices.PlainText);
        var from = PointOf(state, 2);
        var to = PointOf(state, 11);
        to.Y = PointOf(state, state.Text.Length).Y;
        Box(state, from, to);
        state.Execute("copy");
        Assert.Equal("23\n23\n", ((TestClipboard)state.Clipboard!).Text);
        state.Execute("paste");
        Assert.Equal("012345\n012345\n0", state.Text);
        state.Execute("undo");
        Assert.Equal("012345\n012345\n0", state.Text);
    }

    /// <summary>A rectangle may collapse to one insertion point on every row.</summary>
    [Fact]
    public void ZeroWidthBoxCreatesMultipleCarets()
    {
        var state = Editor("012345\n012345\n012345", LanguageServices.PlainText);
        var from = PointOf(state, 3);
        var to = PointOf(state, 17);
        Box(state, from, to);
        Assert.Equal("012|345\n012|345\n012|345", Show(state));
        state.Type("X");
        Assert.Equal("012X345\n012X345\n012X345", state.Text);
        state.Execute("undo");
        Assert.Equal("012|345\n012|345\n012|345", Show(state));
    }

    /// <summary>Rendered tab stops and spaces select the same text at the same horizontal positions.</summary>
    [Fact]
    public void BoxColumnsFollowTheRenderedTabs()
    {
        var state = Editor("\t012345\n    012345", LanguageServices.PlainText);
        var from = PointOf(state, 2);
        var to = PointOf(state, 16);
        Box(state, from, to);
        Assert.Equal("\t0«123»45\n    0«123»45", Show(state));
    }

    /// <summary>Dragging through part of a text element always selects the complete cluster.</summary>
    [Theory]
    [InlineData("e\u0301")]
    [InlineData("🙂")]
    public void BoxEdgesStayOnGraphemeBoundaries(string cluster)
    {
        var line = cluster + "xyz";
        var state = Editor(line + "\n" + line, LanguageServices.PlainText);
        var start = PointOf(state, 0);
        var clusterEnd = PointOf(state, cluster.Length);
        var width = clusterEnd.X - start.X;
        var from = start + new Vector2(width * 0.2f, 0);
        var to = PointOf(state, line.Length + 1 + cluster.Length) - new Vector2(width * 0.2f, 0);
        Box(state, from, to);
        Assert.Equal(2, state.Document.Selections.Count);
        Assert.All(state.Document.Selections, selection =>
            Assert.Equal(cluster, state.Document.Buffer.GetText(selection.Start, selection.Length)));
        state.Execute("deleteLeft");
        Assert.Equal("xyz\nxyz", state.Text);
        state.Execute("undo");
        Assert.Equal(line + "\n" + line, state.Text);
    }

    /// <summary>Hidden lines inside a fold remain outside the screen-space rectangle.</summary>
    [Fact]
    public void BoxSelectionSkipsFoldedRows()
    {
        var state = Editor("012345\nhidden\nhidden\n012345\n012345",
            LanguageServices.PlainText with { Folding = null });
        state.Folds.SetRanges([new FoldingRange(0, 2)]);
        Assert.True(state.ToggleFold(0));
        var from = PointOf(state, 1);
        var to = PointOf(state, state.Document.Buffer.LineStart(4) + 4);
        Box(state, from, to);
        Assert.Equal("0«123»45\nhidden\nhidden\n0«123»45\n0«123»45", Show(state));
        state.Type("X");
        Assert.Equal("0X45\nhidden\nhidden\n0X45\n0X45", state.Text);
        Assert.True(state.IsFolded(0));
    }

    /// <summary>Read-only documents allow rectangular copying while refusing edits.</summary>
    [Fact]
    public void ReadOnlyBoxCanBeCopiedButNotChanged()
    {
        var state = Editor("012345\n012345", LanguageServices.PlainText,
            new TextEditorSettings { ReadOnly = true });
        var from = PointOf(state, 1);
        var to = PointOf(state, 11);
        Box(state, from, to);
        state.Execute("copy");
        Assert.Equal("123\n123", ((TestClipboard)state.Clipboard!).Text);
        _harness.Input.TypeText("X");
        Frame(state);
        state.Execute("cut");
        state.Execute("paste");
        Assert.Equal("012345\n012345", state.Text);
        Assert.Equal(2, state.Document.Selections.Count);
    }

    /// <summary>Ending the gesture frees the pointer while keeping its selections available for editing.</summary>
    [Fact]
    public void ReleasingTheButtonStopsBoxUpdates()
    {
        var state = Editor("012345\n012345", LanguageServices.PlainText);
        var from = PointOf(state, 1);
        var to = PointOf(state, 11);
        Box(state, from, to);
        var selections = state.Document.Selections.ToArray();
        _harness.Input.MoveTo(from);
        Frame(state);
        Assert.Equal(selections, state.Document.Selections);
        Assert.Null(state.View.BoxAnchor);
        Assert.False(state.View.Selecting);
    }

    /// <summary>Edits during a gesture invalidate its anchor before another selection can use stale offsets.</summary>
    [Fact]
    public void ReplacingTheDocumentCancelsTheGesture()
    {
        var state = Editor("012345\n012345", LanguageServices.PlainText);
        var from = PointOf(state, 1);
        var to = PointOf(state, 11);
        Begin(state, from);
        state.Document.Load("new");
        _harness.Input.MoveTo(to);
        Frame(state);
        Assert.Null(state.View.BoxAnchor);
        Assert.False(state.View.Selecting);
        Assert.Equal("|new", Show(state));
        End(state);
    }

    /// <summary>Escape stops an active rectangle and retains only the primary selection.</summary>
    [Fact]
    public void EscapeStopsTheGesture()
    {
        var state = Editor("012345\n012345", LanguageServices.PlainText);
        var from = PointOf(state, 1);
        var to = PointOf(state, 11);
        Begin(state, from);
        _harness.Input.MoveTo(to);
        Frame(state);
        _harness.Input.ReleaseKey(KeyboardKey.LeftAlt);
        _harness.Input.ReleaseKey(KeyboardKey.LeftShift);
        _harness.Input.PressKey(KeyboardKey.Escape);
        Frame(state);
        Assert.False(state.View.Selecting);
        Assert.Null(state.View.BoxAnchor);
        Assert.Single(state.Document.Selections);
        var primary = state.Document.Primary;
        _harness.Input.ReleaseKey(KeyboardKey.Escape);
        _harness.Input.MoveTo(from);
        Frame(state);
        Assert.Equal(primary, state.Document.Primary);
        End(state);
    }

    /// <summary>Undo and redo preserve the chosen primary caret for subsequent commands and viewport following.</summary>
    [Fact]
    public void MultiCaretUndoRestoresTheChosenPrimary()
    {
        var state = Editor("a|\nb|", LanguageServices.PlainText);
        state.Document.SetSelections(state.Document.Selections, 0);
        state.Type("x");
        state.Type("y");
        Assert.Equal(0, state.Document.PrimaryIndex);
        state.Execute("undo");
        Assert.Equal("a\nb", state.Text);
        Assert.Equal(0, state.Document.PrimaryIndex);
        state.Execute("redo");
        Assert.Equal("axy\nbxy", state.Text);
        Assert.Equal(0, state.Document.PrimaryIndex);
    }

    /// <summary>Clipboard spreading retains selected blank rows and accepts an external final line terminator.</summary>
    [Theory]
    [InlineData("1\n2\n", "1|\n2|\n|")]
    [InlineData("1\n2\n\n", "1|\n2|\n|")]
    [InlineData("1\n\n3", "1|\n|\n3|")]
    public void PasteSpreadsEmptyClipboardRows(string copied, string expected)
    {
        var state = Editor("«a»\n«b»\n«c»", LanguageServices.PlainText);
        ((TestClipboard)state.Clipboard!).Text = copied;
        state.Execute("paste");
        Assert.Equal(expected, Show(state));
    }

    /// <summary>Dragging past an edge scrolls while the rectangle's anchor remains at its original text column.</summary>
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 1)]
    public void BoxSelectionScrollsAtViewportEdges(int horizontal, int vertical)
    {
        var line = string.Concat(Enumerable.Repeat("0123456789", 50));
        var state = Editor(string.Join('\n', Enumerable.Repeat(line, 100)), LanguageServices.PlainText);
        var rowHeight = PointOf(state, line.Length + 1).Y - PointOf(state, 0).Y;
        state.ScrollY = rowHeight * 30;
        state.ScrollX = 20;
        var anchor = (line.Length + 1) * 32 + 10;
        var from = PointOf(state, anchor);
        var initialX = state.ScrollX;
        var initialY = state.ScrollY;
        Begin(state, from);
        _harness.Input.MoveTo(new Vector2(horizontal < 0 ? 0 : horizontal > 0 ? 610 : from.X,
            vertical < 0 ? -10 : vertical > 0 ? 410 : from.Y));
        Frame(state);
        Assert.Equal(horizontal, Math.Sign(state.ScrollX - initialX));
        Assert.Equal(vertical, Math.Sign(state.ScrollY - initialY));
        Assert.All(state.Document.Selections, selection =>
            Assert.Equal(10, selection.Anchor - state.Document.Buffer.LineStart(state.Document.Buffer.LineOf(selection.Anchor))));
        End(state);
    }

    /// <summary>The first and last document rows remain valid endpoints when dragging beyond the viewport.</summary>
    [Fact]
    public void BoxScrollingClampsAtDocumentLimits()
    {
        var state = Editor("012345\n012345", LanguageServices.PlainText);
        var from = PointOf(state, 2);
        Begin(state, from);
        _harness.Input.MoveTo(-20, -20);
        Frame(state);
        Assert.Equal(0f, state.ScrollX);
        Assert.Equal(0f, state.ScrollY);
        Assert.Single(state.Document.Selections);
        _harness.Input.MoveTo(610, 410);
        Frame(state);
        Assert.Equal(2, state.Document.Selections.Count);
        Assert.Equal(state.Text.Length, state.Document.Primary.Caret);
        End(state);
    }

    /// <summary>Alt-click retains additive carets, and Shift-click still extends a linear selection.</summary>
    [Fact]
    public void ExistingPointerSelectionGesturesKeepTheirBehavior()
    {
        var state = Editor("|012345", LanguageServices.PlainText);
        var added = PointOf(state, 2);
        var extended = PointOf(state, 5);
        _harness.Input.PressKey(KeyboardKey.LeftAlt);
        _harness.Input.MoveTo(added);
        _harness.Input.PressButton();
        Frame(state);
        _harness.Input.MoveTo(extended);
        Frame(state);
        Assert.Equal("|01|2345", Show(state));
        End(state);
        Frame(state, 1);
        _harness.Input.PressKey(KeyboardKey.LeftShift);
        _harness.Input.MoveTo(extended);
        _harness.Input.PressButton();
        Frame(state);
        End(state);
        Assert.Equal("01«234»5", Show(state));
    }

    /// <summary>Scrollbar and gutter presses keep their own behavior while box modifiers are held.</summary>
    [Fact]
    public void BoxModifiersDoNotTakeOverTheGutterOrScrollbar()
    {
        var state = Editor(string.Join('\n', Enumerable.Repeat("012345", 100)), LanguageServices.PlainText);
        var row = PointOf(state, 7);
        Begin(state, new Vector2(2, row.Y));
        End(state);
        Assert.Equal(new TextSelection(7, 14), state.Document.Primary);
        Begin(state, new Vector2(599, 10));
        _harness.Input.MoveTo(599, 200);
        Frame(state);
        Assert.True(state.ScrollY > 0);
        Assert.False(state.View.Selecting);
        Assert.Null(state.View.BoxAnchor);
        End(state);
        Assert.Null(state.View.ThumbGrab);
        var scroll = state.ScrollY;
        Begin(state, new Vector2(599, 390));
        Assert.True(state.ScrollY > scroll);
        End(state);
    }

    /// <summary>Every row of a pointer-created rectangle paints its selected background.</summary>
    [Fact]
    public void RectangularSelectionPaintsEverySelectedRow()
    {
        var state = Editor("012345\n012345\n012345", LanguageServices.PlainText);
        var top = PointOf(state, 0);
        var rowHeight = PointOf(state, 7).Y - top.Y;
        var from = PointOf(state, 1);
        var to = PointOf(state, 18);
        Box(state, from, to);
        using var image = _harness.Snapshot();
        using var pixels = image.PeekPixels();
        for (var row = 0; row < 3; row++)
        {
            var color = pixels.GetPixelColor((int)from.X + 2, (int)(top.Y - rowHeight * 0.5f + row * rowHeight) + 1);
            Assert.Equal((SKColor)_options.Theme.Selection, color);
        }
    }
}
