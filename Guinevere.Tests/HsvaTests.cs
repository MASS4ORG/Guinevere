namespace Guinevere.Tests;

/// <summary>HSV conversions, hint handling for grays and black, and syncing with an external color.</summary>
public class HsvaTests
{
    /// <summary>Every byte color survives a round trip through HSV.</summary>
    [Fact]
    public void RoundTripsEveryChannelLevel()
    {
        for (var level = 0; level < 256; level += 5)
        {
            foreach (var color in new[]
                     {
                         Color.FromArgb(level, level, 255 - level, 0), Color.FromArgb(255, 0, level, 255),
                         Color.FromArgb(128, 255, 0, level), Color.FromArgb(255, level, level, level)
                     })
            {
                Assert.Equal(color, Hsva.FromColor(color).ToColor());
            }
        }
    }

    /// <summary>Primary and secondary hues land on their sectors.</summary>
    [Theory]
    [InlineData("#ff0000", 0f)]
    [InlineData("#ffff00", 60f)]
    [InlineData("#00ff00", 120f)]
    [InlineData("#00ffff", 180f)]
    [InlineData("#0000ff", 240f)]
    [InlineData("#ff00ff", 300f)]
    public void HuesLandOnTheirSectors(string hex, float hue)
    {
        var value = Hsva.FromColor(Color.ParseHex(hex));
        Assert.Equal(hue, value.H, 3);
        Assert.Equal(1f, value.S);
        Assert.Equal(1f, value.V);
    }

    /// <summary>Grays keep the hue hint, black keeps both hints.</summary>
    [Fact]
    public void HintsFillUndefinedComponents()
    {
        Assert.Equal(new Hsva(42f, 0f, 0.5019608f), Hsva.FromColor(Color.FromArgb(255, 128, 128, 128), 42f, 0.7f));
        Assert.Equal(new Hsva(42f, 0.7f, 0f), Hsva.FromColor(Color.Black, 42f, 0.7f));
    }

    /// <summary>Sync keeps the editor's value while it still produces the color, and converts otherwise.</summary>
    [Fact]
    public void SyncPreservesTheEditorValue()
    {
        var editing = new Hsva(200f, 0.8f, 0f);
        Assert.Equal(editing, Hsva.Sync(editing, Color.Black));
        Assert.Equal(new Hsva(200f, 0f, 1f), Hsva.Sync(editing, Color.White));
        Assert.Equal(0f, Hsva.Sync(editing, Color.Red).H);
    }

    /// <summary>Out-of-range components wrap or clamp.</summary>
    [Fact]
    public void NormalizesComponents()
    {
        Assert.Equal(new Hsva(340f, 1f, 0f), new Hsva(-20f, 2f, -1f, 5f).Normalized());
        Assert.Equal(0f, new Hsva(float.NaN, 0f, 0f).Normalized().H);
        Assert.Equal(Color.Red, new Hsva(720f, 1f, 1f).ToColor());
        Assert.Equal(Color.Red, Hsva.FromRgb(2f, -1f, 0f).ToColor());
    }
}
