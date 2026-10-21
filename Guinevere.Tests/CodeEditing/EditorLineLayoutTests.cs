using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.CodeEditing;

/// <summary>Line adapters retain shared geometry and invalidate bounded ASCII indexes with their inputs.</summary>
public sealed class EditorLineLayoutTests
{
    /// <summary>Retained indexes track immutable buffers, native font changes, tabs and row height.</summary>
    [Fact]
    public void CacheInvalidatesWithEveryMetricInput()
    {
        var cache = new EditorLineCache();
        var font = Font.FromFamilyName("DejaVu Sans Mono", 14);
        var buffer = new SumTreeTextBuffer("a\tb");
        var key = new FontKey(font, 14);
        var first = cache.Get(buffer, 0, key, 4, 20)!;
        Assert.Same(first, cache.Get(buffer, 0, key, 4, 20));
        cache.Invalidate();
        Assert.NotSame(first, cache.Get(buffer, 0, key, 4, 20));
        Assert.NotSame(first, cache.Get(buffer, 0, key, 8, 20));
        var taller = cache.Get(buffer, 0, key, 8, 30)!;
        Assert.Equal(30, taller.Height);
        font.SkFont.ScaleX = 2;
        var wider = cache.Get(buffer, 0, new FontKey(font, 14), 4, 20)!;
        Assert.True(wider.Width > first.Width);
        var edited = cache.Get(buffer.Replace(0, 1, "long"), 0, new FontKey(font, 14), 4, 20)!;
        Assert.NotSame(wider, edited);
        var unsupported = new SumTreeTextBuffer("e\u0301");
        Assert.Null(cache.Get(unsupported, 0, key, 4, 20));
        Assert.Null(cache.Get(unsupported, 0, key, 4, 20));
    }

    /// <summary>The retained line count and source-character budget both evict older indexes.</summary>
    [Theory]
    [InlineData(17, 17000)]
    [InlineData(9, 1000000)]
    public void CacheHasBoundedRetention(int lines, int length)
    {
        var cache = new EditorLineCache();
        var key = new FontKey(Font.FromFamilyName("DejaVu Sans Mono", 14), 14);
        var buffer = new SumTreeTextBuffer(string.Join('\n', Enumerable.Repeat(new string('x', length), lines)));
        var first = cache.Get(buffer, 0, key, 4, 20);
        for (var line = 1; line < lines; line++)
        {
            Assert.NotNull(cache.Get(buffer, line, key, 4, 20));
        }

        Assert.NotSame(first, cache.Get(buffer, 0, key, 4, 20));
    }

    /// <summary>Adapters expose shared carets, ranges, hit testing and draw paths without changing geometry.</summary>
    [Fact]
    public void AdaptersExposeTheSameGeometry()
    {
        using var harness = new FrameHarness(500, 100);
        var font = Font.FromFamilyName("DejaVu Sans Mono", 14);
        harness.Frame(gui =>
        {
            var shared = gui.LayoutText(new RichText("abcd"), size: 14, font: font);
            var ascii = AsciiLineLayout.Create("abcd", new AsciiFontMetrics(font), 4, shared.Height)!;
            foreach (var adapter in new[] { new EditorLineLayout(shared), new EditorLineLayout(ascii) })
            {
                Assert.Equal(shared.Width, adapter.Width);
                Assert.Equal(shared.CaretBounds(2), adapter.CaretBounds(2));
                Assert.Equal(2, adapter.OffsetAt(new Vector2(shared.CaretBounds(2).X, 0)));
                Assert.Equal(shared.RangeBounds(1, 3), adapter.RangeBounds(3, 1));
                Assert.Empty(adapter.RangeBounds(2, 2));
                adapter.Draw(gui, new Vector2(0, 0), Color.White, 0, 500);
            }
        });
    }
}
