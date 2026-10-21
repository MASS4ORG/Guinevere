using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.CodeEditing;

/// <summary>Sparse ASCII geometry and visible glyphs agree with shared basic shaping.</summary>
public sealed class AsciiLineLayoutTests
{
    /// <summary>Kerning, proportional widths and tabs retain the same carets and rendered positions.</summary>
    [Theory]
    [InlineData("DejaVu Sans", 1f)]
    [InlineData("DejaVu Sans Mono", 1.5f)]
    public void SparseGeometryMatchesSharedLayout(string family, float scale)
    {
        using var harness = new FrameHarness(1000, 400);
        var font = Font.FromFamilyName(family, 14 * scale);
        var text = string.Concat(Enumerable.Repeat("AV To ffi 0123\tWim! ", 40));
        var metrics = new AsciiFontMetrics(font);
        var sparse = AsciiLineLayout.Create(text, metrics, 4, 25)!;
        harness.Frame(gui =>
        {
            var shared = gui.LayoutText(new RichText(text), size: font.Size, font: font,
                options: new TextLayoutOptions { WrapMode = TextWrapMode.None, TabSize = 4 });
            Assert.Equal(shared.Width, sparse.Width, 2);
            for (var offset = 0; offset <= text.Length; offset++)
            {
                Assert.Equal(shared.CaretBounds(offset).X, sparse.XAt(offset), 2);
                var x = sparse.XAt(offset);
                Assert.Equal(shared.OffsetAt(new Vector2(x, 0)), sparse.OffsetAt(x));
            }

            Assert.Equal(0, sparse.OffsetAt(-100));
            Assert.Equal(text.Length, sparse.OffsetAt(sparse.Width + 100));
            var (visible, origin) = sparse.Visible(2000, 200);
            Assert.InRange(visible.Text.Length, 1, 100);
            Assert.InRange(origin, 1700, 2000);
            foreach (var run in visible.Runs)
            {
                Assert.All(run.Glyphs!.X, position => Assert.True(float.IsFinite(position)));
            }
        });
    }

    /// <summary>ASCII fast paths reject characters needing grapheme or fallback analysis.</summary>
    [Theory]
    [InlineData("e\u0301")]
    [InlineData("🙂")]
    [InlineData("\u0001")]
    public void NonAsciiUsesTheSharedShaper(string text)
    {
        var metrics = new AsciiFontMetrics(Font.FromFamilyName("DejaVu Sans", 14));
        Assert.Null(AsciiLineLayout.Create(text, metrics, 4, 20));
    }

    /// <summary>Viewport glyph positions and tab advances paint the same pixels as the whole layout.</summary>
    [Fact]
    public void SparseViewportMatchesSharedPixels()
    {
        using var expected = new FrameHarness(500, 100);
        using var actual = new FrameHarness(500, 100);
        var font = Font.FromFamilyName("DejaVu Sans", 14);
        var text = string.Concat(Enumerable.Repeat("AV\tTo 123 Wmi ", 60));
        const float height = 21;
        const float scroll = 1000;
        var sparse = AsciiLineLayout.Create(text, new AsciiFontMetrics(font), 4, height)!;
        expected.Frame(gui =>
        {
            var layout = gui.LayoutText(new RichText(text), size: 14, font: font,
                options: new TextLayoutOptions { WrapMode = TextWrapMode.None, TabSize = 4, LineHeight = 1.5f });
            gui.DrawTextLayout(layout, new Vector2(-scroll, 0), Color.White);
        });
        actual.Frame(gui =>
        {
            var (visible, x) = sparse.Visible(scroll, 500);
            gui.DrawTextLayout(visible, new Vector2(x - scroll, 0), Color.White);
        });
        using var expectedImage = expected.Snapshot();
        using var actualImage = actual.Snapshot();
        using var expectedPixels = expectedImage.PeekPixels();
        using var actualPixels = actualImage.PeekPixels();
        Assert.True(expectedPixels.GetPixelSpan().SequenceEqual(actualPixels.GetPixelSpan()));
    }

    /// <summary>Exact checkpoint boundaries and empty ranges remain valid geometry.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(256)]
    [InlineData(512)]
    public void CheckpointsIncludeTheEndOfTheLine(int length)
    {
        var metrics = new AsciiFontMetrics(Font.FromFamilyName("DejaVu Sans Mono", 14));
        var line = AsciiLineLayout.Create(new string('x', length), metrics, 0, 20)!;
        Assert.Equal(line.Width, line.XAt(length));
        Assert.Equal(line.Width, line.XAt(length + 100));
        Assert.Equal(0f, line.XAt(-1));
        Assert.Equal(length, line.OffsetAt(line.Width + 100));
        var (visible, _) = line.Visible(0, 100);
        Assert.True(visible.Text.Length <= length);
    }

    /// <summary>A zero tab interval stays finite and does not advance the caret.</summary>
    [Fact]
    public void ZeroTabIntervalLeavesTheCaretInPlace()
    {
        var line = AsciiLineLayout.Create("\tx", new AsciiFontMetrics(Font.FromFamilyName("DejaVu Sans Mono", 14)),
            0, 20)!;
        Assert.Equal(0, line.XAt(1));
        Assert.True(line.Width > 0);
    }
}
