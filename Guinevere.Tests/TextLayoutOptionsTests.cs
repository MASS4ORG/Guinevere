using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

public class TextLayoutOptionsTests
{
    static float Measure(string text) => text.Length;

    [Fact]
    public void WordThenCharacterSplitsLongWordsAndPreservesOffsets()
    {
        var lines = WrappedTextLayout.Wrap("abcdefgh", 3f, Measure,
            new TextLayoutOptions { WrapMode = TextWrapMode.WordThenCharacter });

        Assert.Equal(["abc", "def", "gh"], lines.Select(line => line.Text));
        Assert.Equal([0, 3, 6], lines.Select(line => line.Start));
    }

    [Fact]
    public void EllipsisFitsTheAvailableWidth()
    {
        var lines = WrappedTextLayout.Wrap("abc def ghi", 5f, Measure,
            new TextLayoutOptions { MaxLines = 1, Ellipsis = ".." });

        Assert.Single(lines);
        Assert.Equal("abc..", lines[0].Text);
    }

    [Fact]
    public void CharacterWrapKeepsEmojiSurrogatePairsTogether()
    {
        var lines = WrappedTextLayout.Wrap("🙂🙂", 2f, Measure,
            new TextLayoutOptions { WrapMode = TextWrapMode.Character });

        Assert.Equal(["🙂", "🙂"], lines.Select(line => line.Text));
        Assert.Equal([0, 2], lines.Select(line => line.Start));
    }

    [Fact]
    public void ParagraphOfOnlySpacesKeepsItsLine()
    {
        var lines = WrappedTextLayout.Wrap("a\n  \nb", 10f, Measure);

        Assert.Equal(["a", "", "b"], lines.Select(line => line.Text));
        Assert.Equal([0, 2, 5], lines.Select(line => line.Start));
    }

    [Fact]
    public void EllipsisTruncationNeverSplitsASurrogatePair()
    {
        var lines = WrappedTextLayout.Wrap("ab🙂 cd", 4f, Measure,
            new TextLayoutOptions { MaxLines = 1, Ellipsis = "." });

        Assert.Equal("ab.", lines[0].Text);
    }

    [Fact]
    public void EllipsisWiderThanTheWidthIsShortenedFirst()
    {
        var lines = WrappedTextLayout.Wrap("abc def", 2f, Measure,
            new TextLayoutOptions { MaxLines = 1, Ellipsis = "..." });

        Assert.Equal("..", Assert.Single(lines).Text);
    }

    [Fact]
    public void MultipleSpacesStayInsideAWrappedLine()
    {
        var lines = WrappedTextLayout.Wrap("a  b c", 4f, Measure);

        Assert.Equal(["a  b", "c"], lines.Select(line => line.Text));
    }

    [Fact]
    public void InvalidOptionsThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WrappedTextLayout.Wrap("a", 1f, Measure, new TextLayoutOptions { LineHeight = 0f }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WrappedTextLayout.Wrap("a", 1f, Measure, new TextLayoutOptions { MaxLines = -1 }));
    }

    [Fact]
    public void UnwrappedLinesKeepOriginalOffsets()
    {
        var lines = WrappedTextLayout.Wrap("one\ntwo", 0f, Measure);

        Assert.Equal([0, 4], lines.Select(line => line.Start));
    }

    [Fact]
    public void DrawTextUsesTheSameLineLimitAndLineHeightInBothPasses()
    {
        using var harness = new FrameHarness();
        LayoutNode? node = null;
        harness.Frame(gui => node = gui.DrawText("one\ntwo\nthree", size: 10f,
            layout: new TextLayoutOptions { MaxLines = 2, LineHeight = 1.5f }));

        Assert.NotNull(node);
        Assert.Equal(30f, node.Rect.H, 1);
    }

    [Fact]
    public void FluentNodeOptionsReachTextDrawnInside()
    {
        using var harness = new FrameHarness();
        LayoutNode? node = null;
        TextLayoutOptions? options = null;
        harness.Frame(gui =>
        {
            using (gui.Node().TextWrap(TextWrapMode.Character).LineHeight(2f).MaxLines(1, "..").Enter())
            {
                options = gui.CurrentNodeScope.Get<LayoutNodeScopeTextLayout>().Value;
                node = gui.DrawText("one\ntwo", size: 10f);
            }
        });

        Assert.Equal(new TextLayoutOptions
        {
            WrapMode = TextWrapMode.Character,
            LineHeight = 2f,
            MaxLines = 1,
            Ellipsis = ".."
        }, options);
        Assert.Equal(20f, node!.Rect.H, 1);
    }

    [Fact]
    public void FluentNodeOptionsRejectInvalidValues()
    {
        using var harness = new FrameHarness();
        harness.Frame(gui =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => gui.Node().LineHeight(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => gui.Node().MaxLines(-1));
        });
    }

    [Fact]
    public void StyleDeclarationsSetInheritedTextLayout()
    {
        var node = LayoutNode.CreateRoot(new Gui(), 100f, 100f);
        var style = new ResolvedStyle(new Dictionary<string, string>
        {
            ["text-wrap"] = "word-then-character",
            ["line-height"] = "1.5",
            ["max-lines"] = "2",
            ["text-ellipsis"] = "'..'"
        });

        StyleLayout.Apply(node, style);

        var options = node.Scope.Get<LayoutNodeScopeTextLayout>().Value;
        Assert.Equal(TextWrapMode.WordThenCharacter, options.WrapMode);
        Assert.Equal(1.5f, options.LineHeight);
        Assert.Equal(2, options.MaxLines);
        Assert.Equal("..", options.Ellipsis);
    }
}
