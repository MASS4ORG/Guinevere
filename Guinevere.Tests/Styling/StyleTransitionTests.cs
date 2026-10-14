using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Styling;

/// <summary>Checks transition annotations, interpolated styled nodes and stylesheet transforms.</summary>
public class StyleTransitionTests
{
    const string FadeSheet = """
        Box { width = 40; height = 40; opacity = 0.2; color = #000000; }
        Box:hover(0.16 linear) { opacity = 1; color = #ffffff; }
        """;

    static float Opacity(LayoutNode node) => node.Scope.Get<LayoutNodeScopeOpacity>().Value;

    /// <summary>Annotations parse in seconds or milliseconds and no longer swallow the rest of the compound.</summary>
    [Fact]
    public void AnnotationsParse()
    {
        var selector = Selector.Parse("Button:hover(150ms ease-in-out-sine).primary");
        Assert.Equal(0.15f, selector.Transition!.Duration, 4);
        Assert.Equal("ease-in-out-sine", selector.Transition.EasingName);
        Assert.False(selector.Matches(new StyleTarget("Button", null, [], StyleState.Hover)));
        Assert.True(selector.Matches(new StyleTarget("Button", null, ["primary"], StyleState.Hover)));

        Assert.Equal(new StyleTransition(0.2f, "ease"), Selector.Parse(":hover(0.2s)").Transition);
        Assert.Null(Selector.Parse("Box:hold(0.05) :focus").Transition);
        Assert.Equal("cubic-bezier(0, 0, 1, 1)",
            Selector.Parse("Box:hover(1 cubic-bezier(0, 0, 1, 1))").Transition!.EasingName);
        Assert.Throws<FormatException>(() => Selector.Parse("Box:hover(fast)"));
        Assert.Throws<StyleSheetException>(() => StyleSheet.Parse("Box:hover(0.1 wobble) { opacity = 1; }"));
    }

    /// <summary>The resolved style reports the transition of the rule that set each property.</summary>
    [Fact]
    public void ResolvedStyleReportsTransitions()
    {
        var gui = new Gui();
        gui.StyleSheets.Add(StyleSheet.Parse(FadeSheet));
        var idle = gui.ResolveStyle("Box");
        var hover = gui.ResolveStyle("Box", state: StyleState.Hover);
        Assert.Null(idle.GetTransition("opacity"));
        Assert.Equal(0.16f, hover.GetTransition("opacity")!.Duration, 4);
        Assert.Null(hover.GetTransition("width"));
    }

    /// <summary>Hover eases in under its annotation, eases back out under the same one, and continues when interrupted.</summary>
    [Fact]
    public void HoverInterpolatesBothWays()
    {
        using var harness = new FrameHarness();
        harness.Gui.StyleSheets.Add(StyleSheet.Parse(FadeSheet));
        var opacity = 0f;
        var color = Color.Black;
        void Draw(Gui gui)
        {
            var node = gui.StyledNode("Box");
            if (gui.Pass != Pass.Pass2Render)
            {
                return;
            }

            opacity = Opacity(node);
            color = node.Scope.Get<LayoutNodeScopeTextColor>().Value;
        }

        harness.Input.MoveTo(new Vector2(300, 200));
        harness.Frame(Draw);
        Assert.Equal(0.2f, opacity, 3);
        harness.Input.MoveTo(new Vector2(20, 20));
        harness.Frame(Draw);
        Assert.Equal(0.2f, opacity, 3);
        harness.Frame(Draw);
        Assert.Equal(0.28f, opacity, 2);
        Assert.InRange(color.R, (byte)20, (byte)30);

        harness.Input.MoveTo(new Vector2(300, 200));
        harness.Frame(Draw);
        Assert.Equal(0.28f, opacity, 2);
        harness.Frame(Draw);
        Assert.Equal(0.272f, opacity, 2);
        for (var i = 0; i < 12; i++)
        {
            harness.Frame(Draw);
        }

        Assert.Equal(0.2f, opacity, 3);
        Assert.Equal(Color.Black, color);
        Assert.Equal(2, StylingExtensions.TransitionsOf(harness.Gui).TrackedCount);
    }

    /// <summary>A running transition requests frames until it finishes.</summary>
    [Fact]
    public void TransitionRequestsFramesWhileRunning()
    {
        using var harness = new FrameHarness();
        harness.Gui.StyleSheets.Add(StyleSheet.Parse(FadeSheet));
        static void Draw(Gui gui) => gui.StyledNode("Box");

        harness.Input.MoveTo(new Vector2(300, 200));
        harness.Frame(Draw);
        Assert.Equal(double.PositiveInfinity, harness.Gui.FrameWaitSeconds);
        harness.Input.MoveTo(new Vector2(20, 20));
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Equal(0, harness.Gui.FrameWaitSeconds);
        for (var i = 0; i < 14; i++)
        {
            harness.Frame(Draw);
        }

        Assert.Equal(double.PositiveInfinity, harness.Gui.FrameWaitSeconds);
    }

    /// <summary>Reduced motion applies the new values at once.</summary>
    [Fact]
    public void ReduceMotionJumps()
    {
        using var harness = new FrameHarness();
        harness.Gui.StyleSheets.Add(StyleSheet.Parse(FadeSheet));
        harness.Gui.ReduceMotion = true;
        var opacity = 0f;
        void Draw(Gui gui)
        {
            var node = gui.StyledNode("Box");
            if (gui.Pass == Pass.Pass2Render)
            {
                opacity = Opacity(node);
            }
        }

        harness.Input.MoveTo(new Vector2(300, 200));
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(20, 20));
        harness.Frame(Draw);
        Assert.Equal(1f, opacity);
    }

    /// <summary>Layout properties under a semantic modifier animate in the build pass.</summary>
    [Fact]
    public void ModifiersAnimateLayout()
    {
        using var harness = new FrameHarness();
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("""
            Box { width = 40; height = 10; }
            Box:checked(0.16 linear) { width = 80; }
            """));
        var on = false;
        LayoutNode? node = null;
        void Draw(Gui gui) => node = gui.StyledNode("Box", modifiers: on ? ["checked"] : []);

        harness.Frame(Draw);
        on = true;
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Equal(44f, node!.Rect.W, 1);
        for (var i = 0; i < 12; i++)
        {
            harness.Frame(Draw);
        }

        Assert.Equal(80f, node!.Rect.W);
    }

    /// <summary>Values whose numbers and colors sit in the same text interpolate; others jump.</summary>
    [Fact]
    public void ChannelsSplitAndFormat()
    {
        Assert.True(StyleValueChannels.TryParse("0 2px 8 #00000080", out var shape, out var channels));
        Assert.Equal([0f, 2f, 8f, 0f, 0f, 0f, 128f], channels);
        Assert.Equal("0 2px 8 #00000080", StyleValueChannels.Format(shape, channels));
        Assert.True(StyleValueChannels.TryParse("red", out var named, out var red));
        Assert.Equal("#ff0000ff", StyleValueChannels.Format(named, red));
        Assert.True(StyleValueChannels.TryParse("#12345 1", out var odd, out _));
        Assert.Equal("#12345 1", StyleValueChannels.Format(odd, [1f]));
        Assert.False(StyleValueChannels.TryParse("auto", out _, out _));

        var transitions = new StyleTransitions();
        using var harness = new FrameHarness();
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("Box { cursor = pointer; } Box:hover(1) { cursor = text; }"));
        harness.Frame(gui =>
        {
            var node = gui.Node();
            var idle = gui.ResolveStyle("Box");
            Assert.Same(idle, transitions.Apply(gui, node, idle));
            var hover = gui.ResolveStyle("Box", state: StyleState.Hover);
            Assert.Same(hover, transitions.Apply(gui, node, hover));
            Assert.Same(hover, transitions.Apply(gui, node, hover));
        });
    }

    /// <summary>Stylesheet transforms use the node's center by default and animate like any other value.</summary>
    [Fact]
    public void TransformPropertiesSetTheNodeTransform()
    {
        using var harness = new FrameHarness();
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("""
            Box { width = 40; height = 40; scale = 1; }
            Box:hover(0.16 linear) { scale = 2; rotate = 0.25turn; translate = 4; }
            .corner { transform-origin = left top; }
            .pixels { transform-origin = 10px 50%; }
            .low { transform-origin = bottom; }
            .far { transform-origin = right bogus; }
            Box.shift:hover { scale = 1 bogus; rotate = 0rad; translate = 1 2; }
            """));
        LayoutNode? node = null;
        harness.Input.MoveTo(new Vector2(300, 200));
        harness.Frame(gui => node = gui.StyledNode("Box"));
        Assert.Equal(Matrix3x2.Identity, node!.Transform);

        harness.Input.MoveTo(new Vector2(20, 20));
        for (var i = 0; i < 14; i++)
        {
            harness.Frame(gui => node = gui.StyledNode("Box"));
        }

        var expected = Matrix3x2.CreateScale(2f, new Vector2(20, 20))
                       * Matrix3x2.CreateRotation(MathF.PI / 2f, new Vector2(20, 20))
                       * Matrix3x2.CreateTranslation(4, 0);
        AssertClose(expected, node!.Transform);

        harness.Frame(gui => node = gui.StyledNode("Box", ["corner"]));
        Assert.Equal(Vector2.Zero, Vector2.Transform(Vector2.Zero, node!.Transform) - new Vector2(4, 0));
        harness.Frame(gui => node = gui.StyledNode("Box", ["pixels"]));
        Assert.Equal(new Vector2(10, 20), Vector2.Transform(new Vector2(10, 20), node!.Transform) - new Vector2(4, 0));
        harness.Frame(gui => node = gui.StyledNode("Box", ["low"]));
        Assert.Equal(new Vector2(20, 40), Vector2.Transform(new Vector2(20, 40), node!.Transform) - new Vector2(4, 0));
        harness.Frame(gui => node = gui.StyledNode("Box", ["far"]));
        Assert.Equal(new Vector2(40, 20), Vector2.Transform(new Vector2(40, 20), node!.Transform) - new Vector2(4, 0));
        harness.Frame(gui => node = gui.StyledNode("Box", ["shift"]));
        Assert.Equal(Matrix3x2.CreateTranslation(1, 2), node!.Transform);
    }

    static void AssertClose(Matrix3x2 expected, Matrix3x2 actual)
    {
        Assert.Equal(expected.M11, actual.M11, 3);
        Assert.Equal(expected.M12, actual.M12, 3);
        Assert.Equal(expected.M21, actual.M21, 3);
        Assert.Equal(expected.M22, actual.M22, 3);
        Assert.Equal(expected.M31, actual.M31, 2);
        Assert.Equal(expected.M32, actual.M32, 2);
    }
}
