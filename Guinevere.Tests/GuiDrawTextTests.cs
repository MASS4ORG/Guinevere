using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>Drawing text held in a buffer: it shares layouts with strings, selects, wraps and allocates less.</summary>
public class GuiDrawTextTests
{
    const string Sentence = "The quick brown fox jumps over the lazy dog, again and again.";

    static (LayoutNode Node, TextLayout Layout) Plain(Gui gui, char[] buffer, TextLayoutOptions options) =>
        gui.PlainTextBlock(buffer.AsSpan(), null, 14f, 0f, options, TextAlignment.Start);

    /// <summary>Equal text and settings reach one cached layout whichever form asks first, across frames.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SpanAndStringTextShareOneCachedLayout(bool spanFirst)
    {
        using var harness = new FrameHarness();
        var buffer = "Hello".ToCharArray();
        var seen = new List<TextLayout>();
        void Draw(Gui gui)
        {
            var options = gui.CurrentNodeScope.Get<LayoutNodeScopeTextLayout>().Value;
            if (spanFirst)
            {
                seen.Add(Plain(gui, buffer, options).Layout);
            }

            seen.Add(gui.LayoutText("Hello", size: 14, options: options));
            if (!spanFirst)
            {
                seen.Add(Plain(gui, buffer, options).Layout);
            }
        }

        harness.Frame(Draw);
        harness.Frame(Draw);

        Assert.All(seen, layout => Assert.Same(seen[0], layout));
        Assert.Equal(1, harness.Gui.TextLayoutBuilds);
    }

    /// <summary>A span draws a node of the same size as the equal string.</summary>
    [Fact]
    public void SpanAndStringNodesHaveTheSameSize()
    {
        using var harness = new FrameHarness();
        var buffer = Sentence.ToCharArray();
        LayoutNode? fromString = null;
        LayoutNode? fromSpan = null;
        harness.Frame(gui =>
        {
            fromString = gui.DrawText(Sentence, 14, wrapWidth: 200);
            fromSpan = gui.DrawText(buffer, 14, wrapWidth: 200);
        });

        Assert.Equal(fromString!.Rect.W, fromSpan!.Rect.W, 3);
        Assert.Equal(fromString.Rect.H, fromSpan.Rect.H, 3);
        Assert.Equal(1, harness.Gui.TextLayoutBuilds);
    }

    /// <summary>A value formatted into a reused buffer allocates less per warm frame than a string.</summary>
    [Fact]
    public void AWarmSpanFrameAllocatesLessThanTheStringFrame()
    {
        var buffer = new char[16];
        const int Value = 123456;
        var span = SteadyBytesPerFrame(gui =>
        {
            Value.TryFormat(buffer, out var written);
            gui.DrawText(buffer.AsSpan(0, written));
        });
        var text = SteadyBytesPerFrame(gui => gui.DrawText(Value.ToString()));

        Assert.True(span < text, $"span {span} B/frame, string {text} B/frame");
    }

    static long SteadyBytesPerFrame(Action<Gui> draw)
    {
        _ = BytesPerFrame(draw);
        return BytesPerFrame(draw);
    }

    static long BytesPerFrame(Action<Gui> draw)
    {
        using var harness = new FrameHarness(560, 400);
        for (var frame = 0; frame < 60; frame++)
        {
            harness.Frame(draw);
        }

        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 300; frame++)
        {
            harness.Frame(draw);
        }

        return (GC.GetAllocatedBytesForCurrentThread() - before) / 300;
    }

    /// <summary>Selectable buffered text selects and copies the text it drew.</summary>
    [Fact]
    public void SelectableSpanTextSelectsAndCopies()
    {
        const string Text = "hello world";
        using var h = new FrameHarness();
        var buffer = Text.ToCharArray();
        void Draw(Gui gui) => gui.DrawText(buffer.AsSpan(0, buffer.Length), selectable: true);
        TextEditState State() =>
            h.Gui.ControlState($"{h.Gui.RootNode!.Children[0].Id}/selection", static () => new TextEditState());

        h.Frame(Draw);
        h.Input.MoveTo(1, 5);
        h.Input.PressButton();
        h.Frame(Draw);
        h.Input.MoveTo(300, 5);
        h.Frame(Draw);
        h.Input.ReleaseButton();
        h.Frame(Draw);
        Assert.Equal(Text, State().SelectedText);
        h.Input.PressKey(KeyboardKey.LeftControl);
        h.Input.PressKey(KeyboardKey.C);
        h.Frame(Draw);

        Assert.Equal(Text, h.Input.GetClipboardText());
        Assert.Equal(Text, State().Text);
    }

    /// <summary>A buffered text laid out to the fill width reflows exactly as the string does.</summary>
    [Fact]
    public void FillWidthSpanTextLaysOutLikeTheString()
    {
        using var harness = new FrameHarness();
        var buffer = Sentence.ToCharArray();
        var width = 300f;
        LayoutNode? fromString = null;
        LayoutNode? fromSpan = null;
        void Draw(Gui gui)
        {
            using (gui.Node(width, 400).Enter())
            {
                fromString = gui.DrawText(Sentence, 14, wrapWidth: TextLayoutOptions.FillWidth);
            }

            using (gui.Node(width, 400).Enter())
            {
                fromSpan = gui.DrawText(buffer, 14, wrapWidth: TextLayoutOptions.FillWidth);
            }
        }

        harness.Frame(Draw);
        var wideHeight = fromSpan!.Rect.H;
        width = 120f;
        harness.Frame(Draw);

        Assert.True(fromSpan.Rect.H > wideHeight);
        Assert.Equal(fromString!.Rect.W, fromSpan.Rect.W, 3);
        Assert.Equal(fromString.Rect.H, fromSpan.Rect.H, 3);
    }

    /// <summary>An unchanged value drawn from a buffer adds nothing over an existing string.</summary>
    [Fact]
    public void AnUnchangedSpanValueAllocatesNoMoreThanAnExistingString()
    {
        const int Value = 123456;
        const string Existing = "123456";
        var buffer = new char[16];
        var span = WarmFrameBytes(gui =>
        {
            Value.TryFormat(buffer, out var written);
            gui.DrawText(buffer.AsSpan(0, written));
        });
        var text = WarmFrameBytes(gui => gui.DrawText(Existing));

        Assert.True(span <= text, $"span {span} B per 50 frames, existing string {text} B");
    }

    static long WarmFrameBytes(Action<Gui> draw)
    {
        using var harness = new FrameHarness(560, 400);
        for (var frame = 0; frame < 60; frame++)
        {
            harness.Frame(draw);
        }

        return AllocationProbe.Steady(() =>
        {
            for (var frame = 0; frame < 50; frame++)
            {
                harness.Frame(draw);
            }
        });
    }

    /// <summary>An empty or default span draws the same empty node as an empty string, sharing its layout.</summary>
    [Fact]
    public void EmptyAndDefaultSpansDrawLikeAnEmptyString()
    {
        using var harness = new FrameHarness();
        LayoutNode? fromString = null;
        LayoutNode? fromEmpty = null;
        LayoutNode? fromDefault = null;
        harness.Frame(gui =>
        {
            ReadOnlySpan<char> empty = [];
            ReadOnlySpan<char> missing = default;
            fromString = gui.DrawText("", 14);
            fromEmpty = gui.DrawText(empty, 14);
            fromDefault = gui.DrawText(missing, 14);
        });

        Assert.Equal(fromString!.Rect.W, fromEmpty!.Rect.W, 3);
        Assert.Equal(fromString.Rect.H, fromEmpty.Rect.H, 3);
        Assert.Equal(fromString.Rect.W, fromDefault!.Rect.W, 3);
        Assert.Equal(fromString.Rect.H, fromDefault.Rect.H, 3);
        Assert.Equal(1, harness.Gui.TextLayoutBuilds);
    }

    /// <summary>A layout kept before other text is laid out keeps its own runs, offsets and glyphs.</summary>
    [Fact]
    public void LayingOutOtherTextDoesNotChangeALayoutAlreadyReturned()
    {
        const string Other = "A different and much longer sentence that wraps in other places, and keeps going on.";
        TextLayout? first = null;
        using var harness = new FrameHarness();
        harness.Frame(gui =>
        {
            first = Wrapped(gui, Sentence.ToCharArray(), 90f);
            Wrapped(gui, Other.ToCharArray(), 140f);
            Wrapped(gui, "x".ToCharArray(), 0f);
        });
        TextLayout? fresh = null;
        using var other = new FrameHarness();
        other.Frame(gui => fresh = Wrapped(gui, Sentence.ToCharArray(), 90f));

        Assert.True(first!.Lines.Count > 2);
        AssertSameLayout(fresh!, first);
    }

    /// <summary>A shaper that throws mid-build surfaces and leaves nothing stale or held for later builds.</summary>
    [Fact]
    public void AShaperThatThrowsSurfacesAndLaterLayoutsAreUnaffected()
    {
        using var harness = new FrameHarness();
        var fonts = harness.Gui.Fonts;
        var plain = Request(Sentence, BasicTextShaper.Instance, 90f);
        var expected = TextLayoutEngine.Create(plain, fonts);
        var before = BuildBytes(plain, fonts);
        var shaper = new DelegatingShaper
        {
            Hook = calls => calls == 2 ? throw new InvalidOperationException("shape") : null
        };
        var twoRanges = new RichText().Append("ab").Append("cd", TextSpanStyle.Bold).Content;

        Assert.Throws<InvalidOperationException>(() => TextLayoutEngine.Create(Request(twoRanges, shaper), fonts));

        Assert.Equal(2, shaper.Calls);
        AssertSameLayout(expected, TextLayoutEngine.Create(plain, fonts));
        Assert.True(BuildBytes(plain, fonts) <= before, "the scratch state stayed claimed after the exception");
    }

    /// <summary>A shaper that lays out other text inside Shape leaves both layouts and later ones correct.</summary>
    [Fact]
    public void AShaperThatBuildsAnotherLayoutLeavesBothAndLaterLayoutsCorrect()
    {
        const string Inner = "Nested text laid out from inside the shaper.";
        using var harness = new FrameHarness();
        var fonts = harness.Gui.Fonts;
        var outerRequest = Request(Sentence, BasicTextShaper.Instance, 90f);
        var innerRequest = Request(Inner, BasicTextShaper.Instance, 70f);
        var expectedOuter = TextLayoutEngine.Create(outerRequest, fonts);
        var expectedInner = TextLayoutEngine.Create(innerRequest, fonts);
        var before = BuildBytes(outerRequest, fonts);
        TextLayout? nested = null;
        var shaper = new DelegatingShaper
        {
            Hook = _ =>
            {
                nested ??= TextLayoutEngine.Create(innerRequest, fonts);
                return null;
            }
        };

        var outer = TextLayoutEngine.Create(Request(Sentence, shaper, 90f), fonts);

        AssertSameLayout(expectedOuter, outer);
        AssertSameLayout(expectedInner, nested!);
        AssertSameLayout(expectedOuter, TextLayoutEngine.Create(outerRequest, fonts));
        Assert.True(BuildBytes(outerRequest, fonts) <= before, "the scratch state stayed claimed after nesting");
    }

    /// <summary>A shaper that calls <c>LayoutText</c> inside Shape builds both layouts correctly.</summary>
    [Fact]
    public void AShaperThatCallsLayoutTextBuildsBothLayoutsCorrectly()
    {
        const string Inner = "Nested text laid out from inside the shaper.";
        const string Later = "Another sentence entirely, laid out afterwards.";
        TextLayout? expectedOuter = null;
        TextLayout? expectedInner = null;
        TextLayout? expectedLater = null;
        using var reference = new FrameHarness();
        reference.Frame(gui =>
        {
            expectedOuter = gui.LayoutText(Sentence, 90, 14);
            expectedInner = gui.LayoutText(Inner, 70, 14);
            expectedLater = gui.LayoutText(Later, 90, 14);
        });
        using var harness = new FrameHarness();
        TextLayout? nested = null;
        var entered = false;
        var shaper = new DelegatingShaper();
        harness.Gui.TextShaper = shaper;
        shaper.Hook = _ =>
        {
            if (!entered)
            {
                entered = true;
                nested = harness.Gui.LayoutText(Inner, 70, 14);
            }

            return null;
        };
        TextLayout? outer = null;
        TextLayout? later = null;

        harness.Frame(gui =>
        {
            outer = gui.LayoutText(Sentence, 90, 14);
            later = gui.LayoutText(Later, 90, 14);
        });

        AssertSameLayout(expectedOuter!, outer!);
        AssertSameLayout(expectedInner!, nested!);
        AssertSameLayout(expectedLater!, later!);
    }

    static readonly Font TextFont = Font.FromFamilyName("sans-serif", 14);

    static TextLayoutRequest Request(TextLayoutContent content, ITextShaper shaper, float maxWidth = 0f)
    {
        var key = new FontKey(TextFont, 14);
        return new PlainTextRequest(default, key, key, key, 1f, maxWidth, new TextLayoutOptions(),
            TextAlignment.Start, 0, shaper).ToRequest(content);
    }

    static long BuildBytes(TextLayoutRequest request, FontRegistry fonts) =>
        AllocationProbe.Steady(() => TextLayoutEngine.Create(request, fonts));

    /// <summary>Basic shaping that counts its calls and lets a hook run first or replace the result.</summary>
    sealed class DelegatingShaper : ITextShaper
    {
        internal Func<int, ShapedText?>? Hook { get; set; }

        internal int Calls { get; private set; }

        public ShapedText Shape(Font font, string text, int start, int length, bool kerning) =>
            Hook?.Invoke(++Calls) ?? BasicTextShaper.Instance.Shape(font, text, start, length, kerning);
    }

    static TextLayout Wrapped(Gui gui, char[] text, float wrapWidth) =>
        gui.PlainTextBlock(text, null, 14f, wrapWidth, new TextLayoutOptions(), TextAlignment.Start).Layout;

    static void AssertSameLayout(TextLayout expected, TextLayout actual)
    {
        Assert.Equal(expected.Text, actual.Text);
        Assert.Equal(expected.Width, actual.Width, 3);
        Assert.Equal(expected.Height, actual.Height, 3);
        Assert.Equal(expected.Lines, actual.Lines);
        Assert.Equal(expected.Runs.Count, actual.Runs.Count);
        for (var i = 0; i < expected.Runs.Count; i++)
        {
            var (e, a) = (expected.Runs[i], actual.Runs[i]);
            Assert.Equal((e.Start, e.End, e.Text, e.X, e.Width, e.Baseline, e.IsMarker),
                (a.Start, a.End, a.Text, a.X, a.Width, a.Baseline, a.IsMarker));
            Assert.Equal(e.Offsets, a.Offsets);
            Assert.Equal(e.Glyphs?.Ids, a.Glyphs?.Ids);
            Assert.Equal(e.Glyphs?.X, a.Glyphs?.X);
            Assert.Equal(e.Glyphs?.Y, a.Glyphs?.Y);
        }
    }
}
