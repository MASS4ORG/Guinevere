namespace Guinevere.Tests.Platform;

/// <summary>Checks startup placement on small desktops and monitors with nonzero origins.</summary>
public sealed class WindowPlacementTests
{
    /// <summary>Monitor selection follows the window across positive and negative desktop origins.</summary>
    [Theory]
    [InlineData(100, 100, 0)]
    [InlineData(2000, 100, 1)]
    [InlineData(-1800, 100, 2)]
    [InlineData(1750, 100, 1)]
    [InlineData(5000, 100, 1)]
    public void SelectsTheWindowMonitor(float x, float y, int expected)
    {
        Rect[] areas = [new(0, 30, 1920, 1050), new(1920, 0, 1920, 1080), new(-1920, 0, 1920, 1080)];
        Assert.Equal(expected, WindowPlacement.SelectMonitor(new Rect(x, y, 800, 600), areas));
    }

    /// <summary>Invalid monitor areas are skipped and a missing desktop has no selection.</summary>
    [Fact]
    public void IgnoresUnavailableMonitors()
    {
        Assert.Equal(-1, WindowPlacement.SelectMonitor(new Rect(0, 0, 800, 600), []));
        Assert.Equal(-1, WindowPlacement.SelectMonitor(new Rect(0, 0, 800, 600), [new Rect()]));
        Assert.Equal(1, WindowPlacement.SelectMonitor(new Rect(0, 0, 800, 600),
            [new Rect(), new Rect(0, 30, 1920, 1050)]));
    }

    /// <summary>The entire client area and application bar stay in the work area.</summary>
    [Theory]
    [InlineData(1600, 950, 0, 30, 1366, 738)]
    [InlineData(800, 600, -1920, 0, 1920, 1080)]
    [InlineData(800, 600, 1920, 24, 1920, 1056)]
    public void InitialWindowFitsItsMonitor(float width, float height, float x, float y, float w, float h)
    {
        var area = new Rect(x, y, w, h);
        var result = WindowPlacement.Center(new Vector2(width, height), area);
        Assert.True(result.X >= x && result.Y >= y);
        Assert.True(result.X + result.W <= x + w && result.Y + result.H <= y + h);
        Assert.Equal(x + (w - result.W) * 0.5f, result.X);
        Assert.Equal(y + (h - result.H) * 0.5f, result.Y);
    }
}
