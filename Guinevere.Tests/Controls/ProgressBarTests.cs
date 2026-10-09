using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the geometry of the progress bar fill: a known fraction anchors left and clamps, while an
/// indeterminate bar sweeps a chunk that never escapes the track.
/// </summary>
public class ProgressBarTests
{
    static readonly Rect Track = new(10, 4, 200, 6);

    /// <summary>A known fraction fills that share of the track from its left edge.</summary>
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.25f, 50f)]
    [InlineData(1f, 200f)]
    public void AKnownFractionFillsFromTheLeft(float fraction, float expectedWidth)
    {
        var fill = ControlsExtensions.FillRect(Track, fraction, elapsed: 0);

        Assert.Equal(Track.X, fill.X);
        Assert.Equal(Track.Y, fill.Y);
        Assert.Equal(Track.H, fill.H);
        Assert.Equal(expectedWidth, fill.W, 3);
    }

    /// <summary>Out-of-range fractions clamp rather than overflowing the track.</summary>
    [Theory]
    [InlineData(-0.5f, 0f)]
    [InlineData(1.5f, 200f)]
    public void FractionsAreClamped(float fraction, float expectedWidth)
    {
        Assert.Equal(expectedWidth, ControlsExtensions.FillRect(Track, fraction, elapsed: 0).W, 3);
    }

    /// <summary>The indeterminate chunk stays inside the track across a whole sweep.</summary>
    [Fact]
    public void TheIndeterminateChunkNeverLeavesTheTrack()
    {
        for (var elapsed = 0f; elapsed < 10f; elapsed += 0.05f)
        {
            var fill = ControlsExtensions.FillRect(Track, fraction: null, elapsed);

            Assert.True(fill.X >= Track.X, $"chunk started left of the track at {elapsed}s");
            Assert.True(fill.X + fill.W <= Track.X + Track.W + 0.001f,
                $"chunk ran past the right edge at {elapsed}s");
            Assert.True(fill.W >= 0, $"chunk had negative width at {elapsed}s");
        }
    }

    /// <summary>The chunk actually travels, rather than sitting still.</summary>
    [Fact]
    public void TheIndeterminateChunkMovesOverTime()
    {
        var atStart = ControlsExtensions.FillRect(Track, fraction: null, elapsed: 0.5f);
        var later = ControlsExtensions.FillRect(Track, fraction: null, elapsed: 1.2f);

        Assert.NotEqual(atStart.X, later.X);
    }

    /// <summary>The default width fills the parent while explicit dimensions override sheet dimensions.</summary>
    [Theory]
    [InlineData(-1f, null, 200f, 6f)]
    [InlineData(0f, null, 200f, 6f)]
    [InlineData(120f, 12f, 120f, 12f)]
    public void DimensionsRespectTheParentAndArguments(float width, float? height, float expectedWidth,
        float expectedHeight)
    {
        using var harness = new FrameHarness(200, 40);
        harness.Frame(g => g.ProgressBar(0.5f, width, height, id: "loading"));
        var node = Assert.Single(harness.Gui.RootNode!.Children);
        Assert.Equal(expectedWidth, node.Rect.W);
        Assert.Equal(expectedHeight, node.Rect.H);
    }

    /// <summary>Fill borders, radii, opacity and inherited tokens come from the directly selected part.</summary>
    [Fact]
    public void FillUsesItsBoxStyleInsideThePadding()
    {
        using var harness = new FrameHarness(220, 40);
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("""
            progress.custom#loading { height = 20; padding = 2; border-radius = 0; background-color = #0000ff; }
            progress.custom#loading > fill {
                background-color = $fill; border-radius = 0; border-width = 2; border-color = #000000; opacity = 0.5;
            }
            """));
        harness.Frame(g =>
        {
            g.DrawBackgroundRect(Color.White);
            g.SetStyleToken("fill", Color.Red);
            g.ProgressBar(0.5f, width: 200, classes: ["custom"], id: "loading");
        });
        using var image = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(image);
        Assert.Equal(128, pixels.GetPixel(20, 10).Red);
        Assert.InRange(pixels.GetPixel(20, 10).Blue, (byte)126, (byte)128);
        Assert.Equal(0, pixels.GetPixel(2, 10).Red);
        Assert.InRange(pixels.GetPixel(2, 10).Blue, (byte)126, (byte)128);
        Assert.Equal(SKColors.Blue, pixels.GetPixel(150, 10));
        Assert.Equal(SKColors.Blue, pixels.GetPixel(20, 0));
    }

    /// <summary>Unknown progress exposes its semantic modifier to track and fill rules.</summary>
    [Fact]
    public void IndeterminateRulesStyleTheAnimatedFill()
    {
        using var harness = new FrameHarness(200, 40);
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("""
            progress:indeterminate { background-color = #0000ff; }
            progress:indeterminate > fill { background-color = #ff0000; }
            """));
        for (var i = 0; i < 30; i++) harness.Frame(g => g.ProgressBar(null, width: 200));
        using var image = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(image);
        Assert.Equal(SKColors.Red, pixels.GetPixel(40, 3));
        Assert.Equal(SKColors.Blue, pixels.GetPixel(180, 3));
    }

    /// <summary>Empty tracks and zero fractions queue no fill.</summary>
    [Theory]
    [InlineData(0f, 200f)]
    [InlineData(1f, 0f)]
    public void EmptyFillDoesNotDraw(float fraction, float parentWidth)
    {
        using var harness = new FrameHarness(200, 40);
        harness.Frame(g =>
        {
            g.DrawBackgroundRect(Color.White);
            using (g.Node().Width(UnitValue.Pixels(parentWidth)).Height(20).Enter()) g.ProgressBar(fraction, width: -1);
        });
        using var image = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(image);
        Assert.NotEqual((SKColor)ExcaliburStyles.TokenColor(harness.Gui, "accent"), pixels.GetPixel(20, 3));
    }
}
