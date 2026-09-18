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
}
