using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>Advanced typography: spacing, tabs, indents, justification, case, kerning and missing glyphs.</summary>
public class TypographyTests
{
    static readonly Font Roboto = Font.FromFile(FontRegistryTests.FontPath());

    static TextLayout Layout(RichText text, TextLayoutOptions options, float maxWidth = 0,
        TextAlignment? alignment = null, ITextShaper? shaper = null)
    {
        using var harness = new FrameHarness();
        if (shaper is not null)
        {
            harness.Gui.TextShaper = shaper;
        }

        TextLayout? layout = null;
        harness.Frame(gui => layout = gui.LayoutText(text, maxWidth, 20, Roboto, options, alignment));
        return layout!;
    }

    static float CaretX(TextLayout layout, int offset) => layout.CaretBounds(offset).X;

    /// <summary>Each run's glyph positions must sit on its caret positions, or drawing and hit testing disagree.</summary>
    static void AssertGlyphsOnCarets(TextLayout layout)
    {
        foreach (var run in layout.Runs.Where(run => run.Glyphs is not null && !run.IsMarker))
        {
            Assert.Equal(run.Glyphs!.Ids.Length, run.Glyphs.X.Length);
            for (var i = 0; i < run.Glyphs.X.Length; i++)
            {
                Assert.Contains(run.Offsets, offset => Math.Abs(offset - run.Glyphs.X[i]) < 0.01f);
            }
        }
    }

    [Fact]
    public void LetterSpacingAddsEmsAfterEveryCharacterAndHitTestingFollows()
    {
        var plain = Layout("abc", new TextLayoutOptions());
        var spaced = Layout("abc", new TextLayoutOptions { LetterSpacing = 0.5f });

        Assert.Equal(plain.Width + 3 * 10f, spaced.Width, 2);
        Assert.Equal(CaretX(plain, 2) + 20f, CaretX(spaced, 2), 2);
        Assert.Equal(2, spaced.OffsetAt(new Vector2(CaretX(spaced, 2) + 0.5f, 5)));
        AssertGlyphsOnCarets(spaced);
    }

    [Fact]
    public void WordSpacingOnlyWidensSpaces()
    {
        var plain = Layout("a b", new TextLayoutOptions());
        var spaced = Layout("a b", new TextLayoutOptions { WordSpacing = 1f });

        Assert.Equal(CaretX(plain, 1), CaretX(spaced, 1), 2);
        Assert.Equal(CaretX(plain, 2) + 20f, CaretX(spaced, 2), 2);
    }

    [Fact]
    public void SpanSpacingOverridesTheBlock()
    {
        var text = new RichText().Append("ab").Append("cd", new TextSpanStyle { LetterSpacing = 0f });
        var layout = Layout(text, new TextLayoutOptions { LetterSpacing = 1f });
        var plain = Layout("abcd", new TextLayoutOptions());

        Assert.Equal(plain.Width + 2 * 20f, layout.Width, 2);
    }

    [Fact]
    public void TabsAdvanceToFixedThenExplicitStops()
    {
        var space = Roboto.Resized(20).SkFont.MeasureText(" ");
        var fixedStops = Layout("a\tb", new TextLayoutOptions { TabSize = 4 });
        var explicitStops = Layout("a\tb\tc", new TextLayoutOptions { TabSize = 4, TabStops = [100f] });

        Assert.Equal(4 * space, CaretX(fixedStops, 2), 2);
        Assert.Equal(100f, CaretX(explicitStops, 2), 2);
        var interval = 4 * space;
        Assert.Equal(MathF.Floor(CaretX(explicitStops, 3) / interval + 1f) * interval, CaretX(explicitStops, 4), 2);
        Assert.Equal(3, explicitStops.Runs.Sum(run => run.Glyphs?.Ids.Length ?? 0));
    }

    [Fact]
    public void TabsCountWhenWrapping()
    {
        var withTab = Layout("a\tb c", new TextLayoutOptions { TabStops = [100f] }, maxWidth: 120);
        var withoutTab = Layout("a b c", new TextLayoutOptions(), maxWidth: 120);

        Assert.Equal(2, withTab.Lines.Count);
        Assert.Single(withoutTab.Lines);
        Assert.All(withTab.Lines, line => Assert.True(line.Width <= 120.01f));
    }

    [Fact]
    public void FirstLineAndHangingIndentsShiftLinesAndShrinkTheirWidth()
    {
        const string Text = "one two three four five six seven\neight";
        var layout = Layout(Text, new TextLayoutOptions { FirstLineIndent = 30, HangingIndent = 10 }, 120);

        Assert.Equal(30f, layout.Runs[layout.Lines[0].FirstRun].X, 2);
        Assert.Equal(10f, layout.Runs[layout.Lines[1].FirstRun].X, 2);
        var eight = layout.Lines[^1];
        Assert.Equal(30f, layout.Runs[eight.FirstRun].X, 2);
        Assert.All(layout.Lines, line => Assert.True(line.Width <= 120.01f));
    }

    [Fact]
    public void JustifiedLinesFillTheWidthExceptTheLast()
    {
        const string Text = "the quick brown fox jumps over the lazy dog again";
        var layout = Layout(Text, new TextLayoutOptions
        {
            Alignment = TextAlignment.Justify,
            LastLineAlignment = TextAlignment.End
        }, 150);

        Assert.True(layout.Lines.Count > 2);
        foreach (var line in layout.Lines.SkipLast(1))
        {
            Assert.Equal(150f, line.Width, 1);
        }

        var last = layout.Lines[^1];
        Assert.True(last.Width < 150f);
        Assert.Equal(150f - last.Width, last.X, 1);
        Assert.Equal(150f, layout.Width, 1);
        AssertGlyphsOnCarets(layout);
        var space = Text.IndexOf(' ', StringComparison.Ordinal);
        Assert.Equal(space + 1, layout.OffsetAt(new Vector2(CaretX(layout, space + 1) + 0.5f, 5)));
    }

    [Fact]
    public void JustifyWithoutWrapWidthUsesTheLastLinePolicy()
    {
        var layout = Layout("a b", new TextLayoutOptions { LastLineAlignment = TextAlignment.Start },
            alignment: TextAlignment.Justify);

        Assert.Equal(0f, layout.Lines[0].X);
    }

    /// <summary>Justification preserves glyph spacing when a wrapped line contains no spaces to stretch.</summary>
    [Fact]
    public void JustificationWithoutSpacesPreservesLineWidths()
    {
        var options = new TextLayoutOptions { WrapMode = TextWrapMode.WordThenCharacter };
        var plain = Layout("abcdefgh", options, 30);
        var justified = Layout("abcdefgh", options with { Alignment = TextAlignment.Justify }, 30);

        Assert.True(justified.Lines.Count > 1);
        Assert.Equal(plain.Lines.Select(line => line.Width), justified.Lines.Select(line => line.Width));
        AssertGlyphsOnCarets(justified);
    }

    /// <summary>Several glyphs belonging to one source character receive letter spacing only once.</summary>
    [Fact]
    public void MultipleGlyphsInOneClusterReceiveOneLetterSpacingAdvance()
    {
        var layout = Layout("a", new TextLayoutOptions { LetterSpacing = 0.5f }, shaper: new TwoGlyphShaper());

        Assert.Equal(17f, layout.Width);
        Assert.Equal(17f, CaretX(layout, 1));
        Assert.Equal([0f, 3f], layout.Runs.Single().Glyphs!.X);
    }

    /// <summary>Two positioned glyphs for one source character, with advances totaling seven pixels.</summary>
    sealed class TwoGlyphShaper : ITextShaper
    {
        /// <inheritdoc />
        public ShapedText Shape(Font font, string text, int start, int length, bool kerning)
        {
            var glyph = font.SkFont.GetGlyph(text[start]);
            return new ShapedText([glyph, glyph], [3f, 4f], [0, 0]);
        }
    }

    /// <summary>Replacing the shaper rebuilds cached text once, then reuses the newly shaped geometry.</summary>
    [Fact]
    public void ChangingTheShaperInvalidatesAndRepopulatesTheLayoutCache()
    {
        using var harness = new FrameHarness();
        TextLayout? layout = null;
        void Draw(Gui gui) => layout = gui.LayoutText("a", size: 20, font: Roboto);

        harness.Frame(Draw);
        var original = layout;
        var builds = harness.Gui.TextLayoutBuilds;
        harness.Gui.TextShaper = new TwoGlyphShaper();
        harness.Frame(Draw);
        var shaped = layout;

        Assert.NotSame(original, shaped);
        Assert.Equal(7f, shaped!.Width);
        Assert.Equal(builds + 1, harness.Gui.TextLayoutBuilds);

        harness.Frame(Draw);

        Assert.Same(shaped, layout);
        Assert.Equal(builds + 1, harness.Gui.TextLayoutBuilds);
    }

    [Theory]
    [InlineData(TextCase.Upper, "HELLO WORLD")]
    [InlineData(TextCase.Lower, "hello world")]
    [InlineData(TextCase.Capitalize, "HELLO World")]
    public void CaseChangesDrawnTextButNotTheSource(TextCase textCase, string drawn)
    {
        var layout = Layout("hELLO world", new TextLayoutOptions { Case = textCase });

        Assert.Equal("hELLO world", layout.Text);
        Assert.Equal(drawn, string.Concat(layout.Runs.Select(run => run.Text)));
    }

    [Fact]
    public void SpanCaseOverridesTheBlock()
    {
        var layout = Layout(new RichText().Append("ab").Append("cd", new TextSpanStyle { Case = TextCase.Upper }),
            new TextLayoutOptions());

        Assert.Equal("abCD", string.Concat(layout.Runs.Select(run => run.Text)));
    }

    [Fact]
    public void KerningTightensPairsAndCanBeTurnedOff()
    {
        var kerned = Layout("AV", new TextLayoutOptions());
        var unkerned = Layout("AV", new TextLayoutOptions { Kerning = false });
        var spanOff = Layout(new RichText().Append("AV", new TextSpanStyle { Kerning = false }),
            new TextLayoutOptions());

        Assert.True(kerned.Width < unkerned.Width);
        Assert.Equal(unkerned.Width, spanOff.Width, 3);
    }

    [Fact]
    public void MissingGlyphPlaceholderReplacesUnrenderableCharacters()
    {
        var withBox = Layout("a\U000F0000b", new TextLayoutOptions { MissingGlyph = "?" });
        var plain = Layout("a\U000F0000b", new TextLayoutOptions());

        var missing = withBox.Runs.Single(run => run.Start == 1);
        Assert.Equal("?", missing.Text);
        Assert.Equal(3, missing.End);
        Assert.Equal(missing.X + missing.Width, CaretX(withBox, 3), 2);
        Assert.DoesNotContain(plain.Runs, run => run.Text == "?");
    }

    [Fact]
    public void ComplexShaperAgreesOnSimpleTextAndHonoursKerning()
    {
        var basic = Layout("Hello", new TextLayoutOptions());
        var shaped = Layout("Hello", new TextLayoutOptions(), shaper: ComplexTextShaper.Instance);
        var kerned = Layout("AV", new TextLayoutOptions(), shaper: ComplexTextShaper.Instance);
        var unkerned = Layout("AV", new TextLayoutOptions { Kerning = false }, shaper: ComplexTextShaper.Instance);

        // Skia's advances are hinted to whole pixels while HarfBuzz scales design units: up to half a pixel a glyph.
        Assert.InRange(Math.Abs(basic.Width - shaped.Width), 0f, 0.5f * "Hello".Length);
        Assert.True(kerned.Width < unkerned.Width);
        Assert.Null(shaped.Runs[0].Glyphs!.Y);
        AssertGlyphsOnCarets(shaped);
    }

    [Fact]
    public void VerticalGlyphOffsetsFromTheShaperReachTheRun()
    {
        var raised = Layout("abc", new TextLayoutOptions(), shaper: new RaisingShaper());

        Assert.Equal([0f, -3f, -6f], raised.Runs.Single().Glyphs!.Y!);
    }

    /// <summary>Basic shaping with each glyph raised three pixels more than the previous, like stacked marks.</summary>
    sealed class RaisingShaper : ITextShaper
    {
        public ShapedText Shape(Font font, string text, int start, int length, bool kerning)
        {
            var basic = BasicTextShaper.Instance.Shape(font, text, start, length, kerning);
            return basic with { YOffsets = [.. Enumerable.Range(0, basic.Glyphs.Length).Select(i => i * -3f)] };
        }
    }

    [Fact]
    public void RaisedGlyphsBuildAPositionedBlob()
    {
        var run = Layout("AV", new TextLayoutOptions()).Runs[0];
        var glyphs = new RunGlyphs(run.Glyphs!.Ids, run.Glyphs.X, [0f, -6f]);

        using var blob = glyphs.Blob(run.Font.SkFont);

        Assert.NotNull(blob);
        Assert.True(blob!.Bounds.Top < run.Glyphs.Blob(run.Font.SkFont)!.Bounds.Top);
    }

    [Fact]
    public void ShaperMustNotBeNull()
    {
        var gui = new Gui();

        Assert.Throws<ArgumentNullException>(() => gui.TextShaper = null!);
        Assert.Same(BasicTextShaper.Instance, gui.TextShaper);
    }

    [Fact]
    public void FluentAndStyleSheetsSetEveryTypographyOption()
    {
        var expected = new TextLayoutOptions
        {
            LetterSpacing = 0.1f,
            WordSpacing = 0.2f,
            TabSize = 2,
            TabStops = [40f, 80f],
            FirstLineIndent = 12,
            HangingIndent = 6,
            Alignment = TextAlignment.Justify,
            LastLineAlignment = TextAlignment.Center,
            Case = TextCase.Upper,
            Kerning = false,
            MissingGlyph = "□"
        };
        using var harness = new FrameHarness();
        TextLayoutOptions? fluent = null;
        harness.Frame(gui =>
        {
            using (gui.Node().LetterSpacing(0.1f).WordSpacing(0.2f).TabStops(2, 80f, 40f).TextIndent(12, 6)
                       .TextAlign(TextAlignment.Justify, TextAlignment.Center).TextCase(TextCase.Upper)
                       .Kerning(false).MissingGlyph("□").Enter())
            {
                fluent = gui.CurrentNodeScope.Get<LayoutNodeScopeTextLayout>().Value;
            }
        });

        var node = LayoutNode.CreateRoot(new Gui(), 100f, 100f);
        StyleLayout.Apply(node, new ResolvedStyle(new Dictionary<string, string>
        {
            ["letter-spacing"] = "0.1em",
            ["word-spacing"] = "0.2",
            ["tab-size"] = "2",
            ["tab-stops"] = "80 40",
            ["text-indent"] = "12",
            ["hanging-indent"] = "6",
            ["text-align"] = "justify",
            ["text-align-last"] = "center",
            ["text-transform"] = "uppercase",
            ["font-kerning"] = "none",
            ["missing-glyph"] = "'□'"
        }));

        Assert.Equal(expected, fluent);
        Assert.Equal(expected, node.Scope.Get<LayoutNodeScopeTextLayout>().Value);
        Assert.Equal(expected.GetHashCode(), fluent!.GetHashCode());
    }

    [Fact]
    public void InvalidStyleValuesKeepTheInheritedOptions()
    {
        var node = LayoutNode.CreateRoot(new Gui(), 100f, 100f);
        StyleLayout.Apply(node, new ResolvedStyle(new Dictionary<string, string>
        {
            ["letter-spacing"] = "wide",
            ["text-align"] = "(0.5, 0.5)",
            ["tab-size"] = "-1",
            ["text-indent"] = "10%",
            ["text-transform"] = "shout",
            ["missing-glyph"] = "none"
        }));

        Assert.Equal(new TextLayoutOptions(), node.Scope.Get<LayoutNodeScopeTextLayout>().Value);
    }

    [Fact]
    public void PositionedGlyphRunsBuildOneBlobAndDrawPixels()
    {
        var layout = Layout("A V", new TextLayoutOptions { LetterSpacing = 0.3f });
        var run = layout.Runs[0];
        var blob = run.Glyphs!.Blob(run.Font.SkFont);
        using var paint = new SKPaint { Color = SKColors.White };
        var text = new Text(blob!, run.Text, new Vector2(10, 30), run.Font.SkFont, paint, null);
        using var surface = SKSurface.Create(new SKImageInfo(120, 50));
        surface.Canvas.Clear(SKColors.Transparent);

        text.Render(null!, null!, surface.Canvas);

        Assert.Same(blob, run.Glyphs.Blob(run.Font.SkFont));
        Assert.Null(new RunGlyphs([], []).Blob(run.Font.SkFont));
        Assert.False(((IInkBounds)text).InkBounds(null!)!.Value.IsEmpty);
        using var image = surface.Snapshot();
        using var pixmap = image.PeekPixels();
        Assert.Contains(pixmap.GetPixelSpan().ToArray().Chunk(4), pixel => pixel[3] > 0);
    }
}
