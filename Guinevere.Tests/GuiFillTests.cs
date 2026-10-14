using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>Exercises HUD compositing and raster clipping through both GUI passes.</summary>
public class GuiFillTests
{
    static SKBitmap Render(Action<Gui> draw)
    {
        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        surface.Canvas.Clear(SKColors.Transparent);
        var gui = new Gui { Input = new ScriptedInputHandler() };
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        draw(gui);
        Assert.Equal(0, gui.RootNode!.DrawList.Count);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        draw(gui);
        gui.Render();
        gui.EndFrame();
        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    /// <summary>Values clamp without gameplay state.</summary>
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(float.NaN, 0)]
    [InlineData(float.PositiveInfinity, 0)]
    public void ValuesClampWithoutGameplayState(float value, float expected) =>
        Assert.Equal(expected, new FillOptions().Normalize(value));

    /// <summary>Ranges can be reversed repeated or degenerate.</summary>
    [Fact]
    public void RangesCanBeReversedRepeatedOrDegenerate()
    {
        var range = new FillOptions { Minimum = 20, Maximum = 10 };
        Assert.Equal(0.5f, range.Normalize(15));
        Assert.Equal(0, (range with { Maximum = 20 }).Normalize(15));
        var repeat = new FillOptions { Overflow = FillOverflow.Repeat };
        Assert.Equal(0.25f, repeat.Normalize(1.25f));
        Assert.Equal(0.75f, repeat.Normalize(-0.25f));
        Assert.Equal(1, repeat.Normalize(1));
    }

    /// <summary>Fills grow from each edge.</summary>
    [Theory]
    [InlineData(FillOrigin.Left, 20, 50, 80, 50)]
    [InlineData(FillOrigin.Right, 80, 50, 20, 50)]
    [InlineData(FillOrigin.Top, 50, 20, 50, 80)]
    [InlineData(FillOrigin.Bottom, 50, 80, 50, 20)]
    public void FillsGrowFromEachEdge(FillOrigin origin, int x, int y, int emptyX, int emptyY)
    {
        foreach (var kind in new[] { FillKind.Linear, FillKind.Segmented })
        {
            using var pixels = Render(gui => gui.DrawFill(new Rect(10, 10, 80, 80), 0.5f,
                new FillOptions { Kind = kind, Origin = origin, Segments = 4, Gap = 4, Color = Color.Red }));
            Assert.Equal(SKColors.Red, pixels.GetPixel(x, y));
            Assert.Equal(0, pixels.GetPixel(emptyX, emptyY).Alpha);
        }
    }

    /// <summary>Segments keep gaps and partial cells.</summary>
    [Fact]
    public void SegmentsKeepGapsAndPartialCells()
    {
        using var pixels = Render(gui => gui.DrawFill(new Rect(10, 10, 80, 20), 0.375f,
            new FillOptions { Kind = FillKind.Segmented, Segments = 4, Gap = 4 }));
        Assert.Equal(255, pixels.GetPixel(15, 20).Alpha);
        Assert.Equal(0, pixels.GetPixel(28, 20).Alpha);
        Assert.Equal(255, pixels.GetPixel(34, 20).Alpha);
        Assert.Equal(0, pixels.GetPixel(42, 20).Alpha);
    }

    /// <summary>Radial direction selects quadrant.</summary>
    [Theory]
    [InlineData(true, 70, 30, 30, 30)]
    [InlineData(false, 30, 30, 70, 30)]
    public void RadialDirectionSelectsQuadrant(bool clockwise, int x, int y, int emptyX, int emptyY)
    {
        using var pixels = Render(gui => gui.DrawFill(new Rect(10, 10, 80, 80), 0.25f,
            new FillOptions { Kind = FillKind.Radial, Clockwise = clockwise }));
        Assert.Equal(255, pixels.GetPixel(x, y).Alpha);
        Assert.Equal(0, pixels.GetPixel(emptyX, emptyY).Alpha);
        Assert.Equal(0, pixels.GetPixel(70, 70).Alpha);
    }

    /// <summary>Radial ring clips hole at every edge.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(0.25f)]
    [InlineData(1)]
    public void RadialRingClipsHoleAtEveryEdge(float value)
    {
        using var pixels = Render(gui => gui.DrawFill(new Rect(10, 10, 80, 80), value,
            new FillOptions { Kind = FillKind.Radial, InnerRadius = 0.5f }));
        Assert.Equal(0, pixels.GetPixel(50, 50).Alpha);
        Assert.Equal(value == 0 ? 0 : 255, pixels.GetPixel(72, 28).Alpha);
        Assert.Equal(value == 1 ? 255 : 0, pixels.GetPixel(28, 72).Alpha);
        Assert.Equal(0, pixels.GetPixel(11, 11).Alpha);
    }

    /// <summary>Partial sweep and rotated origin respect range.</summary>
    [Fact]
    public void PartialSweepAndRotatedOriginRespectRange()
    {
        using var pixels = Render(gui => gui.DrawFill(new Rect(10, 10, 80, 80), 1,
            new FillOptions { Kind = FillKind.Radial, StartAngle = 0, SweepAngle = 90 }));
        Assert.Equal(255, pixels.GetPixel(70, 70).Alpha);
        Assert.Equal(0, pixels.GetPixel(70, 30).Alpha);
    }

    /// <summary>Alpha masks multiply coverage and restore clip.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(0.5f)]
    [InlineData(1)]
    public void AlphaMasksMultiplyCoverageAndRestoreClip(float value)
    {
        using var maskSurface = SKSurface.Create(new SKImageInfo(80, 80));
        maskSurface.Canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { Color = new SKColor(255, 0, 0, 128) };
        maskSurface.Canvas.DrawCircle(40, 40, 30, paint);
        using var mask = maskSurface.Snapshot();
        using var pixels = Render(gui =>
        {
            gui.DrawFill(new Rect(10, 10, 80, 80), value, new FillOptions { Mask = mask, Color = Color.Blue });
            gui.DrawRect(new Rect(1, 1, 4, 4), Color.Red);
        });
        Assert.Equal(value == 0 ? 0 : 128, pixels.GetPixel(30, 50).Alpha);
        Assert.Equal(value == 1 ? 128 : 0, pixels.GetPixel(70, 50).Alpha);
        Assert.Equal(0, pixels.GetPixel(11, 11).Alpha);
        Assert.Equal(SKColors.Red, pixels.GetPixel(2, 2));
    }

    /// <summary>Sprite atlas tint and silhouette compose.</summary>
    [Fact]
    public void SpriteAtlasTintAndSilhouetteCompose()
    {
        using var spriteSurface = SKSurface.Create(new SKImageInfo(20, 10));
        spriteSurface.Canvas.Clear(SKColors.Red);
        using var paint = new SKPaint { Color = SKColors.White };
        spriteSurface.Canvas.DrawRect(10, 0, 10, 10, paint);
        using var sprite = spriteSurface.Snapshot();
        using var builder = new SKPathBuilder();
        builder.AddCircle(50, 50, 30);
        using var shape = builder.Detach();
        using var pixels = Render(gui => gui.DrawFill(new Rect(10, 10, 80, 80), 0.5f, new FillOptions
        {
            Sprite = sprite,
            Source = new Rect(10, 0, 10, 10),
            Shape = shape,
            Color = Color.Blue
        }));
        Assert.Equal(SKColors.Blue, pixels.GetPixel(30, 50));
        Assert.Equal(0, pixels.GetPixel(70, 50).Alpha);
        Assert.Equal(0, pixels.GetPixel(11, 11).Alpha);
    }

    /// <summary>Thresholds choose smallest matching bound.</summary>
    [Fact]
    public void ThresholdsChooseSmallestMatchingBound()
    {
        var options = new FillOptions
        {
            Color = Color.Blue,
            Thresholds = [new(0.6f, Color.Yellow), new(0.25f, Color.Red)]
        };
        foreach (var (value, expected) in new[] { (0.25f, SKColors.Red), (0.5f, SKColors.Yellow), (1f, SKColors.Blue) })
        {
            using var pixels = Render(gui => gui.DrawFill(new Rect(10, 10, 80, 20), value, options));
            Assert.Equal(expected, pixels.GetPixel(15, 15));
        }
    }

    /// <summary>Health damage and shield are ordered layers.</summary>
    [Fact]
    public void HealthDamageAndShieldAreOrderedLayers()
    {
        using var pixels = Render(gui =>
        {
            var rect = new Rect(10, 10, 80, 20);
            gui.DrawFill(rect, 0.5f, new FillLayers
            {
                Fill = new FillOptions { Color = Color.Lime },
                Ghost = new FillOptions { Color = Color.Red }
            }, 0.75f);
            gui.DrawFill(rect, 0.25f, new FillOptions { Color = Color.Blue });
        });
        Assert.Equal(SKColors.Blue, pixels.GetPixel(15, 15));
        Assert.Equal(SKColors.Lime, pixels.GetPixel(40, 15));
        Assert.Equal(SKColors.Red, pixels.GetPixel(60, 15));
        Assert.Equal(0, pixels.GetPixel(80, 15).Alpha);
    }

    /// <summary>Presenters accept minimal and custom skins.</summary>
    [Fact]
    public void PresentersAcceptMinimalAndCustomSkins()
    {
        using var pixels = Render(gui =>
        {
            gui.DrawFill(new Rect(10, 10, 80, 10), 1, new FillLayers());
            gui.DrawFill(new Rect(10, 30, 20, 20), 1,
                new FillOptions { Kind = FillKind.Radial, Color = new Color(0x000000b0) });
            gui.DrawFill(new Rect(40, 30, 20, 20), 0);
            gui.DrawFill(new Rect(10, 70, 80, 10), 0.5f);
        });
        Assert.Equal(255, pixels.GetPixel(15, 15).Alpha);
        Assert.Equal(176, pixels.GetPixel(20, 40).Alpha);
        Assert.Equal(0, pixels.GetPixel(50, 40).Alpha);
        Assert.Equal(255, pixels.GetPixel(20, 75).Alpha);
        Assert.Equal(0, pixels.GetPixel(70, 75).Alpha);
    }

    /// <summary>Reticle and direction use host coordinates.</summary>
    [Fact]
    public void ReticleAndDirectionUseHostCoordinates()
    {
        using var pixels = Render(gui =>
        {
            gui.DrawReticle(new Vector2(50, 50), Color.White, dotRadius: 2);
            gui.DrawDirection(new Vector2(50, 50), 0, 35, Color.Red);
            gui.DrawReticle(new Vector2(20, 20), Color.Blue);
        });
        Assert.Equal(SKColors.White, pixels.GetPixel(50, 50));
        Assert.Equal(0, pixels.GetPixel(52, 52).Alpha);
        Assert.Equal(SKColors.Red, pixels.GetPixel(80, 50));
        Assert.Equal(SKColors.Blue, pixels.GetPixel(28, 20));
    }

    /// <summary>Smoothing is opt in and sampled once per frame.</summary>
    [Fact]
    public void SmoothingIsOptInAndSampledOncePerFrame()
    {
        using var harness = new FrameHarness();
        var target = 0f;
        var samples = new List<float>();
        void Draw(Gui gui)
        {
            Assert.Equal(target, gui.AnimatedRange(target, id: "immediate").Value);
            samples.Add(gui.AnimatedRange(target, duration: 1f, id: "health").Value);
        }
        harness.Frame(Draw);
        target = 1;
        harness.Frame(Draw);
        harness.Frame(Draw, 0.2);
        Assert.Equal(samples[^1], samples[^2]);
        Assert.InRange(samples[^1], 0.001f, 0.999f);
        harness.Gui.ReduceMotion = true;
        target = 0.5f;
        harness.Frame(Draw);
        Assert.Equal(0.5f, samples[^1]);
    }

    /// <summary>Empty bounds zero sweep and full disk are safe.</summary>
    [Fact]
    public void EmptyBoundsZeroSweepAndFullDiskAreSafe()
    {
        using var pixels = Render(gui =>
        {
            gui.DrawFill(new Rect(0, 0, 0, 10), 1);
            gui.DrawFill(new Rect(0, 0, 10, 0), 1);
            gui.DrawFill(new Rect(10, 10, 80, 80), 1,
                new FillOptions { Kind = FillKind.Radial, SweepAngle = 0 });
            gui.DrawFill(new Rect(20, 20, 60, 60), 1, new FillOptions { Kind = FillKind.Radial });
        });
        Assert.Equal(255, pixels.GetPixel(50, 50).Alpha);
        Assert.Equal(0, pixels.GetPixel(15, 50).Alpha);
    }

    /// <summary>HUD entries inherit group opacity and node clipping without affecting siblings.</summary>
    [Fact]
    public void FillsRespectNodeClipOpacityAndTransform()
    {
        using var pixels = Render(gui =>
        {
            using (gui.Node(30, 30).Enter())
            {
                gui.SetOpacity(0.5f);
                gui.ClipContent();
                gui.DrawFill(new Rect(0, 0, 80, 80), 1, new FillOptions { Color = Color.Red });
            }
            using (gui.Node(20, 20).Absolute(60, 60).Enter())
            {
                gui.SetTransform(Matrix3x2.CreateTranslation(10, 0));
                gui.DrawFill(gui.CurrentNode.Rect, 1, new FillOptions { Color = Color.Blue });
            }
        });
        Assert.InRange(pixels.GetPixel(15, 15).Alpha, 127, 128);
        Assert.Equal(0, pixels.GetPixel(40, 15).Alpha);
        Assert.Equal(SKColors.Blue, pixels.GetPixel(80, 70));
        Assert.Equal(0, pixels.GetPixel(65, 70).Alpha);
    }

    /// <summary>The complete tour page builds both passes and produces the expected HUD regions.</summary>
    [Fact]
    public void HudGalleryRendersOffscreen()
    {
        using var harness = new FrameHarness(800, 900);
        harness.Frame(gui => Example01.Shared.Hud(gui, Example01.ColorPalette.Dark));
        using var image = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(image);
        Assert.True(harness.Gui.RootNode!.Children.Single().Children.Count > 10);
        Assert.NotEqual(0, pixels.GetPixel(50, 75).Alpha);
    }

    /// <summary>The example damage, recovery and animation controls exercise held image trails and frame requests.</summary>
    [Fact]
    public void HudGalleryControlsDriveImageLayers()
    {
        using var harness = new FrameHarness(800, 900);
        static void Draw(Gui gui) => Example01.Shared.Hud(gui, Example01.ColorPalette.Dark);
        Vector2 ButtonCenter(int index) => FrameHarness.Center(
            harness.Gui.RootNode!.Children.Single().Children.First().Children[index]);
        SKColor BarPixel()
        {
            var bounds = harness.Gui.RootNode!.Children.Single().Children[2].Rect;
            using var image = harness.Snapshot();
            using var pixels = SKBitmap.FromImage(image);
            return pixels.GetPixel((int)(bounds.X + bounds.W * 0.9f), (int)(bounds.Y + bounds.H / 2));
        }
        harness.Frame(Draw);
        harness.Click(Draw, ButtonCenter(0));
        harness.Frame(Draw, 0.2);
        harness.Frame(Draw, 0.2);
        var ghost = BarPixel();
        Assert.True(ghost.Red > 200 && ghost.Green > 200 && ghost.Blue < 30);
        harness.Click(Draw, ButtonCenter(1));
        harness.Frame(Draw, 0.2);
        harness.Frame(Draw, 0.2);
        var restored = BarPixel();
        Assert.True(restored.Green > 200 && restored.Red < 30);
        harness.Click(Draw, ButtonCenter(2));
        harness.Frame(Draw);
        Assert.Equal(0, harness.Gui.FrameWaitSeconds);
    }

    /// <summary>A full-image sprite and clamped segment options stay inside destination bounds.</summary>
    [Fact]
    public void SpriteAndSegmentDefaultsAreBounded()
    {
        using var surface = SKSurface.Create(new SKImageInfo(10, 10));
        surface.Canvas.Clear(SKColors.Lime);
        using var sprite = surface.Snapshot();
        using var pixels = Render(gui =>
        {
            gui.DrawFill(new Rect(10, 10, 40, 20), 1, new FillOptions { Sprite = sprite });
            gui.DrawFill(new Rect(10, 50, 80, 20), 1,
                new FillOptions { Kind = FillKind.Segmented, Segments = 0, Gap = -2 });
        });
        Assert.Equal(SKColors.Lime, pixels.GetPixel(30, 20));
        Assert.Equal(255, pixels.GetPixel(80, 60).Alpha);
        Assert.Equal(0, pixels.GetPixel(95, 60).Alpha);
    }

    /// <summary>A normalizer receives raw units and can apply a custom nonlinear cooldown mapping.</summary>
    [Fact]
    public void RawValuesAcceptCustomNormalization()
    {
        var options = new FillOptions { Maximum = 100 };
        Assert.Equal(0.25f, options.Normalize(25));
        var curved = options with { Normalizer = static seconds => MathF.Pow(1 - seconds / 4, 2) };
        Assert.Equal(0.25f, curved.Normalize(2));
        Assert.Equal(0, (curved with { Normalizer = static _ => float.NaN }).Normalize(2));
        Assert.Equal(0.25f, (options with
        {
            Normalizer = static raw => raw / 100,
            Overflow = FillOverflow.Repeat
        }).Normalize(125));
        using var pixels = Render(gui => gui.DrawFill(new Rect(10, 10, 80, 20), 2, curved));
        Assert.Equal(255, pixels.GetPixel(20, 20).Alpha);
        Assert.Equal(0, pixels.GetPixel(40, 20).Alpha);
    }

    /// <summary>Background ignores mapping while ghost and foreground receive their own raw values.</summary>
    [Fact]
    public void ExplicitLayersDrawBackgroundGhostAndFillInOrder()
    {
        using var pixels = Render(gui => gui.DrawFill(new Rect(10, 10, 80, 20), 50, new FillLayers
        {
            Background = new FillOptions { Color = Color.Blue, Normalizer = static _ => 0 },
            Ghost = new FillOptions { Maximum = 100, Color = Color.Red },
            Fill = new FillOptions { Maximum = 100, Color = Color.Lime }
        }, 75));
        Assert.Equal(SKColors.Lime, pixels.GetPixel(40, 20));
        Assert.Equal(SKColors.Red, pixels.GetPixel(60, 20));
        Assert.Equal(SKColors.Blue, pixels.GetPixel(80, 20));
    }

    /// <summary>Omitting a ghost value draws only background and foreground even when a ghost skin exists.</summary>
    [Fact]
    public void GhostLayerIsOptional()
    {
        using var pixels = Render(gui => gui.DrawFill(new Rect(10, 10, 80, 20), 0.5f, new FillLayers
        {
            Background = new FillOptions { Color = Color.Blue },
            Ghost = new FillOptions { Color = Color.Red }
        }));
        Assert.Equal(SKColors.White, pixels.GetPixel(30, 20));
        Assert.Equal(SKColors.Blue, pixels.GetPixel(70, 20));
    }

    /// <summary>Nine-slice skins preserve atlas corners and reveal a fixed stretched center at partial values.</summary>
    [Theory]
    [InlineData(0.5f)]
    [InlineData(1)]
    public void NineSliceAtlasSkinsPreserveCorners(float value)
    {
        using var surface = SKSurface.Create(new SKImageInfo(24, 12));
        surface.Canvas.Clear(SKColors.Red);
        using var paint = new SKPaint { Color = SKColors.White };
        surface.Canvas.DrawRect(4, 2, 12, 8, paint);
        paint.Color = SKColors.Lime;
        surface.Canvas.DrawRect(6, 4, 8, 4, paint);
        paint.Color = SKColors.Blue;
        surface.Canvas.DrawRect(4, 2, 2, 2, paint);
        paint.Color = SKColors.Yellow;
        surface.Canvas.DrawRect(14, 8, 2, 2, paint);
        using var sprite = surface.Snapshot();
        using var pixels = Render(gui => gui.DrawFill(new Rect(10, 10, 80, 40), value, new FillOptions
        {
            Sprite = sprite,
            Source = new Rect(4, 2, 12, 8),
            SpriteBorder = new Insets(2)
        }));
        Assert.Equal(SKColors.Blue, pixels.GetPixel(10, 10));
        Assert.Equal(SKColors.Lime, pixels.GetPixel(30, 25));
        if (value == 1)
        {
            Assert.Equal(SKColors.Yellow, pixels.GetPixel(89, 49));
        }
        else
        {
            Assert.Equal(0, pixels.GetPixel(89, 49).Alpha);
        }
    }

    /// <summary>A nine-slice skin with no center remains valid at tiny destination sizes.</summary>
    [Fact]
    public void NineSliceDegenerateCentersAndTinyDestinationsAreSafe()
    {
        using var surface = SKSurface.Create(new SKImageInfo(8, 8));
        surface.Canvas.Clear(SKColors.Blue);
        using var sprite = surface.Snapshot();
        using var pixels = Render(gui =>
        {
            var options = new FillOptions { Sprite = sprite, SpriteBorder = new Insets(4) };
            gui.DrawFill(new Rect(10, 10, 1, 1), 1, options);
            gui.DrawFill(new Rect(20, 20, 40, 40), 1, options);
        });
        Assert.Equal(SKColors.Blue, pixels.GetPixel(10, 10));
        Assert.Equal(0, pixels.GetPixel(40, 40).Alpha);
    }
}
