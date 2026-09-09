namespace Guinevere.Tests.Controls;

/// <summary>Tests for the multi-state <c>ImageButton</c> control.</summary>
public class ImageButtonTests
{
    private const int Surface = 80;

    private static SKImage Solid(SKColor color)
    {
        var bitmap = new SKBitmap(16, 16);
        bitmap.Erase(color);
        return SKImage.FromBitmap(bitmap);
    }

    private static IInputHandler OffscreenMouse()
    {
        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(-100, -100));
        input.PrevMousePosition.Returns(new Vector2(-100, -100));
        return input;
    }

    private static bool RunFrame(byte[] outPixels, IInputHandler input, Func<Gui, bool> draw)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Surface, Surface, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var gui = new Gui { Input = input };
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(canvas, Font.FromFamilyName("sans-serif", 14), Font.FromFamilyName("sans-serif", 14));
        draw(gui);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        var result = draw(gui);
        gui.Render();
        gui.EndFrame();
        canvas.Flush();

        using var snapshot = surface.Snapshot();
        using var pixmap = snapshot.PeekPixels();
        pixmap.GetPixelSpan()[..outPixels.Length].CopyTo(outPixels);
        return result;
    }

    private static (byte R, byte G, byte B, byte A) At(byte[] px, int x, int y)
    {
        var i = ((y * Surface) + x) * 4;
        return (px[i], px[i + 1], px[i + 2], px[i + 3]);
    }

    /// <summary>With the pointer away, the button shows its normal image.</summary>
    [Fact]
    public void ImageButton_ShowsNormalImageWhenNotHovered()
    {
        using var normal = Solid(new SKColor(220, 30, 30, 255));
        using var hover = Solid(new SKColor(0, 220, 0, 255));
        using var pressed = Solid(new SKColor(0, 0, 220, 255));
        var px = new byte[Surface * Surface * 4];

        // ReSharper disable once AccessToDisposedClosure - RunFrame invokes the callback synchronously
        var clicked = RunFrame(px, OffscreenMouse(),
            gui => gui.ImageButton(normal, hover, pressed, width: 60, height: 40));

        Assert.False(clicked);
        var (r, g, b, _) = At(px, 20, 20);
        Assert.True(r > 180 && g < 80 && b < 80, $"normal image should be red, got ({r},{g},{b})");
    }

    /// <summary>With the pointer over it, the button shows its hover image.</summary>
    [Fact]
    public void ImageButton_ShowsHoverImageWhenHovered()
    {
        using var normal = Solid(new SKColor(220, 30, 30, 255));
        using var hover = Solid(new SKColor(0, 220, 0, 255));
        using var pressed = Solid(new SKColor(0, 0, 220, 255));
        var px = new byte[Surface * Surface * 4];

        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(20, 20)); // inside a button laid out at the origin

        // ReSharper disable once AccessToDisposedClosure - RunFrame invokes the callback synchronously
        RunFrame(px, input, gui => gui.ImageButton(normal, hover, pressed, width: 60, height: 40));

        var (r, g, b, _) = At(px, 20, 20);
        Assert.True(g > 180 && r < 80 && b < 80, $"hover image should be green, got ({r},{g},{b})");
    }

    /// <summary>A disabled button shows the disabled image and never reports a click.</summary>
    [Fact]
    public void ImageButton_Disabled_UsesDisabledImageAndNeverClicks()
    {
        using var normal = Solid(new SKColor(220, 30, 30, 255));
        using var hover = Solid(new SKColor(0, 220, 0, 255));
        using var pressed = Solid(new SKColor(0, 0, 220, 255));
        using var disabled = Solid(new SKColor(90, 90, 90, 255));
        var px = new byte[Surface * Surface * 4];

        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(20, 20));
        input.IsMouseButtonPressed(Arg.Any<MouseButton>()).Returns(true);

        // ReSharper disable once AccessToDisposedClosure - RunFrame invokes the callback synchronously
        var clicked = RunFrame(px, input, gui =>
            gui.ImageButton(normal, hover, pressed, disabled, text: "X", width: 60, height: 40, enabled: false));

        Assert.False(clicked);
        var (r, g, b, _) = At(px, 20, 20);
        Assert.True(r is > 30 and < 160 && g is > 30 and < 160 && b is > 30 and < 160,
            $"disabled image should be grey, got ({r},{g},{b})");
    }

    /// <summary>Nine-slice scaling is accepted and fills the button.</summary>
    [Fact]
    public void ImageButton_NineSlice_Renders()
    {
        using var normal = Solid(new SKColor(220, 30, 30, 255));
        var px = new byte[Surface * Surface * 4];

        // ReSharper disable once AccessToDisposedClosure - RunFrame invokes the callback synchronously
        RunFrame(px, OffscreenMouse(), gui =>
            gui.ImageButton(normal, normal, normal, width: 70, height: 50, nineSlice: new Insets(4)));

        Assert.True(At(px, 30, 25).A > 200, "the nine-sliced button background should be opaque");
    }

    /// <summary>During the layout pass the control reports no click.</summary>
    [Fact]
    public void ImageButton_LayoutPass_ReturnsFalse()
    {
        using var normal = Solid(new SKColor(220, 30, 30, 255));

        var gui = new Gui { Input = OffscreenMouse() };
        var surface = SKSurface.Create(new SKImageInfo(Surface, Surface));
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);

        Assert.False(gui.ImageButton(normal, normal, normal, width: 40, height: 20));
    }
}
