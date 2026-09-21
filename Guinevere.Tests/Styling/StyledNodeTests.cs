namespace Guinevere.Tests.Styling;

/// <summary>Integration tests for <see cref="Gui.StyledNode"/> — a stylesheet drives a real frame.</summary>
public class StyledNodeTests
{
    const int Size = 80;

    static byte[] RenderFrame(string css, IInputHandler input, Action<Gui> draw)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Size, Size, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var gui = new Gui { Input = input };
        gui.StyleSheets.Add(StyleSheet.Parse(css));

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
        var i = ((y * Size) + x) * 4;
        return (px[i], px[i + 1], px[i + 2], px[i + 3]);
    }

    static IInputHandler MouseAt(float x, float y)
    {
        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(x, y));
        input.PrevMousePosition.Returns(new Vector2(x, y));
        return input;
    }

    /// <summary>Layout and background from a class rule are applied to a styled node.</summary>
    [Fact]
    public void ClassRule_AppliesLayoutAndBackground()
    {
        var px = RenderFrame(
            ".panel { flex-grow: 1; background-color: #ff0000; }",
            MouseAt(-100, -100),
            gui =>
            {
                using (gui.StyledNode("VisualElement", ["panel"]).Enter()) { }
            });

        var (r, g, b, _) = At(px, Size / 2, Size / 2);
        Assert.True(r > 200 && g < 60 && b < 60, $"expected red panel, got ({r},{g},{b})");
    }

    /// <summary>A bare-type selector styles a node by its type name.</summary>
    [Fact]
    public void TypeSelector_Applies()
    {
        var px = RenderFrame(
            "Box { flex-grow: 1; background-color: #00ff00; }",
            MouseAt(-100, -100),
            gui =>
            {
                using (gui.StyledNode("Box").Enter()) { }
            });

        Assert.True(At(px, Size / 2, Size / 2).G > 200, "type selector should paint the box green");
    }

    /// <summary>A <c>:hover</c> rule swaps the background when the pointer is over the node.</summary>
    [Fact]
    public void HoverModifier_SwapsBackground()
    {
        const string css = """
            .btn        { flex-grow: 1; background-color: #101010; }
            .btn:hover  { background-color: #00a2ff; }
            """;

        var idle = RenderFrame(css, MouseAt(-100, -100),
            gui => { using (gui.StyledNode("Button", ["btn"]).Enter()) { } });
        var hot = RenderFrame(css, MouseAt(Size / 2f, Size / 2f),
            gui => { using (gui.StyledNode("Button", ["btn"]).Enter()) { } });

        Assert.True(At(idle, Size / 2, Size / 2) is { R: < 40, G: < 40, B: < 40 }, "idle should be dark");
        Assert.True(At(hot, Size / 2, Size / 2).B > 180, "hover should be blue");
    }

    /// <summary>Styled-node scopes automatically provide ancestry and semantic modifiers.</summary>
    [Fact]
    public void NestedRuleAndCustomModifier_ApplyThroughStyledHierarchy()
    {
        var px = RenderFrame("""
            Panel {
                flex-grow: 1;
                > Button:checked { flex-grow: 1; background-color: #00ff00; }
            }
            """, MouseAt(-100, -100), gui =>
            {
                using (gui.StyledNode("Panel").Enter())
                using (gui.StyledNode("Button", modifiers: ["checked"]).Enter()) { }
            });

        Assert.True(At(px, Size / 2, Size / 2).G > 200);
    }

    /// <summary>With no stylesheet a styled node behaves like a plain node.</summary>
    [Fact]
    public void NoStyleSheet_IsHarmless()
    {
        using var surface = SKSurface.Create(new SKImageInfo(Size, Size));
        var gui = new Gui { Input = MouseAt(0, 0) };
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);

        var node = gui.StyledNode("Button", ["x"]);
        Assert.NotNull(node);
    }
}
