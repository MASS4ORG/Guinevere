using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>Checks postprocess pixels, nesting, clipping and safe runtime-filter fallback.</summary>
public class RenderEffectTests
{
    sealed class LegacyEffectsCapability : IGpuEffectsCapability
    {
        /// <inheritdoc />
        public bool Supports(string effect) => false;
    }

    /// <summary>Existing hosts opt into new target and runtime-compilation support explicitly.</summary>
    [Fact]
    public void ExistingCapabilitiesDefaultToUnsupportedTargetsAndRuntimeCompilation()
    {
        IGpuEffectsCapability capability = new LegacyEffectsCapability();
        Assert.False(capability.SupportsRenderTargets);
        Assert.False(capability.SupportsRuntimeShaderCompilation);
        Assert.False(capability.Supports("runtime-color-filter"));
    }

    sealed record Unavailable : RenderEffect;

    static SKBitmap Render(RenderEffectStack stack, Action<Gui>? children = null, float opacity = 1)
    {
        using var frame = new FrameHarness(80, 80);
        frame.Frame(gui =>
        {
            using (gui.Node(20, 20).Absolute(20, 20).Effects(stack).Enter())
            {
                gui.SetOpacity(opacity);
                gui.DrawBackgroundRect(Color.Red);
                children?.Invoke(gui);
            }
        });
        using var image = frame.Snapshot();
        return SKBitmap.FromImage(image);
    }

    /// <summary>Color filters preserve alpha and apply to the group after its children.</summary>
    [Theory]
    [InlineData("invert(100%)", 0, 255, 255)]
    [InlineData("grayscale(1)", 54, 54, 54)]
    [InlineData("saturate(0)", 54, 54, 54)]
    [InlineData("brightness(0.5)", 128, 0, 0)]
    [InlineData("contrast(0)", 128, 128, 128)]
    [InlineData("tint(#0000ff,1)", 0, 0, 255)]
    [InlineData("hue-rotate(0deg)", 255, 0, 0)]
    [InlineData("none", 255, 0, 0)]
    public void ColorFiltersProduceExpectedPixels(string source, int r, int g, int b)
    {
        using var stack = RenderEffectParser.Parse(source);
        using var bitmap = Render(stack);
        var color = bitmap.GetPixel(30, 30);
        Assert.InRange(color.Red, Math.Max(0, r - 1), Math.Min(255, r + 1));
        Assert.InRange(color.Green, Math.Max(0, g - 1), Math.Min(255, g + 1));
        Assert.InRange(color.Blue, Math.Max(0, b - 1), Math.Min(255, b + 1));
        Assert.Equal(255, color.Alpha);
    }

    /// <summary>Changing stack order changes the resulting image.</summary>
    [Fact]
    public void StackOrderIsObservable()
    {
        using var first = RenderEffectParser.Parse("brightness(.5) invert(1)");
        using var second = RenderEffectParser.Parse("invert(1) brightness(.5)");
        using var a = Render(first);
        using var b = Render(second);
        Assert.Equal(255, a.GetPixel(30, 30).Green);
        Assert.InRange(b.GetPixel(30, 30).Green, 127, 129);
    }

    /// <summary>Spatial effects reach outside the original node bounds.</summary>
    [Theory]
    [InlineData("blur(3px)")]
    [InlineData("glow(3,#00ff00)")]
    [InlineData("drop-shadow(4,0,3,#0000ff)")]
    public void SpatialFiltersExpandPastTheSource(string source)
    {
        using var stack = RenderEffectParser.Parse(source);
        using var bitmap = Render(stack);
        Assert.True(bitmap.GetPixel(42, 30).Alpha > 0);
        Assert.Equal(0, bitmap.GetPixel(70, 70).Alpha);
    }

    /// <summary>A sharpen filter keeps a flat interior unchanged.</summary>
    [Fact]
    public void SharpenPreservesFlatInteriors()
    {
        using var stack = RenderEffectParser.Parse("sharpen(1)");
        using var bitmap = Render(stack);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(30, 30));
    }

    /// <summary>Chromatic separation retains the source's alpha mask.</summary>
    [Fact]
    public void ChromaticAberrationSeparatesChannels()
    {
        using var stack = RenderEffectParser.Parse("chromatic-aberration(3,0)");
        using var bitmap = Render(stack);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(30, 30));
        Assert.Equal(0, bitmap.GetPixel(21, 30).Red);
        Assert.Equal(255, bitmap.GetPixel(21, 30).Alpha);
    }

    /// <summary>Nearest sampling produces blocks of constant color.</summary>
    [Fact]
    public void PixelationProducesBlocks()
    {
        using var stack = RenderEffectParser.Parse("pixelate(8)");
        using var frame = new FrameHarness(64, 32);
        frame.Frame(gui =>
        {
            using (gui.Node(64, 32).Effects(stack).Enter())
            {
                for (var x = 0; x < 64; x++)
                {
                    gui.DrawRect(new Rect(x, 0, 1, 32), Color.FromArgb(255, x * 4, 0, 0));
                }
            }
        });
        using var image = frame.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        Assert.Equal(bitmap.GetPixel(1, 10), bitmap.GetPixel(6, 10));
        Assert.NotEqual(bitmap.GetPixel(1, 10), bitmap.GetPixel(10, 10));
    }

    /// <summary>Filtered descendants composite once through their parent's opacity.</summary>
    [Fact]
    public void NestedEffectsAndOpacityKeepTheirSaveOrder()
    {
        using var parent = new RenderEffectStack(new RenderEffect.Invert());
        using var child = new RenderEffectStack(new RenderEffect.Brightness(.5f));
        using var bitmap = Render(parent, gui =>
        {
            using (gui.Node(20, 20).Effects(child).Enter())
            {
                gui.DrawBackgroundRect(Color.Red);
            }
        }, .5f);
        var pixel = bitmap.GetPixel(30, 30);
        Assert.InRange(pixel.Alpha, 127, 129);
        Assert.InRange(pixel.Red, 125, 130);
        Assert.Equal(255, pixel.Green);
    }

    /// <summary>An outer opacity layer includes filter outsets and unbounded drawing in its subtree.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OpacityLayerDoesNotCropDescendantGlow(bool unbounded)
    {
        using var glow = new RenderEffectStack(new RenderEffect.Glow(4, Color.Blue));
        using var frame = new FrameHarness(80, 80);
        var entry = Substitute.For<IDrawListEntry>();
        frame.Frame(gui =>
        {
            using (gui.Node(20, 20).Absolute(20, 20).Enter())
            {
                gui.SetOpacity(.5f);
                if (unbounded && gui.Pass == Pass.Pass2Render)
                {
                    gui.CurrentNode.DrawList.Add(entry);
                }
                using (gui.Node(20, 20).Effects(glow).Enter())
                {
                    gui.DrawBackgroundRect(Color.Red);
                }
            }
        });
        using var image = frame.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        Assert.True(bitmap.GetPixel(42, 30).Alpha > 0);
        Assert.InRange(bitmap.GetPixel(30, 30).Alpha, 127, 129);
    }

    /// <summary>Blur output respects ancestor clips and does not affect later siblings.</summary>
    [Fact]
    public void ClipConstrainsTheFilteredOutput()
    {
        using var stack = new RenderEffectStack(new RenderEffect.Blur(4));
        using var frame = new FrameHarness(80, 80);
        frame.Frame(gui =>
        {
            using (gui.Node(20, 20).Absolute(20, 20).Enter())
            {
                gui.ClipContent();
                using (gui.Node(20, 20).Effects(stack).Enter())
                {
                    gui.DrawBackgroundRect(Color.Red);
                }
            }
            using (gui.Node(10, 10).Absolute(60, 60).Enter())
            {
                gui.DrawBackgroundRect(Color.Blue);
            }
        });
        using var image = frame.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        Assert.Equal(0, bitmap.GetPixel(41, 30).Alpha);
        Assert.True(bitmap.GetPixel(30, 30).Alpha > 0);
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(65, 65));
    }

    /// <summary>Runtime failures are observable and preserve the unfiltered source.</summary>
    [Fact]
    public void RuntimeCompilationRequiresCapabilityAndReportsFailures()
    {
        using var stack = new RenderEffectStack(new RenderEffect.RuntimeColorFilter("invalid SkSL"));
        using var bitmap = Render(stack);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(30, 30));
        Assert.Contains("support", Assert.Single(stack.Diagnostics));
        _ = stack.Paint(true, 1);
        Assert.NotEmpty(stack.Diagnostics);
        using var unbound = new RenderEffectStack(new RenderEffect.RuntimeColorFilter(
            "uniform half amount; half4 main(half4 c) { return c * amount; }"));
        _ = unbound.Paint(true, 1);
        Assert.Contains("unbound", Assert.Single(unbound.Diagnostics));
        using var unavailable = new RenderEffectStack(new Unavailable());
        using var source = Render(unavailable);
        Assert.Equal(SKColors.Red, source.GetPixel(30, 30));
        Assert.Contains("Unsupported", Assert.Single(unavailable.Diagnostics));
    }

    /// <summary>Runtime and precompiled filters share the ordered color-filter path.</summary>
    [Fact]
    public void CustomFiltersWorkAndNativePaintIsReused()
    {
        const string source = "half4 main(half4 c) { return half4(c.b, c.r, c.g, c.a); }";
        using var runtime = new RenderEffectStack(new RenderEffect.RuntimeColorFilter(source));
        var paint = runtime.Paint(true, 1);
        Assert.Same(paint, runtime.Paint(true, .5f));
        Assert.Empty(runtime.Diagnostics);
        using var effect = SKRuntimeEffect.CreateColorFilter(source, out _);
        using var color = effect.ToColorFilter();
        using var compiled = new RenderEffectStack(new RenderEffect.CompiledColorFilter(color));
        using var bitmap = Render(compiled);
        Assert.Equal(SKColors.Lime, bitmap.GetPixel(30, 30));
        Assert.True(SkiaEffectsCapability.Instance.SupportsRenderTargets);
        Assert.True(SkiaEffectsCapability.Instance.SupportsRuntimeShaderCompilation);
        Assert.True(SkiaEffectsCapability.Instance.Supports("runtime-color-filter"));
        Assert.False(SkiaEffectsCapability.Instance.Supports("neighbor-shader"));
    }

    /// <summary>Disposed native stacks cannot be prepared again.</summary>
    [Fact]
    public void DisposedStackRejectsReuse()
    {
        var stack = new RenderEffectStack();
        stack.Dispose();
        stack.Dispose();
        Assert.Throws<ObjectDisposedException>(() => stack.Paint(false, 1));
    }

    /// <summary>Effect layers preserve transformed positions without applying the transform twice.</summary>
    [Fact]
    public void EffectLayerKeepsTransformsInScreenSpace()
    {
        using var stack = new RenderEffectStack(new RenderEffect.Invert());
        using var frame = new FrameHarness(80, 80);
        frame.Frame(gui =>
        {
            using (gui.RenderEffects(stack))
            {
                gui.SetTransform(Matrix3x2.CreateTranslation(20, 20));
                using (gui.Node(10, 10).Enter())
                {
                    gui.DrawBackgroundRect(Color.Red);
                }
            }
        });
        using var image = frame.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        Assert.Equal(SKColors.Cyan, bitmap.GetPixel(25, 25));
        Assert.Equal(0, bitmap.GetPixel(45, 45).Alpha);
    }

    /// <summary>Parameter updates rebuild only changed filter chains.</summary>
    [Fact]
    public void UpdatedParametersRebuildTheFilter()
    {
        using var stack = new RenderEffectStack(new RenderEffect.Brightness(1));
        var paint = stack.Paint(false, 1);
        stack.Update([new RenderEffect.Brightness(1)]);
        Assert.Same(paint, stack.Paint(false, 1));
        stack.Update([new RenderEffect.Brightness(.5f)]);
        using var bitmap = Render(stack);
        Assert.InRange(bitmap.GetPixel(30, 30).Red, 127, 129);
    }

    /// <summary>Preparing a static native stack does not allocate managed memory after warmup.</summary>
    [Fact]
    public void StaticPaintPreparationDoesNotAllocate()
    {
        using var stack = new RenderEffectStack(new RenderEffect.Blur(2), new RenderEffect.Invert());
        for (var i = 0; i < 100; i++)
        {
            _ = stack.Paint(false, 1);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            _ = stack.Paint(false, 1);
        }
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    /// <summary>An explicitly unsupported renderer reports its fallback without applying the filter.</summary>
    [Fact]
    public void UnsupportedRenderTargetsUseIdentityPaint()
    {
        using var stack = new RenderEffectStack(new RenderEffect.Invert());
        Assert.Null(stack.Paint(false, 1, false).ImageFilter);
        Assert.Contains("render targets", Assert.Single(stack.Diagnostics));
    }

    /// <summary>Malformed filters fail before drawing.</summary>
    [Theory]
    [InlineData("blur(NaN)")]
    [InlineData("blur(1,2)")]
    [InlineData("unknown(1)")]
    [InlineData("glow(1,no-color)")]
    [InlineData("blur(1) trailing")]
    [InlineData("bad blur(1)")]
    [InlineData("blur(")]
    public void MalformedFiltersAreRejected(string source) =>
        Assert.Throws<FormatException>(() => RenderEffectParser.Parse(source));
}
