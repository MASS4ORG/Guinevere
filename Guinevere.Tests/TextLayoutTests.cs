using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>The shared text layout: measurement, truncation, hit testing and range geometry.</summary>
public class TextLayoutTests
{
    const string Sentence = "The quick brown fox jumps over the lazy dog";

    static TextLayout Layout(RichText text, float maxWidth = 0, TextLayoutOptions? options = null,
        TextAlignment alignment = TextAlignment.Start, float size = 14)
    {
        using var harness = new FrameHarness();
        TextLayout? layout = null;
        harness.Frame(gui => layout = gui.LayoutText(text, maxWidth, size, options: options, alignment: alignment));
        return layout!;
    }

    public static TheoryData<TextWrapMode, float, int> Options => new()
    {
        { TextWrapMode.None, 1.2f, 0 },
        { TextWrapMode.Word, 1.2f, 0 },
        { TextWrapMode.Word, 1.5f, 2 },
        { TextWrapMode.Character, 1f, 0 },
        { TextWrapMode.WordThenCharacter, 2f, 3 },
    };

    [Theory]
    [MemberData(nameof(Options))]
    public void DrawTextNodeMatchesTheMeasuredLayout(TextWrapMode mode, float lineHeight, int maxLines)
    {
        var options = new TextLayoutOptions { WrapMode = mode, LineHeight = lineHeight, MaxLines = maxLines };
        using var harness = new FrameHarness();
        LayoutNode? node = null;
        TextLayout? layout = null;
        harness.Frame(gui =>
        {
            node = gui.DrawText(Sentence, size: 14, wrapWidth: 90, layout: options);
            layout = gui.LayoutText(Sentence, 90, 14, options: options);
        });

        Assert.Equal(layout!.Width, node!.Rect.W, 3);
        Assert.Equal(layout.Height, node.Rect.H, 3);
        Assert.Equal(layout.Lines.Count * 14 * lineHeight, layout.Height, 2);
    }

    [Fact]
    public void LinesKeepWrapOffsetsAndText()
    {
        var layout = Layout(Sentence, 90);

        Assert.True(layout.Lines.Count > 1);
        var rebuilt = string.Join(" ", Enumerable.Range(0, layout.Lines.Count).Select(layout.LineText));
        Assert.Equal(Sentence, rebuilt);
        Assert.All(layout.Lines, line => Assert.True(line.Width <= 90f + 0.01f));
    }

    [Fact]
    public void CaretAfterATrailingSpaceMovesPastTheSpace()
    {
        var layout = Layout("ab c");

        Assert.True(layout.CaretBounds(3).X > layout.CaretBounds(2).X);
    }

    [Fact]
    public void OffsetAtReturnsEachCharacterFromItsCaretPosition()
    {
        var layout = Layout("Hello world", 0);

        for (var offset = 0; offset <= 11; offset++)
        {
            var caret = layout.CaretBounds(offset);
            Assert.Equal(offset, layout.OffsetAt(new Vector2(caret.X + 0.1f, caret.Y + 1f)));
        }
    }

    [Fact]
    public void OffsetAtClampsOutsideTheBlock()
    {
        var layout = Layout("one\ntwo");

        Assert.Equal(0, layout.OffsetAt(new Vector2(-50, -50)));
        Assert.Equal(7, layout.OffsetAt(new Vector2(500, 500)));
        Assert.Equal(3, layout.OffsetAt(new Vector2(500, 1)));
    }

    [Fact]
    public void SoftWrapBoundaryBelongsToTheNextLine()
    {
        var layout = Layout("abcdefgh", 30, new TextLayoutOptions { WrapMode = TextWrapMode.Character });

        Assert.True(layout.Lines.Count > 1);
        Assert.Equal(1, layout.LineOf(layout.Lines[1].Start));
        Assert.Equal(layout.Lines[1].Y, layout.CaretBounds(layout.Lines[1].Start).Y);
    }

    [Fact]
    public void RangeBoundsReturnsOneRectanglePerTouchedLine()
    {
        var layout = Layout(Sentence, 90);

        var bounds = layout.RangeBounds(0, Sentence.Length);

        Assert.Equal(layout.Lines.Count, bounds.Count);
        Assert.Empty(layout.RangeBounds(3, 3));
        Assert.Equal(layout.RangeBounds(0, 5), layout.RangeBounds(5, 0));
    }

    [Fact]
    public void CharacterBoundsCoversOneTextElement()
    {
        var layout = Layout("a🙂b");

        var emoji = layout.CharacterBounds(1);

        Assert.True(emoji.W > 0f);
        Assert.Equal(layout.CaretBounds(3).X, emoji.X + emoji.W, 2);
        Assert.Equal(Rect.Zero, layout.CharacterBounds(-1));
        Assert.Equal(Rect.Zero, layout.CharacterBounds(4));
    }

    [Theory]
    [InlineData(40f)]
    [InlineData(70f)]
    [InlineData(130f)]
    public void EllipsisStaysInsideTheWidthWhileResizing(float width)
    {
        var layout = Layout(Sentence, width, new TextLayoutOptions { MaxLines = 1 });

        Assert.True(layout.IsTruncated);
        Assert.Single(layout.Lines);
        Assert.EndsWith("…", layout.LineText(0), StringComparison.Ordinal);
        Assert.True(layout.Lines[0].Width <= width + 0.01f);
        Assert.Equal(layout.Lines[0].End, layout.OffsetAt(new Vector2(width, 1)));
    }

    [Fact]
    public void VariationSelectorsKeepSourceOffsetsWithoutDrawing()
    {
        var layout = Layout("A️B");

        Assert.Equal(3, layout.OffsetAt(new Vector2(500, 1)));
        Assert.DoesNotContain(layout.Runs, run => run.Text.Contains('️'));
        Assert.Equal(layout.CaretBounds(1).X, layout.CaretBounds(2).X, 3);
    }

    [Fact]
    public void EmptyTextHasOneLineOfTheFontHeight()
    {
        var layout = Layout("");

        Assert.Single(layout.Lines);
        Assert.Equal(0f, layout.Width);
        Assert.Equal(14f * 1.2f, layout.Height, 3);
        Assert.Equal(0, layout.OffsetAt(new Vector2(10, 10)));
    }

    [Theory]
    [InlineData(TextAlignment.Center, 0.5f)]
    [InlineData(TextAlignment.End, 1f)]
    public void AlignmentOffsetsShorterLines(TextAlignment alignment, float factor)
    {
        var layout = Layout("a\nlonger line", 0, alignment: alignment);

        var first = layout.Lines[0];
        Assert.Equal((layout.Width - first.Width) * factor, first.X, 3);
        Assert.Equal(first.X, layout.Runs[first.FirstRun].X, 3);
        Assert.Equal(0, layout.OffsetAt(new Vector2(first.X - 1f, 1f)));
    }
}
