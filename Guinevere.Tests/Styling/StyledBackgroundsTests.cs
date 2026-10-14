using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Styling;

/// <summary>Checks bitmap theme rendering, resource lifetimes and located failures.</summary>
public sealed class StyledBackgroundsTests
{
    static byte[] Image(SKColor color)
    {
        using var bitmap = new SKBitmap(4, 2);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    static void Draw(Gui gui)
    {
        gui.DrawBackgroundRect(Color.White);
        using (gui.StyledNode("box").Enter()) { }
    }

    static SKColor Pixel(FrameHarness harness, int x, int y)
    {
        using var snapshot = harness.Snapshot();
        using var bitmap = SKBitmap.FromImage(snapshot);
        return bitmap.GetPixel(x, y);
    }

    /// <summary>Logical image dimensions, aspect ratio, repeat axes and percentage offsets determine coverage.</summary>
    [Theory]
    [InlineData("repeat", "auto", "0% 0%", 19, 19, true)]
    [InlineData("repeat-x", "auto", "0 0", 19, 3, false)]
    [InlineData("repeat-y", "auto", "0 0", 3, 19, true)]
    [InlineData("no-repeat", "auto", "right bottom", 18, 19, true)]
    [InlineData("no-repeat", "auto", "bottom right", 1, 1, false)]
    [InlineData("no-repeat", "contain", "center", 10, 1, false)]
    [InlineData("no-repeat", "contain", "center", 10, 10, true)]
    [InlineData("no-repeat", "cover", "center", 1, 1, true)]
    [InlineData("no-repeat", "50% auto", "50% 50%", 6, 8, true)]
    [InlineData("no-repeat", "auto 10px", "left top", 19, 9, true)]
    [InlineData("no-repeat", "8px 6px", "2px 3px", 4, 5, true)]
    [InlineData("no-repeat", "auto", "top", 9, 1, true)]
    public void SizePositionAndRepeat(string repeat, string size, string position, int x, int y, bool red)
    {
        using var harness = new FrameHarness(24, 24);
        var png = Image(SKColors.Red);
        harness.Gui.StyleSheets.Add(StyleSheet.Parse($$"""
            box { width = 20; height = 20; background-image = url("tile.png");
                background-repeat = {{repeat}}; background-size = {{size}}; background-position = {{position}}; }
            """, new StyleSheetOptions { ResourceResolver = _ => new MemoryStream(png) }));
        harness.Frame(Draw);
        Assert.Equal(red ? SKColors.Red : SKColors.White, Pixel(harness, x, y));
        Assert.Equal(SKColors.White, Pixel(harness, 22, 22));
        Assert.Empty(harness.Gui.BackgroundDiagnostics);
        harness.Gui.ClearBackgroundImages();
    }

    /// <summary>Both entry points preserve rounded clipping, borders, inset shadows and whole-box opacity.</summary>
    [Fact]
    public void BoxAndPartMatchWithEffects()
    {
        using var harness = new FrameHarness(60, 30);
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("""
            box { width = 20; height = 20; background-image = url("tile.png");
                border-radius = 6; border-width = 2; border-color = #0000ff; opacity = 0.5;
                box-shadow = inset 0 2px 0 #000000; }
            """, new StyleSheetOptions { ResourceResolver = _ => new MemoryStream(Image(SKColors.Red)) }));
        harness.Frame(g =>
        {
            g.DrawBackgroundRect(Color.White);
            using (g.Node().Direction(Axis.Horizontal).Gap(10).Enter())
            {
                using (g.StyledNode("box").Enter()) { }
                using (g.Node(20, 20).Enter())
                {
                    g.DrawStyledBox(g.ResolveStyle("box"), g.CurrentNode.Rect);
                }
            }
        });
        for (var y = 0; y < 20; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                var node = Pixel(harness, x, y);
                var part = Pixel(harness, x + 30, y);
                Assert.InRange(Math.Abs(node.Red - part.Red), 0, 4);
                Assert.InRange(Math.Abs(node.Green - part.Green), 0, 4);
                Assert.InRange(Math.Abs(node.Blue - part.Blue), 0, 4);
                Assert.Equal(node.Alpha, part.Alpha);
            }
        }

        Assert.Equal(SKColors.White, Pixel(harness, 0, 0));
        Assert.InRange(Pixel(harness, 10, 10).Green, (byte)120, (byte)140);
        harness.Gui.ClearBackgroundImages();
    }

    /// <summary>URI bases survive imports; sheets and tokens invalidate both successful and failed decodes.</summary>
    [Fact]
    public void CacheAndImportedResources()
    {
        using var harness = new FrameHarness(24, 24);
        var loads = 0;
        Uri? loaded = null;
        var png = Image(SKColors.Red);
        var options = new StyleSheetOptions
        {
            ImportResolver = _ => new StyleSheetText("box { width = 20; height = 20; background = url(tile.png); }",
                "child.pss", new Uri("resource:///child/theme.pss")),
            ResourceResolver = uri => { loads++; loaded = uri; return new MemoryStream(png); },
        };
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("@import \"child\";", options));
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Equal(1, loads);
        Assert.Equal(new Uri("resource:///child/tile.png"), loaded);
        Assert.Equal(SKColors.Red, Pixel(harness, 10, 10));
        png = Image(SKColors.Blue);
        harness.Gui.StyleSheets.SetToken("reload", "1");
        harness.Frame(Draw);
        Assert.Equal(2, loads);
        Assert.Equal(SKColors.Blue, Pixel(harness, 10, 10));
        harness.Gui.StyleSheets[0] = StyleSheet.Parse("box { width = 20; height = 20; background-image = none; }");
        harness.Frame(Draw);
        Assert.Equal(SKColors.White, Pixel(harness, 10, 10));
        harness.Gui.ClearBackgroundImages();
    }

    /// <summary>Embedded demonstration textures load through the sample stylesheet's resource resolver.</summary>
    [Theory]
    [InlineData("win95")]
    [InlineData("luna")]
    [InlineData("aqua")]
    [InlineData("crt")]
    public void SampleAssetsLoad(string id)
    {
        using var harness = new FrameHarness(120, 64);
        harness.Gui.StyleSheets.Add(Controls01.Program.StylingSheet);
        harness.Frame(g => { using (g.StyledNode("VisualElement", ["tile"], id).Enter()) { } });
        Assert.Empty(harness.Gui.BackgroundDiagnostics);
        Assert.NotEqual(SKColors.Transparent, Pixel(harness, 60, 32));
        Assert.NotEqual(Pixel(harness, 60, 32), Pixel(harness, 60, 35));
        harness.Gui.ClearBackgroundImages();
    }

    /// <summary>Bitmap pixels composite above the solid fill on both styled nodes and drawn parts.</summary>
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 128)]
    [InlineData(false, 255)]
    [InlineData(true, 0)]
    [InlineData(true, 128)]
    [InlineData(true, 255)]
    public void BitmapCompositesAboveSolidFill(bool part, byte alpha)
    {
        using var harness = new FrameHarness(24, 24);
        var png = Image(SKColors.Red.WithAlpha(alpha));
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("""
            box { width = 20; height = 20; background-color = #0000ff; background-image = url(tile.png); }
            """, new StyleSheetOptions { ResourceResolver = _ => new MemoryStream(png) }));
        harness.Frame(g =>
        {
            if (part)
            {
                using (g.Node(20, 20).Enter())
                {
                    g.DrawStyledBox(g.ResolveStyle("box"), g.CurrentNode.Rect);
                }
            }
            else
            {
                using (g.StyledNode("box").Enter()) { }
            }
        });
        Assert.Equal(new SKColor(alpha, 0, (byte)(255 - alpha)), Pixel(harness, 10, 10));
        harness.Gui.ClearBackgroundImages();
    }

    /// <summary>Asset streams close after decoding; canvas scaling and ancestor clips affect bitmap tiles.</summary>
    [Fact]
    public void ScaledBackgroundIsClippedAndStreamDisposed()
    {
        using var harness = new FrameHarness(24, 24);
        var stream = new MemoryStream(Image(SKColors.Red));
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("""
            box { width = 10; height = 10; background-image = url(tile.png);
                background-repeat = no-repeat; scale = 2; transform-origin = 0 0; }
            """, new StyleSheetOptions { ResourceResolver = _ => stream }));
        harness.Frame(g =>
        {
            g.DrawBackgroundRect(Color.White);
            using (g.Node(6, 6).Enter())
            {
                g.ClipContent();
                using (g.StyledNode("box").Enter()) { }
            }
        });
        Assert.False(stream.CanRead);
        Assert.Equal(SKColors.Red, Pixel(harness, 5, 2));
        Assert.Equal(SKColors.White, Pixel(harness, 5, 5));
        Assert.Equal(SKColors.White, Pixel(harness, 7, 2));
        harness.Gui.ClearBackgroundImages();
    }

    /// <summary>Missing and malformed resources are reported once, with the winning declaration's position.</summary>
    [Theory]
    [InlineData("background-image = url(missing.png)")]
    [InlineData("background-image = url(a.png), url(b.png)")]
    [InlineData("background-image = url(a.png); background-repeat = space")]
    [InlineData("background-image = url(a.png); background-size = 0")]
    [InlineData("background-image = url(a.png); background-size = 1 2 3")]
    [InlineData("background-image = url(a.png); background-position = left left")]
    [InlineData("background-image = url(a.png); background-position = 1 2 3")]
    public void LocatedDiagnostics(string declaration)
    {
        using var harness = new FrameHarness(24, 24);
        var loads = 0;
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("box { width = 20; height = 20;\n    " + declaration + "; }",
            new StyleSheetOptions
            {
                SourceName = "theme.pss",
                ResourceResolver = _ => { loads++; return null; },
            }));
        harness.Frame(Draw);
        harness.Frame(Draw);
        var diagnostic = Assert.Single(harness.Gui.BackgroundDiagnostics);
        Assert.Equal("theme.pss", diagnostic.Source);
        Assert.Equal(2, diagnostic.Line);
        Assert.True(diagnostic.Column >= 5);
        Assert.InRange(loads, 0, 1);
    }

    /// <summary>Transparent pixels reveal the solid background, and local file assets decode without a resolver.</summary>
    [Fact]
    public void FileSourceAndTransparency()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            File.WriteAllBytes(Path.Combine(directory.FullName, "tile.png"), Image(SKColors.Transparent));
            var file = Path.Combine(directory.FullName, "theme.pss");
            File.WriteAllText(file, "box { width = 20; height = 20; bg-color = #0000ff; background-image = url(tile.png); }");
            using var harness = new FrameHarness(24, 24);
            harness.Gui.AddStyleSheet(StyleSheetSource.FromFile(file));
            harness.Frame(Draw);
            Assert.Equal(SKColors.Blue, Pixel(harness, 10, 10));
            Assert.Empty(harness.Gui.BackgroundDiagnostics);
            harness.Gui.StyleSheets[0] = StyleSheet.Parse(
                "box { width = 20; height = 20; background-image = url(missing.png); }",
                new StyleSheetOptions { BaseUri = new Uri(file) });
            harness.Frame(Draw);
            Assert.Single(harness.Gui.BackgroundDiagnostics);
            harness.Gui.ClearBackgroundImages();
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
