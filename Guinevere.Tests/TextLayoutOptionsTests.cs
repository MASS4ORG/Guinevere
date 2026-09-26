using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

public class TextLayoutOptionsTests
{
    static float Measure(string text) => text.Length;

    [Fact]
    public void WordThenCharacter_SplitsLongWordsAndPreservesOffsets()
    {
        var lines = WrappedTextLayout.Wrap("abcdefgh", 3f, Measure,
            new TextLayoutOptions { WrapMode = TextWrapMode.WordThenCharacter });

        Assert.Equal(["abc", "def", "gh"], lines.Select(line => line.Text));
        Assert.Equal([0, 3, 6], lines.Select(line => line.Start));
    }

    [Fact]
    public void Ellipsis_FitsTheAvailableWidth()
    {
        var lines = WrappedTextLayout.Wrap("abc def ghi", 5f, Measure,
            new TextLayoutOptions { MaxLines = 1, Ellipsis = ".." });

        Assert.Single(lines);
        Assert.Equal("abc..", lines[0].Text);
    }

    [Fact]
    public void CharacterWrap_KeepsEmojiSurrogatePairsTogether()
    {
        var lines = WrappedTextLayout.Wrap("🙂🙂", 2f, Measure,
            new TextLayoutOptions { WrapMode = TextWrapMode.Character });

        Assert.Equal(["🙂", "🙂"], lines.Select(line => line.Text));
        Assert.Equal([0, 2], lines.Select(line => line.Start));
    }

    [Fact]
    public void UnwrappedLines_KeepOriginalOffsets()
    {
        var lines = WrappedTextLayout.Wrap("one\ntwo", 0f, Measure);

        Assert.Equal([0, 4], lines.Select(line => line.Start));
    }

    [Fact]
    public void DrawText_UsesTheSameLineLimitAndLineHeightInBothPasses()
    {
        using var harness = new FrameHarness();
        LayoutNode? node = null;
        harness.Frame(gui => node = gui.DrawText("one\ntwo\nthree", size: 10f,
            layout: new TextLayoutOptions { MaxLines = 2, LineHeight = 1.5f }));

        Assert.NotNull(node);
        Assert.Equal(30f, node.Rect.H, 1);
    }

    [Fact]
    public void StyleDeclarations_SetInheritedTextLayout()
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
