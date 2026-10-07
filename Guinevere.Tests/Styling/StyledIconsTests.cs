namespace Guinevere.Tests.Styling;

/// <summary>Tests for <see cref="StyledIcons"/>: icon themes defined by <c>icon#id</c> rules.</summary>
public sealed class StyledIconsTests : IDisposable
{
    const string Svg = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><rect width="24" height="24" fill="#f00"/></svg>""";

    readonly string _dir = Directory.CreateTempSubdirectory("guinevere-icons-").FullName;

    public StyledIconsTests()
    {
        using var bitmap = new SKBitmap(4, 2);
        bitmap.Erase(SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(_dir, "folder.png"), png.ToArray());
        File.WriteAllText(Path.Combine(_dir, "folder.svg"), Svg);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    StyleSheet Sheet(string text) =>
        StyleSheet.Parse(text, new StyleSheetOptions { BaseUri = new Uri(_dir + Path.DirectorySeparatorChar) });

    static Gui GuiWith(params StyleSheet[] sheets)
    {
        var gui = new Gui { Input = Substitute.For<IInputHandler>() };
        foreach (var sheet in sheets) gui.StyleSheets.Add(sheet);
        return gui;
    }

    static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Guinevere.slnx"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, relative);
    }

    /// <summary>The same id renders from a glyph theme and from an image theme by swapping sheets.</summary>
    [Fact]
    public void SwappingSheets_SwapsGlyphAndImageThemes()
    {
        var gui = GuiWith(Sheet("""icon#asset\.folder { glyph = "\f07b"; color = #ff0000; }"""));

        var glyph = gui.ResolveIcon("asset.folder");
        gui.StyleSheets[0] = Sheet("""icon#asset\.folder { src = url("folder.png"); tint = true; }""");
        var image = gui.ResolveIcon("asset.folder");

        Assert.Equal((IconKind.Glyph, "", Color.FromArgb(255, 255, 0, 0)), (glyph!.Kind, glyph.Glyph, glyph.Color));
        Assert.Equal((IconKind.Image, true, 4), (image!.Kind, image.Tintable, image.Image!.Width));
    }

    /// <summary><c>src</c> wins over <c>glyph</c>; <c>src = none</c> or an unreadable file falls back to the glyph.</summary>
    [Fact]
    public void Src_WinsOverGlyph_UntilCleared()
    {
        var gui = GuiWith(
            Sheet("""icon#a { glyph = "A"; src = url("folder.png"); }"""),
            Sheet("""icon#b { glyph = "B"; src = url("missing.png"); } icon#c { glyph = "C"; src = url("folder.png"); }"""),
            Sheet("""icon#c { src = none; } icon#d { src = url("http://example.com/d.png"); }"""));

        var a = gui.ResolveIcon("a")!;

        Assert.Equal((IconKind.Image, false), (a.Kind, a.Tintable));
        Assert.Equal("B", gui.ResolveIcon("b")!.Glyph);
        Assert.Equal("C", gui.ResolveIcon("c")!.Glyph);
        Assert.Null(gui.ResolveIcon("d"));
        Assert.Null(gui.ResolveIcon("unknown"));
    }

    /// <summary>Classes, modifiers and states select icon variants.</summary>
    [Fact]
    public void ClassesModifiersAndStates_SelectVariants()
    {
        var gui = GuiWith(Sheet("""
            icon.file.ext-png { glyph = "P"; }
            icon#folder { glyph = "F"; :open { glyph = "O"; } :hover { glyph = "H"; } }
            """));

        Assert.Equal("P", gui.ResolveIcon("any", classes: ["file", "ext-png"])!.Glyph);
        Assert.Equal("O", gui.ResolveIcon("folder", modifiers: ["open"])!.Glyph);
        Assert.Equal("H", gui.ResolveIcon("folder", state: StyleState.Hover)!.Glyph);
        Assert.Equal("F", gui.ResolveIcon("folder")!.Glyph);
    }

    /// <summary>Resolved icons are reused until the sheets change.</summary>
    [Fact]
    public void ResolvedIcons_AreCachedPerSheetVersion()
    {
        var gui = GuiWith(Sheet("""icon#a { src = url("folder.png"); }"""));

        var first = gui.ResolveIcon("a");
        var second = gui.ResolveIcon("a");
        gui.StyleSheets.SetToken("unrelated", "1");
        var third = gui.ResolveIcon("a");

        Assert.Same(first, second);
        Assert.NotSame(first, third);
    }

    /// <summary><c>font-family</c> resolves <c>@font-face</c> files, then system families, else the scope fallback.</summary>
    [Fact]
    public void FontFamily_ResolvesFontFacesThenFallback()
    {
        var font = new Uri(RepoFile("Integrations/Resources/Fonts/widget-icons.ttf")).AbsoluteUri;
        var gui = GuiWith(Sheet($$"""
            @font-face { font-family = "widgets"; src = url("{{font}}"); }
            icon#a { glyph = "\f00c"; font-family = "nope", "widgets"; }
            icon#b { glyph = "b"; font-family = "no-such-family-xyz"; }
            icon#c { glyph = ""; }
            """));

        var a = gui.ResolveIcon("a")!;

        Assert.NotNull(a.Font);
        Assert.NotEqual((ushort)0, a.Font!.SkFont.GetGlyph(0xf00c));
        Assert.Null(gui.ResolveIcon("b")!.Font);
        Assert.Null(gui.ResolveIcon("c"));
    }

    /// <summary>SVG sources need the registered decoder and become tintable pictures on request.</summary>
    [Fact]
    public void SvgSources_UseRegisteredDecoder()
    {
        var gui = GuiWith(Sheet("""icon#a { src = url("folder.svg"); tint = true; color = #00ff00; }"""));

        Assert.Null(gui.ResolveIcon("a"));

        gui.IconDecoders.Add(SvgIconDecoder.Instance);
        gui.StyleSheets.SetToken("reload", "1");
        var icon = gui.ResolveIcon("a")!;

        Assert.Equal((IconKind.Picture, true), (icon.Kind, icon.Tintable));
        Assert.Equal(Color.FromArgb(255, 0, 255, 0), icon.Color);
    }

    /// <summary><c>gui.StyledIcon</c> draws the resolved icon and reserves the square when it is missing.</summary>
    [Fact]
    public void StyledIcon_DrawsOrReserves()
    {
        const int size = 32;
        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        var gui = GuiWith(Sheet("""icon#a { src = url("folder.png"); }"""));
        LayoutNode? missing = null;

        void Draw(Gui g)
        {
            using (g.Node().Direction(Axis.Vertical).Enter())
            {
                g.StyledIcon("a", size);
                missing = g.StyledIcon("missing", 10);
            }
        }

        surface.Canvas.Clear(SKColors.Transparent);
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        Draw(gui);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        Draw(gui);
        gui.Render();
        gui.EndFrame();

        using var snapshot = surface.Snapshot();
        using var pixmap = snapshot.PeekPixels();
        var px = pixmap.GetPixelSpan();
        var center = ((size / 2 * size) + (size / 2)) * 4;
        Assert.Equal((255, 0, 0, 255), (px[center], px[center + 1], px[center + 2], px[center + 3]));
        Assert.Equal((10f, 10f), (missing!.Rect.W, missing.Rect.H));
    }

    /// <summary>The id must be non-empty.</summary>
    [Fact]
    public void ResolveIcon_RejectsEmptyId() =>
        Assert.Throws<ArgumentException>(() => GuiWith().ResolveIcon(""));
}
