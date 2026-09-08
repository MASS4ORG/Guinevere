namespace Guinevere.Tests;

/// <summary>Tests for <see cref="Gui.DrawImage(SKImage, Rect, Rect?, Color?, float)"/> and <see cref="Bitmap"/>.</summary>
public class DrawImageTests
{
    private const int Surface = 64;

    private static SKImage SolidImage(int w, int h, SKColor color)
    {
        var bitmap = new SKBitmap(w, h);
        bitmap.Erase(color);
        return SKImage.FromBitmap(bitmap);
    }

    private static SKSurface NewSurface() =>
        SKSurface.Create(new SKImageInfo(Surface, Surface, SKColorType.Rgba8888, SKAlphaType.Unpremul));

    private static void RunFrame(SKSurface surface, Action<Gui> draw)
    {
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var gui = new Gui { Input = Substitute.For<IInputHandler>() };
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(canvas);
        draw(gui);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        draw(gui);
        gui.Render();
        gui.EndFrame();
        canvas.Flush();
    }

    private static (byte R, byte G, byte B, byte A) PixelAt(SKSurface surface, int x, int y)
    {
        using var snapshot = surface.Snapshot();
        using var pixmap = snapshot.PeekPixels();
        var span = pixmap.GetPixelSpan();
        var i = ((y * Surface) + x) * 4;
        return (span[i], span[i + 1], span[i + 2], span[i + 3]);
    }

    /// <summary>The image fills exactly the destination rectangle; outside it stays clear.</summary>
    [Fact]
    public void DrawImage_FillsDestinationRectAndNothingElse()
    {
        using var surface = NewSurface();
        using var image = SolidImage(8, 8, new SKColor(255, 0, 0, 255));

        RunFrame(surface, gui => gui.DrawImage(image, new Rect(16, 16, 24, 24)));

        Assert.Equal((255, 0, 0, 255), PixelAt(surface, 28, 28)); // inside
        Assert.Equal((byte)0, PixelAt(surface, 4, 4).A);          // outside
    }

    /// <summary>A tint multiplies the source colours.</summary>
    [Fact]
    public void DrawImage_Tint_MultipliesColour()
    {
        using var surface = NewSurface();
        using var image = SolidImage(4, 4, new SKColor(255, 255, 255, 255));

        RunFrame(surface, gui => gui.DrawImage(image, new Rect(0, 0, Surface, Surface), null, Color.FromArgb(255, 0, 128, 0)));

        var (r, g, b, a) = PixelAt(surface, 32, 32);
        Assert.Equal((byte)0, r);
        Assert.InRange(g, 120, 135);
        Assert.Equal((byte)0, b);
        Assert.Equal((byte)255, a);
    }

    /// <summary>Opacity scales the alpha of the blit.</summary>
    [Fact]
    public void DrawImage_Opacity_ScalesAlpha()
    {
        using var surface = NewSurface();
        using var image = SolidImage(4, 4, new SKColor(255, 255, 255, 255));

        RunFrame(surface, gui => gui.DrawImage(image, new Rect(0, 0, Surface, Surface), null, null, 0.5f));

        Assert.InRange(PixelAt(surface, 32, 32).A, 120, 135);
    }

    /// <summary>A source sub-rectangle selects part of the image.</summary>
    [Fact]
    public void DrawImage_SourceRect_SamplesSubRegion()
    {
        var bitmap = new SKBitmap(2, 1);
        bitmap.SetPixel(0, 0, new SKColor(255, 0, 0, 255));
        bitmap.SetPixel(1, 0, new SKColor(0, 0, 255, 255));
        using var image = SKImage.FromBitmap(bitmap);
        using var surface = NewSurface();

        RunFrame(surface, gui =>
            gui.DrawImage(image, new Rect(0, 0, Surface, Surface), new Rect(1, 0, 1, 1))); // right (blue) half only

        Assert.Equal((0, 0, 255, 255), PixelAt(surface, 32, 32));
    }

    /// <summary>Drawing does nothing during the layout pass.</summary>
    [Fact]
    public void DrawImage_LayoutPass_DrawsNothing()
    {
        using var surface = NewSurface();
        using var image = SolidImage(4, 4, new SKColor(255, 0, 0, 255));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var gui = new Gui { Input = Substitute.For<IInputHandler>() };
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(canvas);
        gui.DrawImage(image, new Rect(0, 0, Surface, Surface));
        gui.CalculateLayout();
        gui.Render();
        gui.EndFrame();
        canvas.Flush();

        Assert.Equal((byte)0, PixelAt(surface, 32, 32).A);
    }

    /// <summary><see cref="Bitmap.FromEncoded"/> round-trips a PNG.</summary>
    [Fact]
    public void Texture_FromEncoded_DecodesPng()
    {
        using var src = SolidImage(6, 4, new SKColor(10, 20, 30, 255));
        using var data = src.Encode(SKEncodedImageFormat.Png, 100);

        using var bitmap = Bitmap.FromEncoded(data.AsSpan());

        Assert.Equal(6, bitmap.Width);
        Assert.Equal(4, bitmap.Height);
    }

    /// <summary><see cref="Bitmap.FromPixels"/> wraps raw pixels and rejects short buffers.</summary>
    [Fact]
    public void Texture_FromPixels_WrapsAndValidates()
    {
        var info = new SKImageInfo(2, 2, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = Bitmap.FromPixels(info, new byte[2 * 2 * 4]);

        Assert.Equal(2, bitmap.Width);
        Assert.Throws<ArgumentException>(() => Bitmap.FromPixels(info, new byte[3]));
    }
}
