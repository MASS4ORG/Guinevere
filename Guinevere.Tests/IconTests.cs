namespace Guinevere.Tests;

/// <summary>Tests for <see cref="Icon"/> and <see cref="Gui.Icon"/>.</summary>
public class IconTests
{
    const int Surface = 64;

    static SKImage SolidImage(int w, int h, SKColor color)
    {
        var bitmap = new SKBitmap(w, h);
        bitmap.Erase(color);
        return SKImage.FromBitmap(bitmap);
    }

    static SKPicture SquarePicture(SKColor color)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(10, 10, 20, 20));
        canvas.DrawRect(new SKRect(10, 10, 20, 20), new SKPaint { Color = color });
        return recorder.EndRecording();
    }

    static byte[] Render(Action<Gui> draw)
    {
        using var surface =
            SKSurface.Create(new SKImageInfo(Surface, Surface, SKColorType.Rgba8888, SKAlphaType.Unpremul));
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
        return [.. pixmap.GetPixelSpan()];
    }

    static (byte R, byte G, byte B, byte A) At(byte[] px, int x, int y)
    {
        var i = ((y * Surface) + x) * 4;
        return (px[i], px[i + 1], px[i + 2], px[i + 3]);
    }

    /// <summary>The most opaque pixel and the bounds of every visible pixel.</summary>
    static ((byte R, byte G, byte B, byte A) Strongest, SKRectI Bounds) Ink(byte[] px)
    {
        var strongest = (R: (byte)0, G: (byte)0, B: (byte)0, A: (byte)0);
        var bounds = new SKRectI(Surface, Surface, 0, 0);
        for (var y = 0; y < Surface; y++)
            for (var x = 0; x < Surface; x++)
            {
                var pixel = At(px, x, y);
                if (pixel.A == 0) continue;
                bounds = new SKRectI(Math.Min(bounds.Left, x), Math.Min(bounds.Top, y), Math.Max(bounds.Right, x + 1),
                    Math.Max(bounds.Bottom, y + 1));
                if (pixel.A > strongest.A) strongest = pixel;
            }
        return (strongest, bounds);
    }

    /// <summary>An icon node is a square of the given size, or of the scope text size by default.</summary>
    [Fact]
    public void Icon_LaysOutSquareNode()
    {
        LayoutNode? sized = null, scoped = null;
        Render(gui =>
        {
            sized = gui.Icon(null, 20);
            scoped = gui.Icon(Icon.FromGlyph("A"));
        });

        Assert.Equal((20f, 20f), (sized!.Rect.W, sized.Rect.H));
        Assert.True(scoped!.Rect.W > 0f);
        Assert.Equal(scoped.Rect.W, scoped.Rect.H);
    }

    /// <summary>A missing icon reserves its square and draws nothing.</summary>
    [Fact]
    public void Icon_Null_DrawsNothing() =>
        Assert.Equal((byte)0, Ink(Render(gui => gui.Icon(null, Surface))).Strongest.A);

    /// <summary>Images keep their aspect ratio, centred in the square.</summary>
    [Fact]
    public void ImageIcon_FitsKeepingAspect()
    {
        using var image = SolidImage(2, 1, SKColors.Red);
        var icon = Icon.FromImage(image);

        var px = Render(gui => gui.Icon(icon, Surface));

        Assert.Equal((255, 0, 0, 255), At(px, 32, 32));
        Assert.Equal((byte)0, At(px, 32, 4).A);
        Assert.Equal((byte)0, At(px, 32, 60).A);
    }

    /// <summary>A tint recolors only tintable images; opacity always applies.</summary>
    [Theory]
    [InlineData(false, 255, 255)]
    [InlineData(true, 0, 255)]
    public void ImageIcon_TintOnlyWhenTintable(bool tintable, int red, int green)
    {
        using var image = SolidImage(4, 4, SKColors.White);
        var icon = Icon.FromImage(image, tintable);

        var px = Render(gui => gui.Icon(icon, Surface, Color.FromArgb(255, 0, 255, 0)));
        var faded = Render(gui => gui.Icon(icon, Surface, opacity: 0.5f));

        Assert.Equal((red, green), (At(px, 32, 32).R, At(px, 32, 32).G));
        Assert.InRange(At(faded, 32, 32).A, 120, 135);
    }

    /// <summary>Pictures scale from their cull rectangle to the square and take a tint when tintable.</summary>
    [Fact]
    public void PictureIcon_ScalesFromCullRect()
    {
        var icon = Icon.FromPicture(SquarePicture(SKColors.Red), tintable: true);

        var plain = Render(gui => gui.Icon(icon, Surface));
        var tinted = Render(gui => gui.Icon(icon, Surface, Color.FromArgb(255, 0, 0, 255)));

        Assert.Equal((255, 0, 0, 255), At(plain, 2, 2));
        Assert.Equal((255, 0, 0, 255), At(plain, 61, 61));
        Assert.Equal((0, 0, 255, 255), At(tinted, 32, 32));
    }

    /// <summary>Glyphs draw in the tint, else the icon color, centred in the square.</summary>
    [Fact]
    public void GlyphIcon_TintedAndCentred()
    {
        var icon = Icon.FromGlyph("H", color: Color.FromArgb(255, 255, 0, 0));

        var (own, bounds) = Ink(Render(gui => gui.Icon(icon, Surface)));
        var (tinted, _) = Ink(Render(gui => gui.Icon(icon, Surface, Color.FromArgb(255, 0, 0, 255))));
        var (faded, _) = Ink(Render(gui => gui.Icon(icon, Surface, opacity: 0.5f)));

        Assert.Equal((255, 0, 0), (own.R, own.G, own.B));
        Assert.Equal((0, 0, 255), (tinted.R, tinted.G, tinted.B));
        Assert.InRange(faded.A, 100, 135);
        Assert.InRange(bounds.MidX, 30, 34);
        Assert.InRange(bounds.MidY, 30, 34);
    }

    /// <summary>An explicit font draws the glyph; without one, the scope text color applies.</summary>
    [Fact]
    public void GlyphIcon_ExplicitFontAndScopeColor()
    {
        var icon = Icon.FromGlyph("H", Font.FromFamilyName("monospace"));

        var (ink, _) = Ink(Render(gui =>
        {
            gui.SetTextColor(Color.FromArgb(255, 0, 255, 0));
            gui.Icon(icon, Surface);
        }));

        Assert.Equal((0, 255, 0), (ink.R, ink.G, ink.B));
    }

    /// <summary>Without a font, a glyph the text font lacks comes from the widget-icon font, then the icon font.</summary>
    [Fact]
    public void GlyphIcon_FallsBackToWidgetThenIconFont()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "Guinevere.slnx"))) dir = dir.Parent;
        var icons = Font.FromFile(Path.Combine(dir.FullName, "Integrations/Resources/Fonts/widget-icons.ttf"));
        var check = Icon.FromGlyph("");

        var (widget, _) = Ink(Render(gui =>
        {
            gui.SetWidgetIconFont(icons);
            gui.Icon(check, Surface);
        }));
        var (emoji, _) = Ink(Render(gui =>
        {
            gui.SetEmojiFont(icons);
            gui.Icon(check, Surface);
        }));

        Assert.True(widget.A > 0);
        Assert.True(emoji.A > 0);
    }

    /// <summary><c>gui.DrawIcon</c> draws into a rectangle and ignores empty ones.</summary>
    [Fact]
    public void DrawIcon_DrawsIntoRect()
    {
        using var image = SolidImage(4, 4, SKColors.Red);
        var icon = Icon.FromImage(image);

        var px = Render(gui =>
        {
            gui.DrawIcon(icon, new Rect(0, 0, 16, 16));
            gui.DrawIcon(icon, new Rect(40, 40, 0, 10));
        });

        Assert.Equal((255, 0, 0, 255), At(px, 8, 8));
        Assert.Equal((byte)0, At(px, 40, 44).A);
    }

    /// <summary>Raster files decode without a decoder; a matching decoder wins; undecodable data is null.</summary>
    [Fact]
    public void FromStream_PicksDecoderOrRaster()
    {
        using var image = SolidImage(3, 2, SKColors.Red);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        var decoded = Icon.FromGlyph("x");
        var decoder = Substitute.For<IIconDecoder>();
        decoder.CanDecode(".x").Returns(true);
        decoder.Decode(Arg.Any<Stream>()).Returns(decoded);

        var raster = Icon.FromStream(new MemoryStream(png.ToArray()), ".png", [decoder]);

        Assert.Equal((IconKind.Image, 3), (raster!.Kind, raster.Image!.Width));
        Assert.Same(decoded, Icon.FromStream(new MemoryStream([1, 2, 3]), ".x", [decoder]));
        Assert.Null(Icon.FromStream(new MemoryStream([1, 2, 3]), ".png"));
    }

    /// <summary><see cref="Icon.FromFile"/> picks the decoder by file extension.</summary>
    [Fact]
    public void FromFile_DecodesRaster()
    {
        var path = Path.Combine(Path.GetTempPath(), $"icon-{Guid.NewGuid():N}.png");
        using (var image = SolidImage(5, 5, SKColors.Red))
        using (var png = image.Encode(SKEncodedImageFormat.Png, 100))
            File.WriteAllBytes(path, png.ToArray());
        try
        {
            Assert.Equal(5, Icon.FromFile(path)!.Image!.Width);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Factories reject empty input and glyphs are always tintable.</summary>
    [Fact]
    public void Factories_Validate()
    {
        Assert.Throws<ArgumentException>(() => Icon.FromGlyph(""));
        Assert.Throws<ArgumentNullException>(() => Icon.FromImage(null!));
        Assert.Throws<ArgumentNullException>(() => Icon.FromPicture(null!));
        Assert.True(Icon.FromGlyph("a").Tintable);
    }

    /// <summary>Fitting keeps the aspect ratio and leaves degenerate sources at the full bounds.</summary>
    [Fact]
    public void Fit_KeepsAspect()
    {
        Assert.Equal(new Rect(0, 25, 100, 50), Gui.Fit(new Rect(0, 0, 100, 100), 2, 1));
        Assert.Equal(new Rect(0, 0, 10, 10), Gui.Fit(new Rect(0, 0, 10, 10), 0, 1));
    }
}
