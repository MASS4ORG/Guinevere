using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>Native font reuse, mutable wrapper isolation and bounded resource retention.</summary>
public class FontCacheTests
{
    /// <summary>Font access and different layout results share the same native font across passes and frames.</summary>
    [Fact]
    public void AccessAndChangingLayoutsReuseNativeFontsAcrossFrames()
    {
        using var harness = new FrameHarness();
        var source = Font.FromFamilyName("sans-serif");
        harness.Gui.ConfigureFonts(source);
        SKFont? retained = null;
        for (var frame = 0; frame < 3; frame++)
        {
            var text = $"Health: {frame}";
            harness.Frame(gui =>
            {
                var accessed = gui.GetTextFont(14);
                retained ??= accessed.SkFont;
                Assert.Same(retained, accessed.SkFont);
                var layout = gui.LayoutText(text, size: 14);
                Assert.Same(retained, layout.Runs[0].Font.SkFont);
                Assert.Equal(layout.Width, gui.MeasureTextWidth(text, 14));
                gui.DrawText(text, 14);
                accessed.Dispose();
            });
        }

        Assert.NotEqual(IntPtr.Zero, retained!.Handle);
        Assert.Equal(1, harness.Gui.TextFontBuilds);
        source.Dispose();
    }

    /// <summary>Layout-cache hits and idle frames do not evict a font needed by the next changing label.</summary>
    [Fact]
    public void FontsSurviveLayoutCacheHitsAndIdleFrames()
    {
        using var harness = new FrameHarness();
        TextLayout? retained = null;
        harness.Frame(gui => retained = gui.LayoutText("steady", size: 14));
        var native = retained!.Runs[0].Font.SkFont;
        for (var frame = 0; frame < 4; frame++)
        {
            harness.Frame(gui => Assert.Same(retained, gui.LayoutText("steady", size: 14)));
        }

        harness.Frame(_ => { });
        harness.Frame(_ => { });
        harness.Frame(gui => Assert.Same(native, gui.LayoutText("changed", size: 14).Runs[0].Font.SkFont));
        Assert.Equal(1, harness.Gui.TextFontBuilds);
    }

    /// <summary>Resizing and disposing returned wrappers cannot change other callers or retained layouts.</summary>
    [Fact]
    public void ReturnedFontsKeepIndependentMutationAndDisposal()
    {
        using var harness = new FrameHarness();
        harness.Frame(gui =>
        {
            var first = gui.GetTextFont(14);
            var second = gui.GetTextFont(14);
            var layout = gui.LayoutText("retained", size: 14);
            Assert.NotSame(first, second);
            first.Size = 30;
            Assert.Equal(14, second.Size);
            Assert.Equal(14, layout.Runs[0].Font.Size);
            first.Dispose();
            first.Dispose();
            Assert.Throws<ObjectDisposedException>(() => first.Size);
            Assert.Throws<ObjectDisposedException>(() => first.Size = 14);
            Assert.Throws<ObjectDisposedException>(() => first.WithSize(14));
            Assert.Throws<ObjectDisposedException>(() => first.MeasureText("text"));
            Assert.True(second.MeasureText("text").W > 0);
            second.Dispose();
            gui.DrawTextLayout(layout, Vector2.Zero);
            Assert.Equal(14, gui.GetTextFont(14).Size);
        });
    }

    /// <summary>A caller can resize a layout's exposed font without changing the font in another result.</summary>
    [Fact]
    public void LayoutFontMutationDoesNotChangeOtherResults()
    {
        using var harness = new FrameHarness();
        harness.Frame(static gui =>
        {
            var first = gui.LayoutText("first", size: 14);
            var second = gui.LayoutText("second", size: 14);
            first.Runs[0].Font.Size = 28;
            Assert.Equal(14, second.Runs[0].Font.Size);
            Assert.Equal(14, gui.LayoutText("third", size: 14).Runs[0].Font.Size);
            if (gui.Pass == Pass.Pass2Render)
            {
                first.Runs[0].Font.Dispose();
            }
            gui.DrawTextLayout(second, Vector2.Zero);
        });
    }

    /// <summary>Every mutable native rendering option selects a new cache entry and preserves the old snapshot.</summary>
    [Theory]
    [InlineData(nameof(SKFont.Embolden), true)]
    [InlineData(nameof(SKFont.SkewX), -0.25f)]
    [InlineData(nameof(SKFont.ScaleX), 1.25f)]
    [InlineData(nameof(SKFont.Edging), SKFontEdging.Alias)]
    [InlineData(nameof(SKFont.Hinting), SKFontHinting.None)]
    [InlineData(nameof(SKFont.Subpixel), true)]
    [InlineData(nameof(SKFont.LinearMetrics), true)]
    [InlineData(nameof(SKFont.BaselineSnap), false)]
    [InlineData(nameof(SKFont.EmbeddedBitmaps), true)]
    [InlineData(nameof(SKFont.ForceAutoHinting), true)]
    public void NativeConfigurationChangesUseDistinctCachedFonts(string property, object value)
    {
        var source = Font.FromFile(FontRegistryTests.FontPath());
        var cache = new FontCache();
        var before = new FontKey(source, 14).Resolve(cache: cache);
        var setting = typeof(SKFont).GetProperty(property)!;
        var original = setting.GetValue(before.SkFont);
        setting.SetValue(source.SkFont, value);
        var after = new FontKey(source, 14).Resolve(cache: cache);
        Assert.NotSame(before.SkFont, after.SkFont);
        Assert.Equal(original, setting.GetValue(before.SkFont));
        Assert.Equal(value, setting.GetValue(after.SkFont));
        setting.SetValue(source.SkFont, original);
        Assert.Same(before.SkFont, new FontKey(source, 14).Resolve(cache: cache).SkFont);
        source.Dispose();
        before.Dispose();
        after.Dispose();
    }

    /// <summary>Typeface and size distinguish entries while equivalent source wrappers share native fonts.</summary>
    [Fact]
    public void NativeKeysDistinguishTypefaceSizeAndPreserveFallbackSnapshots()
    {
        var source = Font.FromFile(FontRegistryTests.FontPath());
        var fallback = Font.FromFile(FontRegistryTests.FontPath("icons.ttf"));
        var cache = new FontCache();
        source.Fallbacks = [fallback];
        var first = new FontKey(source, 14).Resolve(cache: cache);
        var equivalent = source.WithSize(20);
        Assert.Same(first.SkFont, new FontKey(equivalent, 14).Resolve(cache: cache).SkFont);
        Assert.NotSame(first.SkFont, new FontKey(source, 15).Resolve(cache: cache).SkFont);
        Assert.NotSame(first.SkFont, new FontKey(fallback, 14).Resolve(cache: cache).SkFont);
        Assert.Equal(14, first.Fallbacks[0].Size);
        fallback.SkFont.Embolden = true;
        var changed = new FontKey(source, 14).Resolve(cache: cache);
        Assert.Same(first.SkFont, changed.SkFont);
        Assert.NotSame(first.Fallbacks[0].SkFont, changed.Fallbacks[0].SkFont);
        Assert.False(first.Fallbacks[0].SkFont.Embolden);
        Assert.True(changed.Fallbacks[0].SkFont.Embolden);
        source.Dispose();
        fallback.Dispose();
        equivalent.Dispose();
    }

    /// <summary>Synthesized styles share native fonts while preserving native options and caller independence.</summary>
    [Fact]
    public void SynthesizedStylesReuseNativeFontsWithoutChangingTheirSource()
    {
        var source = Font.FromFile(FontRegistryTests.FontPath());
        var cache = new FontCache();
        var first = source.Restyled(700, true, cache);
        var second = source.Restyled(700, true, cache);
        Assert.Same(first.SkFont, second.SkFont);
        Assert.NotSame(first.SkFont, source.SkFont);
        Assert.True(first.SkFont.Embolden);
        Assert.Equal(-0.25f, first.SkFont.SkewX);
        Assert.False(source.SkFont.Embolden);
        first.Size = 28;
        Assert.Equal(source.Size, second.Size);
        Assert.NotSame(first.SkFont, second.SkFont);
        var upright = source.Restyled(400, false, cache);
        Assert.False(upright.SkFont.Embolden);
        Assert.Equal(0, upright.SkFont.SkewX);
        source.Dispose();
        first.Dispose();
        second.Dispose();
        upright.Dispose();
    }

    /// <summary>Admission and aging stay bounded without disposing fonts still held by a layout or command.</summary>
    [Fact]
    public void BoundedAdmissionAndAgingKeepRetainedFontsDrawable()
    {
        var source = Font.FromFile(FontRegistryTests.FontPath());
        var face = new FontKey.Face(source);
        var cache = new FontCache();
        var retained = cache.Get(face, 14);
        for (var i = 1; i < FontCache.MaxEntries; i++)
        {
            cache.Get(face, 20 + i);
        }

        var overflow = cache.Get(face, 2000);
        Assert.Same(overflow, cache.Get(face, 2000));
        Assert.Equal(FontCache.MaxEntries + 1, cache.Count);
        Assert.Equal(FontCache.MaxEntries + 1, cache.Builds);
        Assert.Same(retained, cache.Get(face, 14));
        for (var i = 0; i < FontCache.MaxEntries * 2; i++)
        {
            cache.Get(face, 3000 + i);
            Assert.InRange(cache.Count, 1, FontCache.MaxEntries * 2);
        }

        Assert.NotSame(retained, cache.Get(face, 14));
        using var surface = SKSurface.Create(new SKImageInfo(100, 40));
        using var paint = new SKPaint { Color = SKColors.White };
        source.Dispose();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        surface.Canvas.DrawText("retained", 0, 20, SKTextAlign.Left, retained, paint);
        Assert.True(retained.MeasureText("retained") > 0);
        Assert.NotEqual(IntPtr.Zero, overflow.Handle);
    }

    /// <summary>Display scaling and theme font replacement choose fonts at the effective size and face.</summary>
    [Fact]
    public void DpiAndThemeChangesRefreshFontAccessWithoutChangingRetainedLayouts()
    {
        using var harness = new FrameHarness();
        var source = Font.FromFile(FontRegistryTests.FontPath());
        var replacement = Font.FromFile(FontRegistryTests.FontPath("icons.ttf"));
        var display = Substitute.For<IDisplayCapability>();
        display.ScaleFactor.Returns(1f);
        harness.Gui.Platform.Register(display);
        harness.Gui.ConfigureFonts(source);
        TextLayout? retained = null;
        harness.Frame(gui => retained = gui.LayoutText("Hello", size: 14));
        var native = retained!.Runs[0].Font.SkFont;
        display.ScaleFactor.Returns(2f);
        harness.Frame(gui =>
        {
            Assert.Equal(28, gui.GetTextFont(14).Size);
            Assert.NotSame(native, gui.GetTextFont(14).SkFont);
            Assert.Equal(14, retained.Runs[0].Font.Size);
        });
        harness.Gui.ConfigureFonts(replacement);
        harness.Frame(gui => Assert.Same(replacement.SkFont.Typeface, gui.GetTextFont(14).SkFont.Typeface));
        source.Dispose();
        replacement.Dispose();
    }

    /// <summary>Cached and uncached native fonts produce identical layout, caret geometry and raster pixels.</summary>
    [Theory]
    [InlineData(TextWrapMode.None, 1f, TextAlignment.Start)]
    [InlineData(TextWrapMode.Word, 1f, TextAlignment.Center)]
    [InlineData(TextWrapMode.Character, 2f, TextAlignment.End)]
    [InlineData(TextWrapMode.WordThenCharacter, 1.5f, TextAlignment.Justify)]
    public void CachedAndUncachedLayoutsAndPixelsMatch(TextWrapMode mode, float scale, TextAlignment alignment)
    {
        var source = Font.FromFile(FontRegistryTests.FontPath());
        var icons = Font.FromFile(FontRegistryTests.FontPath("widget-icons.ttf"));
        var emoji = Font.FromFile(FontRegistryTests.FontPath("icons.ttf"));
        source.Fallbacks = [icons];
        source.SkFont.ScaleX = 1.2f;
        source.SkFont.SkewX = -0.15f;
        source.SkFont.Embolden = true;
        var text = "Hello e\u0301 \uf007 🙂\ufe0f\tworld\nA long line with several words to wrap or truncate.";
        var styled = new RichText(text).Style(0, 5, new TextSpanStyle { Weight = 700, Italic = true, Size = 18 });
        var request = new TextLayoutRequest(styled.Content, new FontKey(source, 14 * scale), new FontKey(emoji, 14 * scale),
            new FontKey(icons, 14 * scale), scale, 180, new TextLayoutOptions
            {
                WrapMode = mode,
                MaxLines = 2,
                TabStops = [40, 80],
            }, alignment, 0, BasicTextShaper.Instance);
        var expected = TextLayoutEngine.Create(request, null);
        var cache = new FontCache();
        var actual = TextLayoutEngine.Create(request, null, cache);
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        Assert.Equal(expected.Lines, actual.Lines);
        Assert.Equal(expected.Runs.Count, actual.Runs.Count);
        for (var i = 0; i < actual.Runs.Count; i++)
        {
            var before = expected.Runs[i];
            var after = actual.Runs[i];
            Assert.Equal((before.Start, before.End, before.Text, before.X, before.Width, before.Baseline),
                (after.Start, after.End, after.Text, after.X, after.Width, after.Baseline));
            Assert.Equal(before.Glyphs?.Ids, after.Glyphs?.Ids);
            Assert.Equal(before.Glyphs?.X, after.Glyphs?.X);
        }

        for (var i = 0; i <= text.Length; i++)
        {
            Assert.Equal(expected.CaretBounds(i), actual.CaretBounds(i));
        }

        using var beforeFrame = new FrameHarness(400, 150);
        using var afterFrame = new FrameHarness(400, 150);
        beforeFrame.Frame(gui => gui.DrawTextLayout(expected, new Vector2(10, 10)));
        var face = new FontKey.Face(source);
        for (var i = 0; i < FontCache.MaxEntries * 2; i++)
        {
            cache.Get(face, 100 + i);
        }
        afterFrame.Frame(gui => gui.DrawTextLayout(actual, new Vector2(10, 10)));
        using var beforeImage = beforeFrame.Snapshot();
        using var afterImage = afterFrame.Snapshot();
        using var beforePixels = SKBitmap.FromImage(beforeImage);
        using var afterPixels = SKBitmap.FromImage(afterImage);
        Assert.Equal(beforePixels.Pixels, afterPixels.Pixels);
        Assert.Contains(beforePixels.Pixels, pixel => pixel.Alpha > 0);
        source.Dispose();
        icons.Dispose();
        emoji.Dispose();
    }
}
