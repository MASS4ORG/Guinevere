namespace Guinevere.Tests.Styling;

/// <summary>
/// Rendering tests for the visual properties a styled node draws: per-corner radius, gradients, shadows, outline,
/// opacity, cursor and inherited text styling.
/// </summary>
public class StyleBoxTests
{
    const int Size = 80;

    /// <summary>Renders one frame with a 20px-padded host so boxes have room for shadows and outlines.</summary>
    static byte[] Render(string pss, Action<Gui> draw, Gui? gui = null)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Size, Size, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        surface.Canvas.Clear(SKColors.Transparent);
        gui ??= new Gui { Input = Substitute.For<IInputHandler>() };
        gui.StyleSheets.Add(StyleSheet.Parse(pss));

        void Frame()
        {
            using (gui.Node(Size, Size).Padding(20).Enter()) draw(gui);
        }

        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        Frame();
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        Frame();
        gui.Render();
        gui.EndFrame();

        using var snapshot = surface.Snapshot();
        using var pixmap = snapshot.PeekPixels();
        return [.. pixmap.GetPixelSpan()];
    }

    static void Box(Gui gui, string type = "box")
    {
        using (gui.StyledNode(type).Enter()) { }
    }

    static (byte R, byte G, byte B, byte A) At(byte[] px, int x, int y)
    {
        var i = ((y * Size) + x) * 4;
        return (px[i], px[i + 1], px[i + 2], px[i + 3]);
    }

    const string Base = "box { width = 40; height = 40; ";

    /// <summary>Each corner takes its own radius from the shorthand.</summary>
    [Fact]
    public void BorderRadius_PerCorner()
    {
        var px = Render(Base + "background-color = #ff0000; border-radius = 16 0 0 0; }", g => Box(g));

        Assert.Equal((byte)0, At(px, 21, 21).A);
        Assert.Equal((255, 0, 0, 255), At(px, 58, 21));
        Assert.Equal((255, 0, 0, 255), At(px, 21, 58));
    }

    /// <summary>A linear gradient runs in the CSS direction; <c>background</c> also takes a plain color.</summary>
    [Fact]
    public void Background_GradientAndColor()
    {
        var gradient = Render(Base + "background = linear-gradient(to right, #ff0000, #0000ff); }", g => Box(g));
        var solid = Render(Base + "background = #00ff00; }", g => Box(g));

        Assert.True(At(gradient, 21, 40).R > 200 && At(gradient, 21, 40).B < 50);
        Assert.True(At(gradient, 58, 40).B > 200 && At(gradient, 58, 40).R < 50);
        Assert.Equal((0, 255, 0, 255), At(solid, 40, 40));
    }

    /// <summary>Outer shadows paint outside the box only; inset shadows paint inside along the offset edge.</summary>
    [Fact]
    public void BoxShadow_OuterAndInset()
    {
        var outer = Render(Base + "box-shadow = 6px 6px 0 #000000; }", g => Box(g));
        var inset = Render(Base + "background-color = #ffffff; box-shadow = inset 0 6px 0 0 #000000; }", g => Box(g));
        var spread = Render(Base + "background-color = #ffffff; box-shadow = inset 0 0 0 30px #000000, 0 0 4px 2px #ff0000; }",
            g => Box(g));

        Assert.Equal((0, 0, 0, 255), At(outer, 63, 63));
        Assert.Equal((byte)0, At(outer, 40, 40).A);
        Assert.Equal((0, 0, 0, 255), At(inset, 40, 22));
        Assert.Equal((255, 255, 255, 255), At(inset, 40, 50));
        Assert.Equal((0, 0, 0, 255), At(spread, 40, 40));
        Assert.True(At(spread, 18, 40).R > 0);
    }

    /// <summary>The outline is a ring outside the box at its offset; borders still draw on the edge.</summary>
    [Fact]
    public void Outline_DrawsOutsideTheBox()
    {
        var px = Render(Base + "border-color = #00ff00; border-width = 2; outline = 2px solid #0000ff; outline-offset = 3; }",
            g => Box(g));

        Assert.Equal((0, 0, 255, 255), At(px, 40, 15));
        Assert.Equal((0, 255, 0, 255), At(px, 40, 20));
        Assert.Equal((byte)0, At(px, 40, 40).A);
    }

    /// <summary>
    /// Opacity fades the node and its descendants as one group, like CSS: an opaque-composited child does not show
    /// the parent through itself, and a fully transparent node hides its subtree.
    /// </summary>
    [Fact]
    public void Opacity_CompositesTheSubtreeAsAGroup()
    {
        var px = Render("""
            box { width = 40; height = 40; padding = 10; background-color = #ff0000; opacity = 50%; }
            inner { width = 20; height = 20; background-color = #0000ff; opacity = 0.5; }
            hidden { width = 1; height = 1; opacity = 0; background-color = #ffffff; }
            """, g =>
        {
            using (g.StyledNode("box").Enter())
            {
                Box(g, "inner");
                Box(g, "hidden");
            }
        });

        // Inside the group the child is 50% blue over opaque red; the whole group is then drawn at 50%.
        Assert.InRange(At(px, 22, 22).A, 120, 135);
        Assert.InRange(At(px, 40, 40).A, 120, 135);
        Assert.InRange(At(px, 40, 40).R, 120, 135);
        Assert.InRange(At(px, 40, 40).B, 120, 135);
    }

    /// <summary>The group's offscreen layer covers ink outside the box: blurred shadows and outlines.</summary>
    [Fact]
    public void Opacity_LayerKeepsShadowsAndOutlines()
    {
        var px = Render(Base + "opacity = 0.5; box-shadow = 8px 8px 6px #000000; outline = 2px #0000ff; outline-offset = 4; }",
            g => Box(g));

        Assert.InRange(At(px, 64, 64).A, 20, 135);
        Assert.InRange(At(px, 40, 15).A, 120, 135);
    }

    /// <summary>Descendants on another z-layer leave the group but still get its opacity multiplied in.</summary>
    [Fact]
    public void Opacity_FadesDescendantsOnOtherLayers()
    {
        var px = Render("""
            box { width = 40; height = 40; background-color = #ff0000; opacity = 0.5; }
            over { width = 10; height = 10; background-color = #0000ff; }
            """, g =>
        {
            using (g.StyledNode("box").Enter())
            using (g.StyledNode("over").Enter())
                g.SetZIndex(5);
        });

        // The layered child is drawn after the group, itself at 50%: 0.5 + 0.5 × 0.5 = 75% coverage, not opaque blue.
        Assert.InRange(At(px, 25, 25).A, 185, 200);
        Assert.InRange(At(px, 25, 25).B, 160, 180);
        Assert.InRange(At(px, 50, 50).A, 120, 135);
    }

    /// <summary>Text color, size and font set on a parent are inherited by child text.</summary>
    [Fact]
    public void TextProperties_AreInherited()
    {
        LayoutNodeScope? scope = null;
        Font? font = null;
        Render("""
            card { color = #00ff00; font-size = 20; font-weight = bold; font-style = italic; }
            """, g =>
        {
            using (g.StyledNode("card").Enter())
            using (g.Node(10, 10).Enter())
            {
                scope = g.CurrentNodeScope;
                font = scope.Get<LayoutNodeScopeTextFont>().Value;
            }
        });

        Assert.Equal(Color.FromArgb(255, 0, 255, 0), scope!.Get<LayoutNodeScopeTextColor>().Value);
        Assert.Equal(20f, scope.Get<LayoutNodeScopeTextSize>().Value);
        Assert.True(font!.SkFont.Embolden || font.SkFont.Typeface.FontWeight >= 600);
        Assert.True(font.SkFont.SkewX != 0f || font.SkFont.Typeface.FontSlant != SKFontStyleSlant.Upright);
    }

    /// <summary>Synthesized bold survives resizing in <c>DrawText</c>, so bold text draws wider ink.</summary>
    [Fact]
    public void SynthesizedBold_DrawsHeavierText()
    {
        static int Ink(byte[] px) => Enumerable.Range(0, Size * Size).Count(i => px[(i * 4) + 3] > 128);

        var regular = Render("card { color = #000000; }", g =>
        {
            using (g.StyledNode("card").Enter()) g.DrawText("WWW", 16);
        });
        var bold = Render("card { color = #000000; font-weight = 900; }", g =>
        {
            using (g.StyledNode("card").Enter()) g.DrawText("WWW", 16);
        });

        Assert.True(Ink(bold) > Ink(regular));
    }

    /// <summary><c>font-family</c> finds <c>@font-face</c> files; unknown families keep the inherited font.</summary>
    [Fact]
    public void FontFamily_UsesFontFaces()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "Guinevere.slnx"))) dir = dir.Parent;
        var file = new Uri(Path.Combine(dir.FullName, "Integrations/Resources/Fonts/Roboto-Regular.ttf")).AbsoluteUri;
        Font? found = null, missing = null, inherited = null;

        Render($$"""
            @font-face { font-family = "Brand"; src = url("{{file}}"); }
            a { font-family = "Brand"; }
            b { font-family = "no-such-family-xyz"; }
            """, g =>
        {
            inherited = g.CurrentNodeScope.Get<LayoutNodeScopeTextFont>().Value;
            using (g.StyledNode("a").Enter()) found = g.CurrentNodeScope.Get<LayoutNodeScopeTextFont>().Value;
            using (g.StyledNode("b").Enter()) missing = g.CurrentNodeScope.Get<LayoutNodeScopeTextFont>().Value;
        });

        Assert.Equal("Roboto", found!.SkFont.Typeface.FamilyName);
        Assert.Same(inherited, missing);
    }

    /// <summary><c>cursor</c> sets the node's pointer cursor in the build pass.</summary>
    [Fact]
    public void Cursor_IsApplied()
    {
        LayoutNode? node = null;
        Render("box { cursor = pointer; } plain { }", g =>
        {
            node = g.StyledNode("box");
            g.StyledNode("plain");
        });

        Assert.Equal(PointerCursor.Hand, node!.CursorShape);
    }

    /// <summary>A parent's <c>:hover</c> text color reaches a styled child that only sets its own font size.</summary>
    [Fact]
    public void HoverTextColor_ReachesStyledChildren()
    {
        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(40, 40));
        input.PrevMousePosition.Returns(new Vector2(40, 40));
        LayoutNodeScope? scope = null;

        Render(Base + "color = #00ff00; :hover { color = #ff0000; } } inner { font-size = 20; }", g =>
        {
            using (g.StyledNode("box").Enter())
            using (g.StyledNode("inner").Enter())
                scope = g.CurrentNodeScope;
        }, new Gui { Input = input });

        Assert.Equal(Color.FromArgb(255, 255, 0, 0), scope!.Get<LayoutNodeScopeTextColor>().Value);
        Assert.Equal(20f, scope.Get<LayoutNodeScopeTextSize>().Value);
    }

    /// <summary>A <c>:hover</c> cursor replaces the base one while the pointer is over the node.</summary>
    [Theory]
    [InlineData(40f, PointerCursor.Move)]
    [InlineData(5f, PointerCursor.Hand)]
    public void Cursor_FollowsHoverState(float pointer, PointerCursor expected)
    {
        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(pointer, pointer));
        input.PrevMousePosition.Returns(new Vector2(pointer, pointer));
        LayoutNode? node = null;

        Render(Base + "cursor = pointer; :hover { cursor = grab; } }", g => node = g.StyledNode("box"),
            new Gui { Input = input });

        Assert.Equal(expected, node!.CursorShape);
    }
}
