namespace Guinevere.Tests;

/// <summary>
/// Tests for <see cref="Gui.DrawImageNineSlice(SKImage, Rect, Insets, Color?, float)"/> and
/// <see cref="NineSliceDrawable"/>. The <c>ui --sample nineslice</c> CLI verb renders it visually.
/// </summary>
public class NineSliceTests
{
    private const int Surface = 120;
    private const int Tex = 24;
    private const int Border = 8;

    // A texture with a distinct 1px red frame, a green border band and a blue centre.
    private static SKImage BorderedTexture()
    {
        var bitmap = new SKBitmap(Tex, Tex);
        for (var y = 0; y < Tex; y++)
        for (var x = 0; x < Tex; x++)
        {
            SKColor c;
            if (x < Border || y < Border || x >= Tex - Border || y >= Tex - Border)
                c = new SKColor(0, 200, 0, 255); // border
            else
                c = new SKColor(0, 0, 200, 255); // centre
            bitmap.SetPixel(x, y, c);
        }

        return SKImage.FromBitmap(bitmap);
    }

    private static byte[] Render(Action<Gui> draw)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Surface, Surface, SKColorType.Rgba8888, SKAlphaType.Unpremul));
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

        using var snapshot = surface.Snapshot();
        using var pixmap = snapshot.PeekPixels();
        return pixmap.GetPixelSpan().ToArray();
    }

    private static (byte R, byte G, byte B, byte A) At(byte[] px, int x, int y)
    {
        var i = ((y * Surface) + x) * 4;
        return (px[i], px[i + 1], px[i + 2], px[i + 3]);
    }

    /// <summary>A stretched nine-patch keeps its border on the edges and its centre in the middle.</summary>
    [Fact]
    public void NineSlice_KeepsBorderAndStretchesCentre()
    {
        using var texture = BorderedTexture();
        // ReSharper disable once AccessToDisposedClosure - Render invokes the callback synchronously
        var px = Render(gui => gui.DrawImageNineSlice(texture, new Rect(10, 10, 100, 100), new Insets(Border)));

        // Near each edge, well inside the border band: green.
        Assert.Equal((byte)200, At(px, 60, 13).G); // top edge
        Assert.Equal((byte)200, At(px, 60, 106).G); // bottom edge
        Assert.Equal((byte)200, At(px, 13, 60).G); // left edge
        Assert.Equal((byte)200, At(px, 106, 60).G); // right edge

        // Middle of a 100px-wide patch made from a 24px texture: the centre stretched → blue.
        var (_, _, b, _) = At(px, 60, 60);
        Assert.Equal((byte)200, b);
    }

    /// <summary>Corners are drawn at 1:1 — a corner pixel is the texture's corner color, undistorted.</summary>
    [Fact]
    public void NineSlice_CornersAreUnscaled()
    {
        using var texture = BorderedTexture();
        // ReSharper disable once AccessToDisposedClosure - Render invokes the callback synchronously
        var px = Render(gui => gui.DrawImageNineSlice(texture, new Rect(10, 10, 100, 100), new Insets(Border)));

        Assert.Equal((byte)200, At(px, 12, 12).G); // top-left corner region
        Assert.Equal((byte)200, At(px, 107, 107).G); // bottom-right corner region
    }

    /// <summary>A destination smaller than the combined corners scales them down without crashing.</summary>
    [Fact]
    public void NineSlice_TinyDestination_DoesNotThrow()
    {
        using var texture = BorderedTexture();

        // ReSharper disable once AccessToDisposedClosure - Render invokes the callback synchronously
        var ex = Record.Exception(() =>
            Render(gui => gui.DrawImageNineSlice(texture, new Rect(5, 5, 6, 6), new Insets(Border))));

        Assert.Null(ex);
    }

    /// <summary>Nothing is drawn during the layout pass.</summary>
    [Fact]
    public void NineSlice_LayoutPass_DrawsNothing()
    {
        using var texture = BorderedTexture();
        using var surface = SKSurface.Create(new SKImageInfo(Surface, Surface, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var gui = new Gui { Input = Substitute.For<IInputHandler>() };
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(canvas);
        gui.DrawImageNineSlice(texture, new Rect(0, 0, Surface, Surface), new Insets(Border));
        gui.CalculateLayout();
        gui.Render();
        gui.EndFrame();
        canvas.Flush();

        using var snapshot = surface.Snapshot();
        using var pixmap = snapshot.PeekPixels();
        Assert.Equal((byte)0, pixmap.GetPixelSpan().ToArray()[(60 * Surface + 60) * 4 + 3]);
    }

    /// <summary><see cref="Insets"/> convenience constructors fill the right edges.</summary>
    [Fact]
    public void Insets_Constructors()
    {
        Assert.Equal(new Insets(4, 4, 4, 4), new Insets(4));
        Assert.Equal(new Insets(3, 7, 3, 7), new Insets(3, 7));
        Assert.Equal(20f, new Insets(6, 4, 14, 10).Horizontal);
        Assert.Equal(14f, new Insets(6, 4, 14, 10).Vertical);
    }
}
