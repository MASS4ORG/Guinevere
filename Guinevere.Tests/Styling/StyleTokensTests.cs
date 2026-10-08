namespace Guinevere.Tests.Styling;

/// <summary>Tests for <see cref="StyleTokens"/>: tokens inherited down the node tree like CSS custom properties.</summary>
public class StyleTokensTests
{
    const string Sheet = """
        $accent = #0000ff;
        box { background-color = $accent; }
        .danger { $accent = #ff0000; }
        .hot:hover { $accent = #00ff00; }
        own { $accent = #ffff00; background-color = $accent; }
        """;

    /// <summary>Runs a frame and returns what <paramref name="probe"/> read in the render pass.</summary>
    static Color? Frame(Func<Gui, Color?> probe, Vector2? pointer = null, Action<Gui>? setup = null)
    {
        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(pointer ?? new Vector2(-10, -10));
        input.PrevMousePosition.Returns(pointer ?? new Vector2(-10, -10));
        var gui = new Gui { Input = input };
        gui.StyleSheets.Add(StyleSheet.Parse(Sheet));
        setup?.Invoke(gui);
        Color? result = null;
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        probe(gui);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        result = probe(gui);
        gui.Render();
        gui.EndFrame();
        return result;
    }

    static Color? BoxColor(Gui gui) => gui.ResolveStyle("box").GetColor("background-color");

    static readonly Color Red = Color.FromArgb(255, 255, 0, 0);
    static readonly Color Blue = Color.FromArgb(255, 0, 0, 255);
    static readonly Color Green = Color.FromArgb(255, 0, 255, 0);

    /// <summary>A rule's token reaches every styled descendant, but not siblings outside the subtree.</summary>
    [Fact]
    public void RuleTokens_InheritToDescendants()
    {
        Color? inside = null, outside = null;
        Frame(gui =>
        {
            using (gui.StyledNode("panel", ["danger"]).Enter())
            using (gui.Node(10, 10).Enter())
                inside = BoxColor(gui);
            outside = BoxColor(gui);
            return null;
        });

        Assert.Equal((Red, Blue), (inside, outside));
    }

    /// <summary>Code sets a token for a subtree; nested tokens override outer ones.</summary>
    [Fact]
    public void SetStyleToken_ThemesASubtree()
    {
        var color = Frame(gui =>
        {
            using (gui.StyledNode("panel", ["danger"]).Enter())
            using (gui.Node(10, 10).Enter())
            {
                gui.SetStyleToken("$accent", Green);
                using (gui.Node(5, 5).Enter()) return BoxColor(gui);
            }
        });

        Assert.Equal(Green, color);
    }

    /// <summary>
    /// The element's own rule tokens and call-site variables beat inherited ones; inherited ones beat host tokens.
    /// </summary>
    [Fact]
    public void Precedence_OwnAndCallSiteBeatInherited_InheritedBeatsHost()
    {
        Color? own = null, callSite = null, overHost = null;
        Frame(gui =>
        {
            using (gui.StyledNode("panel", ["danger"]).Enter())
            {
                own = gui.ResolveStyle("own").GetColor("background-color");
                callSite = gui.ResolveStyle("box", variables: [new StyleVariable("accent", Green)])
                    .GetColor("background-color");
                overHost = BoxColor(gui);
            }
            return null;
        }, setup: gui => gui.StyleSheets.SetToken("accent", "#00ffff"));

        Assert.Equal((Color.FromArgb(255, 255, 255, 0), Green, Red), (own, callSite, overHost));
    }

    /// <summary>A token a rule sets only under <c>:hover</c> reaches the subtree while the parent is hovered.</summary>
    [Fact]
    public void HoverTokens_PassDownInTheRenderPass()
    {
        Color? Probe(Gui gui)
        {
            using (gui.StyledNode("panel", ["hot"]).Width(50).Height(50).Enter())
            using (gui.Node(10, 10).Enter())
                return BoxColor(gui);
        }

        Assert.Equal(Green, Frame(Probe, new Vector2(20, 20)));
        Assert.Equal(Blue, Frame(Probe));
    }

    /// <summary>A node whose rules stop setting tokens in the render pass falls back to the inherited tokens.</summary>
    [Fact]
    public void RenderPass_DropsTokensTheBuildPassSet()
    {
        var pass = 0;
        var color = Frame(gui =>
        {
            pass++;
            using (gui.StyledNode("panel", pass == 1 ? ["danger"] : []).Enter())
            using (gui.Node(10, 10).Enter())
                return BoxColor(gui);
        });

        Assert.Equal(Blue, color);
    }

    /// <summary>Outside a frame, resolution simply has no inherited tokens; names are validated.</summary>
    [Fact]
    public void OutsideAFrame_AndValidation()
    {
        var gui = new Gui();
        gui.StyleSheets.Add(StyleSheet.Parse(Sheet));

        Assert.Equal(Blue, BoxColor(gui));
        Assert.Throws<ArgumentException>(() => gui.SetStyleToken(" ", 1));
        Assert.Same(StyleTokens.Default, StyleTokens.Default.With(null));
    }
}
