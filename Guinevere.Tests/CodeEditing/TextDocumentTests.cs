namespace Guinevere.Tests.CodeEditing;

public class TextDocumentTests
{
    static TextDocument Document(string text) => new(new SumTreeTextBuffer(text));

    /// <summary>An empty insertion keeps the document version and buffer while moving its selection.</summary>
    [Fact]
    public void EmptyInsertionsDoNotCreateHistory()
    {
        var document = Document("abc");
        var buffer = document.Buffer;

        Assert.False(document.Apply([new TextEdit(1, 0, "")], [TextSelection.At(1)]));
        Assert.Same(buffer, document.Buffer);
        Assert.Equal(0, document.Version);
        Assert.False(document.CanUndo);
        Assert.Equal(1, document.Primary.Caret);
    }

    /// <summary>Caret movement either extends the immutable selection or collapses it at the new position.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovingASelectionPreservesOrReplacesItsAnchor(bool extend)
    {
        var selection = new TextSelection(5, 2);

        var moved = selection.MoveTo(9, extend, 11);

        Assert.Equal(new TextSelection(extend ? 5 : 9, 9, 11), moved);
        Assert.Equal(!extend, moved.IsEmpty);
        Assert.Equal(new TextSelection(5, 2), selection);
    }

    [Fact]
    public void EditsApplyTogetherInOriginalOffsetsAndOverlapsAreDropped()
    {
        var document = Document("abcdef");
        var changes = new List<DocumentChange>();
        document.Changed += changes.Add;

        var changed = document.Apply([new TextEdit(4, 1, "E"), new TextEdit(0, 1, "A\n"), new TextEdit(4, 2, "x")],
            [TextSelection.At(2)]);

        Assert.True(changed);
        Assert.Equal("A\nbcdEf", document.Buffer.Text);
        Assert.Equal(1, document.Version);
        Assert.Equal(new DocumentChange(0, 1, changes[0].Edits), changes.Single());
        Assert.Equal(2, changes[0].Edits.Count);
        Assert.False(document.Apply([], [TextSelection.At(1)]));
        Assert.Equal(1, document.Primary.Caret);
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Apply([new TextEdit(90, 0, "x")], [default]));
    }

    [Fact]
    public void SelectionsAreSortedClampedAndMergedKeepingThePrimary()
    {
        var document = Document("0123456789");

        document.SetSelections([new TextSelection(6, 8), new TextSelection(2, 5), new TextSelection(4, 7),
            TextSelection.At(99), TextSelection.At(9)], primary: 3);

        Assert.Equal([new TextSelection(2, 8), TextSelection.At(9), TextSelection.At(10)], document.Selections);
        Assert.Equal(TextSelection.At(10), document.Primary);
        Assert.Throws<ArgumentException>(() => document.SetSelections([]));
    }

    [Fact]
    public void TypingMergesIntoOneUndoStepUntilTheGroupBreaks()
    {
        var document = Document("");
        foreach (var c in "abc")
        {
            var caret = document.Primary.Caret;
            document.Apply([new TextEdit(caret, 0, c.ToString())], [TextSelection.At(caret + 1)], EditKind.Typing);
        }

        document.BreakUndoGroup();
        document.Apply([new TextEdit(3, 0, "d")], [TextSelection.At(4)], EditKind.Typing);

        Assert.True(document.Undo());
        Assert.Equal("abc", document.Buffer.Text);
        Assert.True(document.Undo());
        Assert.Equal("", document.Buffer.Text);
        Assert.False(document.Undo());
        Assert.True(document.Redo());
        Assert.Equal("abc", document.Buffer.Text);
        Assert.Equal(TextSelection.At(3), document.Primary);
        Assert.True(document.CanRedo);
        document.Apply([new TextEdit(0, 0, "!")], [TextSelection.At(1)]);
        Assert.False(document.CanRedo);
    }

    [Fact]
    public void UndoReportsItsFirstLineWithoutEdits()
    {
        var document = Document("a\nb\nc");
        document.Apply([new TextEdit(4, 0, "x\ny")], [TextSelection.At(7)]);
        DocumentChange? change = null;
        document.Changed += value => change = value;

        document.Undo();

        Assert.Equal(2, change!.Value.FirstLine);
        Assert.Equal(-1, change.Value.LineDelta);
        Assert.Empty(change.Value.Edits);
    }

    [Fact]
    public void LoadingKeepsTheLineBreakForSaving()
    {
        var document = Document("old");
        document.Apply([new TextEdit(0, 0, "x")], [TextSelection.At(1)]);

        document.Load("a\r\nb\r\n");

        Assert.Equal("a\nb\n", document.Buffer.Text);
        Assert.Equal("a\r\nb\r\n", document.ToFileText());
        Assert.False(document.CanUndo);
    }

    [Fact]
    public void OffsetsMapThroughEdits()
    {
        TextEdit[] edits = [new(2, 2, "xyz"), new(6, 0, "!")];

        Assert.Equal(1, TextEdit.Map(1, edits));
        Assert.Equal(2, TextEdit.Map(2, edits));
        Assert.Equal(5, TextEdit.Map(3, edits));
        Assert.Equal(6, TextEdit.Map(5, edits));
        Assert.Equal(8, TextEdit.Map(6, edits));
        Assert.Equal(3, new TextSelection(5, 2).Length);
    }
}
