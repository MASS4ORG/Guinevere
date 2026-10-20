using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Examples;

/// <summary>Exercises the effect gallery on a raster canvas.</summary>
public class RenderEffectsDemoTests
{
    /// <summary>All effect cards build and render their text, shapes and image content.</summary>
    [Fact]
    public void GalleryRendersAllBuiltInFilters()
    {
        using var frame = new FrameHarness(1200, 1600);
        frame.Frame(gui => Example01.Shared.RenderEffectsDemo(gui, Example01.ColorPalette.Dark));
        Assert.NotEmpty(frame.Gui.RootNode!.Children);
        using var image = frame.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        Assert.Contains(bitmap.Pixels, pixel => pixel.Alpha > 0);
    }
}
