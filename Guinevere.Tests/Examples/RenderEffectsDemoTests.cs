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
        frame.Frame(static gui => Example01.Example01.RenderEffectsDemo(gui, Shared.ColorPalette.Dark));
        Assert.NotEmpty(frame.Gui.RootNode!.Children);
        using var image = frame.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        Assert.Contains(bitmap.Pixels, static pixel => pixel.Alpha > 0);
    }
}
