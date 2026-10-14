using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Styling;

/// <summary>Checks stylesheet slice parsing, painting and diagnostics.</summary>
public sealed class StyledNineSliceTests
{
    /// <summary>The full Styling panel builds its new sliced tile and loads each declared bitmap.</summary>
    [Fact]
    public void GalleryStylingPanelRenders()
    {
        using var harness = new FrameHarness(900, 600);
        harness.Gui.StyleSheets.Add(Controls01.Program.StylingSheet);
        var draw = typeof(Controls01.Program).GetMethod("StylingContent",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .CreateDelegate<Action<Gui>>();
        harness.Frame(draw);
        Assert.Empty(harness.Gui.BackgroundDiagnostics);
        harness.Gui.ClearBackgroundImages();
    }

    /// <summary>The gallery skin preserves its thin border around a stretched solid center.</summary>
    [Fact]
    public void GalleryStylesheetLoadsSliceSkin()
    {
        using var harness = new FrameHarness(120, 64);
        harness.Gui.StyleSheets.Add(Controls01.Program.StylingSheet);
        harness.Frame(g => { using (g.StyledNode("VisualElement", ["tile"], "sliced").Enter()) { } });
        using var snapshot = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(snapshot);
        Assert.NotEqual(pixels.GetPixel(2, 32), pixels.GetPixel(60, 32));
        Assert.Empty(harness.Gui.BackgroundDiagnostics);
        harness.Gui.ClearBackgroundImages();
    }

    /// <summary>Slice values expand in top, right, bottom, left order and accept pixels or none.</summary>
    [Theory]
    [InlineData("2", 2, 2, 2, 2)]
    [InlineData("2px 3px", 3, 2, 3, 2)]
    [InlineData("2 3 4", 3, 2, 3, 4)]
    [InlineData("2 3 4 5", 5, 2, 3, 4)]
    public void ExpandsSliceValues(string value, float left, float top, float right, float bottom) =>
        Assert.Equal(new Insets(left, top, right, bottom), StyleBitmapBackground.Slice(value));

    /// <summary>Omitted or explicitly disabled slicing retains normal bitmap rendering.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("NONE")]
    public void UnslicedValues(string? value) => Assert.Null(StyleBitmapBackground.Slice(value));

    static byte[] Texture()
    {
        using var bitmap = new SKBitmap(12, 12);
        bitmap.Erase(SKColors.Green);
        for (var y = 4; y < 8; y++)
        {
            for (var x = 4; x < 8; x++)
            {
                bitmap.SetPixel(x, y, SKColors.Blue);
            }
        }

        bitmap.SetPixel(0, 0, SKColors.Transparent);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Styled slices match the core renderer, including tiny targets, fill, clips, shadows and opacity.</summary>
    [Theory]
    [InlineData(40, 30, false)]
    [InlineData(5, 3, false)]
    [InlineData(0, 0, false)]
    [InlineData(40, 30, true)]
    public void StyledSlicesMatchDrawing(float width, float height, bool effects)
    {
        using var harness = new FrameHarness(120, 60);
        var png = Texture();
        using var image = SKImage.FromEncodedData(png);
        var extra = effects ? "border-radius = 6; opacity = 0.5; box-shadow = inset 0 2px 0 #000000;" : "";
        harness.Gui.StyleSheets.Add(StyleSheet.Parse($$"""
            box { width = {{width}}; height = {{height}}; background-color = #ff0000;
                background-image = url(panel.png); background-slice = 4; {{extra}} }
            """, new StyleSheetOptions { ResourceResolver = _ => new MemoryStream(png) }));
        harness.Frame(g =>
        {
            g.DrawBackgroundRect(Color.White);
            using (g.Node().Direction(Axis.Horizontal).Gap(10).Enter())
            {
                using (g.StyledNode("box").Enter()) { }
                using (g.Node(width, height).Enter())
                {
                    if (effects)
                    {
                        g.DrawStyledBox(g.ResolveStyle("box"), g.CurrentNode.Rect);
                    }
                    else
                    {
                        g.DrawBackgroundRect(Color.Red);
                        g.DrawImageNineSlice(image, g.CurrentNode.Rect, new Insets(4));
                    }
                }
            }
        });
        using var snapshot = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(snapshot);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var first = pixels.GetPixel(x, y);
                var second = pixels.GetPixel(x + (int)width + 10, y);
                // Picture shaders resample their cached tile, so boundary colors can differ from direct image draws.
                Assert.True(Math.Abs(first.Red - second.Red) <= 16 && Math.Abs(first.Green - second.Green) <= 16
                    && Math.Abs(first.Blue - second.Blue) <= 16, $"({x}, {y}): {first} versus {second}");
            }
        }

        if (effects)
        {
            Assert.Equal(SKColors.White, pixels.GetPixel(0, 0));
        }
        else if (width == 40)
        {
            Assert.Equal(SKColors.Green, pixels.GetPixel(1, 15));
            Assert.Equal(SKColors.Blue, pixels.GetPixel(20, 15));
            Assert.Equal(SKColors.Red, pixels.GetPixel(0, 0));
        }
        Assert.Empty(harness.Gui.BackgroundDiagnostics);
        harness.Gui.ClearBackgroundImages();
    }

    /// <summary>Unsupported slice syntax reports the winning declaration location once and retains the solid fill.</summary>
    [Theory]
    [InlineData("-1")]
    [InlineData("20%")]
    [InlineData("auto")]
    [InlineData("NaN")]
    [InlineData("1 2 3 4 5")]
    [InlineData("1 -2")]
    public void InvalidSliceDiagnostics(string value)
    {
        using var harness = new FrameHarness(20, 20);
        harness.Gui.StyleSheets.Add(StyleSheet.Parse(
            "box { width = 20; height = 20; background-color = #ff0000; background-image = url(panel.png);\n"
            + "background-slice = " + value + "; }", new StyleSheetOptions
            {
                SourceName = "slice.pss",
                ResourceResolver = _ => new MemoryStream(Texture()),
            }));
        static void Draw(Gui g) { using (g.StyledNode("box").Enter()) { } }
        harness.Frame(Draw);
        harness.Frame(Draw);
        var diagnostic = Assert.Single(harness.Gui.BackgroundDiagnostics);
        Assert.Equal("slice.pss", diagnostic.Source);
        Assert.Equal(2, diagnostic.Line);
        using var snapshot = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(snapshot);
        Assert.Equal(SKColors.Red, pixels.GetPixel(10, 10));
        harness.Gui.ClearBackgroundImages();
    }
}
