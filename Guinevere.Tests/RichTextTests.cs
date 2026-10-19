using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>Rich-text spans: composition, value identity, mixed metrics, links and inline images.</summary>
public class RichTextTests
{
    static TextLayout Layout(RichText text, float maxWidth = 0, float size = 14)
    {
        using var harness = new FrameHarness();
        TextLayout? layout = null;
        harness.Frame(gui => layout = gui.LayoutText(text, maxWidth, size));
        return layout!;
    }

    static SKImage Image(int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        return surface.Snapshot();
    }

    [Fact]
    public void OverlappingSpansComposeLaterPropertiesOverEarlierOnes()
    {
        var text = new RichText("abcdef")
            .Style(0, 4, new TextSpanStyle { Color = Color.Red, Weight = 700 })
            .Style(2, 4, TextSpanStyle.Colored(Color.Blue));

        var ranges = text.Content.Ranges;

        Assert.Equal([(0, 2), (2, 4), (4, 6)], ranges.Select(range => (range.Start, range.End)));
        Assert.Equal(Color.Red, ranges[0].Style.Color);
        Assert.Equal(Color.Blue, ranges[1].Style.Color);
        Assert.Equal(700, ranges[1].Style.Weight);
        Assert.Null(ranges[2].Style.Weight);
    }

    [Fact]
    public void EqualContentComparesEqualAndChangesInvalidateTheSnapshot()
    {
        var first = new RichText().Append("bold", TextSpanStyle.Bold).Append(" plain");
        var second = new RichText().Append("bold", TextSpanStyle.Bold).Append(" plain");

        Assert.Equal(first.Content, second.Content);
        Assert.Equal(first.Content.GetHashCode(), second.Content.GetHashCode());

        var before = first.Content;
        first.Append("!");
        Assert.NotEqual(before, first.Content);
        Assert.Equal("bold plain!", first.Text);
        Assert.False(before.Equals(null));
        Assert.False(before.Equals("text"));
    }

    [Fact]
    public void UnchangedContentIsNotLaidOutAgainAcrossFrames()
    {
        using var harness = new FrameHarness();
        static void Draw(Gui gui) => gui.DrawRichText(new RichText().Append("Hello ", TextSpanStyle.Bold).Append("world"));

        harness.Frame(Draw);
        var builds = harness.Gui.TextLayoutBuilds;
        harness.Frame(Draw);
        harness.Frame(Draw);

        Assert.Equal(builds, harness.Gui.TextLayoutBuilds);
    }

    [Fact]
    public void MixedSizesShareOneBaselineAndTheTallestLineBox()
    {
        var layout = Layout(new RichText().Append("small ").Append("BIG", new TextSpanStyle { Size = 28 }));

        Assert.Single(layout.Lines);
        Assert.Equal(layout.Runs[0].Baseline, layout.Runs[^1].Baseline, 3);
        Assert.Equal(28f * 1.2f, layout.Height, 1);
        Assert.True(layout.Runs[^1].Font.Size > layout.Runs[0].Font.Size);
    }

    [Fact]
    public void BaselineShiftRaisesASpan()
    {
        var layout = Layout(new RichText().Append("x").Append("2", new TextSpanStyle { BaselineShift = 4 }));

        Assert.Equal(layout.Runs[0].Baseline - 4f, layout.Runs[1].Baseline, 3);
    }

    [Fact]
    public void LinksHitTestOnlyOverTheirRuns()
    {
        var text = new RichText().Append("see ").AppendLink("docs", "https://example.com").Append(" now");
        var layout = Layout(text);
        var link = layout.Runs.Single(run => run.LinkIndex >= 0);

        var hit = layout.LinkAt(new Vector2(link.X + link.Width * 0.5f, 2f));

        Assert.Equal(new TextLink(4, 8, "https://example.com"), hit);
        Assert.Null(layout.LinkAt(new Vector2(1f, 2f)));
        Assert.Null(layout.LinkAt(new Vector2(link.X + 1f, -1f)));
        Assert.Equal(TextDecoration.Underline, link.Style.Decoration);
    }

    [Fact]
    public void InlineImagesWrapAndHitTestAsOneUnit()
    {
        using var image = Image(20, 30);
        var layout = Layout(new RichText().Append("a").AppendImage(image, 20, 30).Append("b"));
        var run = layout.Runs.Single(value => value.Image is not null);

        Assert.Equal(20f, run.Width, 3);
        Assert.True(layout.Height >= 30f);
        Assert.Equal(1, layout.OffsetAt(new Vector2(run.X + 5f, 5f)));
        Assert.Equal(2, layout.OffsetAt(new Vector2(run.X + 15f, 5f)));
        Assert.Equal(run.X + run.Width, layout.CaretBounds(2).X, 3);
    }

    [Fact]
    public void SpanBoundariesInsideAnImageAreIgnored()
    {
        using var image = Image(4, 4);
        var text = new RichText("abcd").Image(1, 2, image, 4, 4).Style(0, 2, TextSpanStyle.Bold);

        Assert.DoesNotContain(text.Content.Ranges, range => range.Start == 2);
    }

    [Fact]
    public void InvalidRangesThrow()
    {
        var text = new RichText("abc");
        using var image = Image(1, 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => text.Style(2, 5, TextSpanStyle.Bold));
        Assert.Throws<ArgumentOutOfRangeException>(() => text.Style(-1, 1, TextSpanStyle.Bold));
        Assert.Throws<ArgumentOutOfRangeException>(() => text.Image(0, 0, image, 1, 1));
        Assert.Throws<ArgumentException>(() => text.AppendImage(image, 1, 1, ""));
    }

    [Fact]
    public void ImplicitConversionCreatesUnstyledText()
    {
        RichText text = "plain";

        Assert.Equal(5, text.Length);
        Assert.Equal(RichTextContent.Plain("plain"), text.Content);
    }

    [Fact]
    public void StyleWithReplacesOnlySetProperties()
    {
        var merged = new TextSpanStyle { Size = 10, Italic = true }
            .With(TextSpanStyle.Struck with { Background = Color.Yellow, DecorationColor = Color.Red });

        Assert.Equal(10f, merged.Size);
        Assert.True(merged.Italic);
        Assert.Equal(TextDecoration.Strikethrough, merged.Decoration);
        Assert.Equal(Color.Yellow, merged.Background);
        Assert.Equal(Color.Red, merged.DecorationColor);
        Assert.True(TextSpanStyle.Slanted.Italic);
    }

    [Fact]
    public void BoldSpansUseAHeavierFace()
    {
        var layout = Layout(new RichText().Append("a").Append("b", TextSpanStyle.Bold));

        Assert.True(layout.Runs[1].Font.Weight >= 700);
        Assert.True(layout.Runs[0].Font.Weight < 700);
    }
}
