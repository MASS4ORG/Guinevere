using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>Plain-text cache reuse, invalidation, retention limits and retained layout lifetimes.</summary>
public class TextLayoutCacheTests
{
    /// <summary>Measurement and rendering reuse one layout even when the content is supplied as plain or rich text.</summary>
    [Fact]
    public void PlainMeasurementAndDrawingShareOneLayoutAcrossPassesAndFrames()
    {
        using var harness = new FrameHarness();
        var options = new TextLayoutOptions { WrapMode = TextWrapMode.None };
        TextLayout? retained = null;
        void Draw(Gui gui)
        {
            var width = gui.MeasureTextWidth("Hello", 14);
            var node = gui.DrawText("Hello", 14, centerInRect: false, layout: options);
            var layout = gui.LayoutText("Hello", size: 14, options: options);
            if (gui.Pass == Pass.Pass2Render)
            {
                Assert.Equal(width, node.Rect.W, 3);
            }
            Assert.Equal(width, layout.Width, 3);
            if (retained is not null)
            {
                Assert.Same(retained, layout);
            }

            retained = layout;
        }

        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Equal(1, harness.Gui.TextLayoutBuilds);
    }

    /// <summary>A source mutation cannot change a key already stored in a dictionary.</summary>
    [Fact]
    public void FontKeysKeepTheirIdentityWhenTheSourceFontChanges()
    {
        var font = Font.FromFamilyName("sans-serif");
        var before = new FontKey(font, 14);
        var hash = before.GetHashCode();
        font.SkFont.Embolden = true;
        var after = new FontKey(font, 14);

        Assert.NotEqual(before, after);
        Assert.Equal(hash, before.GetHashCode());
        font.Dispose();
    }

    /// <summary>New font settings apply to new requests while retained layouts preserve their measured appearance.</summary>
    [Fact]
    public void MutatingTheFontRebuildsWithoutChangingAnExistingLayout()
    {
        using var harness = new FrameHarness();
        var font = Font.FromFamilyName("sans-serif", 14);
        TextLayout? first = null;
        harness.Frame(gui => first = gui.LayoutText("Hello", size: 14, font: font));
        var builds = harness.Gui.TextLayoutBuilds;
        font.SkFont.Embolden = true;
        TextLayout? second = null;
        harness.Frame(gui => second = gui.LayoutText("Hello", size: 14, font: font));

        Assert.NotSame(first, second);
        Assert.Equal(builds + 1, harness.Gui.TextLayoutBuilds);
        Assert.False(first!.Runs[0].Font.SkFont.Embolden);
        Assert.True(second!.Runs[0].Font.SkFont.Embolden);
        font.Dispose();
    }

    /// <summary>Caller-owned tab arrays cannot leave a cached layout stale.</summary>
    [Fact]
    public void TabStopChangesInvalidateTheCachedLayout()
    {
        using var harness = new FrameHarness();
        var stops = new[] { 40f };
        var options = new TextLayoutOptions { TabStops = stops };
        TextLayout? first = null;
        harness.Frame(gui => first = gui.LayoutText("a\tb", options: options));
        stops[0] = 80f;
        TextLayout? second = null;
        harness.Frame(gui => second = gui.LayoutText("a\tb", options: options));

        Assert.NotSame(first, second);
        Assert.Equal(40f, second!.CaretBounds(2).X - first!.CaretBounds(2).X, 2);
    }

    /// <summary>Empty mutable tab lists are isolated just like populated lists.</summary>
    [Fact]
    public void AddingTheFirstTabStopDoesNotMutateTheExistingCacheKey()
    {
        using var harness = new FrameHarness();
        var stops = new List<float>();
        var options = new TextLayoutOptions { TabStops = stops };
        TextLayout? first = null;
        harness.Frame(gui => first = gui.LayoutText("a\tb", options: options));
        stops.Add(100f);
        TextLayout? second = null;
        harness.Frame(gui => second = gui.LayoutText("a\tb", options: options));
        Assert.NotSame(first, second);
        Assert.Equal(100f, second!.CaretBounds(2).X, 2);
        stops.Clear();
        harness.Frame(gui => Assert.Equal(first!.Width, gui.LayoutText("a\tb", options: options).Width));
    }

    /// <summary>A layout's explicit drawing size is independent of later source-font size changes.</summary>
    [Fact]
    public void ResizingACallerFontKeepsExplicitSizeLayoutsStable()
    {
        using var harness = new FrameHarness();
        var font = Font.FromFamilyName("sans-serif", 14);
        TextLayout? first = null;
        harness.Frame(gui => first = gui.LayoutText("Hello", size: 14, font: font));
        font.Size = 100;
        harness.Frame(gui => Assert.Same(first, gui.LayoutText("Hello", size: 14, font: font)));
        Assert.Equal(14, first!.Runs[0].Font.Size);
        font.Dispose();
    }

    /// <summary>Cache saturation preserves admitted layouts and still produces drawable overflow results.</summary>
    [Fact]
    public void EntryLimitKeepsHotLayoutsWhileOverflowCanStillBeDrawn()
    {
        using var harness = new FrameHarness();
        TextLayout? retained = null;
        TextLayout? overflow = null;
        harness.Frame(gui =>
        {
            var first = gui.LayoutText("hot", size: 14);
            retained ??= first;
            Assert.Same(retained, first);
            for (var i = 1; i < TextLayoutCache.MaxEntries; i++)
            {
                gui.LayoutText(i.ToString(), size: 14);
            }

            var last = gui.LayoutText("overflow", size: 14);
            if (overflow is not null)
            {
                Assert.NotSame(overflow, last);
            }

            overflow = last;
            gui.DrawTextLayout(last, Vector2.Zero);
        });

        Assert.Equal(TextLayoutCache.MaxEntries + 2, harness.Gui.TextLayoutBuilds);
    }

    /// <summary>Text retention has a separate budget, and aging does not dispose externally retained results.</summary>
    [Fact]
    public void TextBudgetRejectsOversizedEntriesAndAgingKeepsRetainedLayoutsUsable()
    {
        using var harness = new FrameHarness();
        var text = new string('a', (int)TextLayoutCache.MaxWeight / 2);
        TextLayout? first = null;
        TextLayout? second = null;
        TextLayout? retained = null;
        harness.Frame(gui =>
        {
            var one = gui.LayoutText(text);
            first ??= one;
            Assert.Same(first, one);
            var two = gui.LayoutText(text + "b");
            if (second is not null)
            {
                Assert.NotSame(second, two);
            }

            second = two;
        });
        Assert.Equal(3, harness.Gui.TextLayoutBuilds);
        harness.Frame(gui => retained = gui.LayoutText("retained", size: 14));
        var blob = retained!.Runs[0].Glyphs!.Blob(retained.Runs[0].Font.SkFont)!;
        harness.Frame(_ => { });
        harness.Frame(_ => { });
        harness.Frame(gui =>
        {
            Assert.NotSame(retained, gui.LayoutText("retained", size: 14));
            gui.DrawTextLayout(retained, Vector2.Zero);
        });
        Assert.NotEqual(IntPtr.Zero, blob.Handle);
        Assert.Same(blob, retained.Runs[0].Glyphs!.Blob(retained.Runs[0].Font.SkFont));
    }

    /// <summary>Fallback face and synthesis changes refresh Unicode glyphs without altering earlier layouts.</summary>
    [Fact]
    public void FallbackChangesRebuildMixedUnicodeWithoutMutatingPriorFontSnapshots()
    {
        using var harness = new FrameHarness();
        var primary = Font.FromFile(FontRegistryTests.FontPath());
        var icons = Font.FromFile(FontRegistryTests.FontPath("widget-icons.ttf"));
        var other = Font.FromFile(FontRegistryTests.FontPath("icons.ttf"));
        primary.Fallbacks = [icons];
        TextLayout? first = null;
        harness.Frame(gui => first = gui.LayoutText("A\uf007 e\u0301", size: 14, font: primary));
        var icon = first!.Runs.Single(run => run.Text == "\uf007");
        Assert.Same(icons.SkFont.Typeface, icon.Font.SkFont.Typeface);
        icons.SkFont.Embolden = true;
        TextLayout? changed = null;
        harness.Frame(gui => changed = gui.LayoutText("A\uf007 e\u0301", size: 14, font: primary));
        Assert.NotSame(first, changed);
        Assert.False(icon.Font.SkFont.Embolden);
        Assert.True(changed!.Runs.Single(run => run.Text == "\uf007").Font.SkFont.Embolden);
        primary.Fallbacks = [other];
        harness.Frame(gui => Assert.NotSame(changed, gui.LayoutText("A\uf007 e\u0301", size: 14, font: primary)));
        primary.Dispose();
        icons.Dispose();
        other.Dispose();
    }

    /// <summary>Registrations and shaping implementation changes invalidate otherwise identical labels.</summary>
    [Fact]
    public void FontRegistryAndShaperChangesInvalidatePlainLabels()
    {
        using var harness = new FrameHarness();
        TextLayout? first = null;
        harness.Frame(gui => first = gui.LayoutText("Hello", size: 14));
        var font = Font.FromFile(FontRegistryTests.FontPath());
        harness.Gui.Fonts.Register("New", font);
        TextLayout? second = null;
        harness.Frame(gui => second = gui.LayoutText("Hello", size: 14));
        Assert.NotSame(first, second);
        harness.Gui.TextShaper = new BasicTextShaper();
        harness.Frame(gui => Assert.NotSame(second, gui.LayoutText("Hello", size: 14)));
        font.Dispose();
    }

    /// <summary>Widget and emoji fallback lists affect the glyphs, metrics and cache identity.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IconFontFallbacksParticipateInLayoutAndCacheIdentity(bool widget)
    {
        using var harness = new FrameHarness();
        var primary = Font.FromFile(FontRegistryTests.FontPath());
        var emoji = primary.WithSize(12);
        var widgets = primary.WithSize(12);
        var icons = Font.FromFile(FontRegistryTests.FontPath("widget-icons.ttf"));
        var fallback = widget ? widgets : emoji;
        harness.Gui.ConfigureFonts(primary, emoji, widgets);
        TextLayout? first = null;
        harness.Frame(gui => first = gui.LayoutText("A\uf007", size: 14));
        fallback.Fallbacks = [icons];
        TextLayout? second = null;
        harness.Frame(gui => second = gui.LayoutText("A\uf007", size: 14));
        Assert.NotSame(first, second);
        var icon = second!.Runs.Single(run => run.Text == "\uf007");
        Assert.Same(icons.SkFont.Typeface, icon.Font.SkFont.Typeface);
        Assert.NotEqual((ushort)0, Assert.Single(icon.Glyphs!.Ids));
        Assert.Equal(icon.X + icon.Width, second.CaretBounds(2).X, 3);
        primary.Dispose();
        emoji.Dispose();
        widgets.Dispose();
        icons.Dispose();
    }

    /// <summary>Equivalent font instances share identity and preserve their native rendering settings when resized.</summary>
    [Fact]
    public void FontSnapshotsCompareByValueAndPreserveDescriptorsAndSize()
    {
        var source = Font.FromFamilyName("sans-serif");
        source.Families = "Brand";
        source.RequestedWeight = 700;
        source.RequestedItalic = true;
        source.SkFont.SkewX = -0.25f;
        source.SkFont.ScaleX = 1.2f;
        source.SkFont.Subpixel = true;
        source.SkFont.LinearMetrics = true;
        source.SkFont.Hinting = SKFontHinting.None;
        source.Fallbacks = [Font.FromFamilyName("serif")];
        var key = new FontKey(source, 14);
        var equal = new FontKey(source, 14);
        var copy = source.WithSize(18);
        Assert.Equal(key, new FontKey(copy, 14));
        copy.Dispose();
        Assert.True(key.Equals((object)equal));
        Assert.False(key.Equals("font"));
        Assert.Equal(key.GetHashCode(), equal.GetHashCode());
        Assert.NotEqual(key, new FontKey(source, 16));
        var resolved = key.Resolve();
        Assert.Equal(14, resolved.Size);
        Assert.Equal(1.2f, resolved.SkFont.ScaleX);
        Assert.Equal(-0.25f, resolved.SkFont.SkewX);
        Assert.Equal(source.SkFont.Subpixel, resolved.SkFont.Subpixel);
        Assert.Equal(source.SkFont.LinearMetrics, resolved.SkFont.LinearMetrics);
        Assert.Equal(source.SkFont.Hinting, resolved.SkFont.Hinting);
        Assert.Equal(source.SkFont.Edging, resolved.SkFont.Edging);
        Assert.Equal(source.SkFont.BaselineSnap, resolved.SkFont.BaselineSnap);
        Assert.Equal(source.SkFont.EmbeddedBitmaps, resolved.SkFont.EmbeddedBitmaps);
        Assert.Equal(source.SkFont.ForceAutoHinting, resolved.SkFont.ForceAutoHinting);
        Assert.Equal("Brand", resolved.Families);
        Assert.Equal(700, resolved.Weight);
        Assert.True(resolved.Italic);
        Assert.Single(resolved.Fallbacks);
        var resized = key.Resolve(20);
        Assert.Equal(20, resized.Size);
        Assert.Equal(20, resized.Fallbacks[0].Size);
        var scaled = resolved.WithSize(24);
        Assert.Equal(24, scaled.Size);
        Assert.Equal(resolved.SkFont.ScaleX, scaled.SkFont.ScaleX);
        Assert.Equal(resolved.SkFont.Hinting, scaled.SkFont.Hinting);
        Assert.Equal(resolved.SkFont.Subpixel, scaled.SkFont.Subpixel);
        Assert.Equal(resolved.SkFont.LinearMetrics, scaled.SkFont.LinearMetrics);
        scaled.Dispose();
        source.Fallbacks[0].Dispose();
        source.Dispose();
        resolved.Fallbacks[0].Dispose();
        resolved.Dispose();
        resized.Fallbacks[0].Dispose();
        resized.Dispose();
    }
}
