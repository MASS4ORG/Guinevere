namespace Guinevere.Tests.Controls;

/// <summary>Tests for the <c>Image</c> control.</summary>
public class ImageTests
{
    static SKImage SolidImage(int w, int h)
    {
        var bitmap = new SKBitmap(w, h);
        bitmap.Erase(new SKColor(200, 50, 50, 255));
        return SKImage.FromBitmap(bitmap);
    }

    static Gui BuildStageGui()
    {
        var surface = SKSurface.Create(new SKImageInfo(400, 300));
        var gui = new Gui { Input = Substitute.For<IInputHandler>() };
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        return gui;
    }

    /// <summary>With no explicit size the node is the image's pixel size.</summary>
    [Fact]
    public void Image_DefaultSize_MatchesImagePixels()
    {
        var gui = BuildStageGui();
        using var image = SolidImage(48, 32);

        var node = gui.Image(image);
        gui.CalculateLayout();

        Assert.Equal(48f, node.Rect.W, 1);
        Assert.Equal(32f, node.Rect.H, 1);
    }

    /// <summary>An explicit size overrides the image dimensions.</summary>
    [Fact]
    public void Image_ExplicitSize_OverridesImagePixels()
    {
        var gui = BuildStageGui();
        using var image = SolidImage(48, 32);

        var node = gui.Image(image, width: 120, height: 90);
        gui.CalculateLayout();

        Assert.Equal(120f, node.Rect.W, 1);
        Assert.Equal(90f, node.Rect.H, 1);
    }

    /// <summary>The control renders into its node's rect during the render pass.</summary>
    [Fact]
    public void Image_RenderPass_FillsItsNode()
    {
        using var surface = SKSurface.Create(new SKImageInfo(64, 64, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        using var image = SolidImage(8, 8);

        void Draw(Gui g)
        {
            using (g.Node().Expand().Padding(10).Enter())
                g.Image(image).Expand();
        }

        var gui = new Gui { Input = Substitute.For<IInputHandler>() };
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(canvas);
        Draw(gui);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        Draw(gui);
        gui.Render();
        gui.EndFrame();
        canvas.Flush();

        using var snapshot = surface.Snapshot();
        using var pixmap = snapshot.PeekPixels();
        var span = pixmap.GetPixelSpan();
        var center = ((32 * 64) + 32) * 4;
        Assert.True(span[center] > 150 && span[center + 3] == 255, "image center should be opaque red");

        var corner = ((2 * 64) + 2) * 4; // inside the 10px padding
        Assert.Equal((byte)0, span[corner + 3]);
    }
}
