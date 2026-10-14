using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Layout;

/// <summary>Checks safe layout policies and their rendering and pointer integration.</summary>
public class ViewportOptionsTests
{
    static ViewportOptions Options => new()
    {
        Bounds = new Rect(100, 50, 800, 600),
        Insets = new ViewportInsets(20, 40, 20, 40),
        ReferenceResolution = new Vector2(400, 300)
    };

    /// <summary>Every scale mode resolves repeatable bounds and uniform geometry.</summary>
    [Theory]
    [InlineData(ReferenceScaleMode.Fit, 1.7333333f)]
    [InlineData(ReferenceScaleMode.Fill, 1.9f)]
    [InlineData(ReferenceScaleMode.MatchWidth, 1.9f)]
    [InlineData(ReferenceScaleMode.MatchHeight, 1.7333333f)]
    [InlineData(ReferenceScaleMode.Constant, 1f)]
    public void ModesProduceUniformDeterministicScales(ReferenceScaleMode mode, float expected)
    {
        var options = Options with { ScaleMode = mode };
        var layout = options.Resolve();
        Assert.Equal(expected, layout.Scale, 5);
        Assert.Equal(new Rect(120, 90, 760, 520), layout.SafeBounds);
        Assert.Equal(layout, options.Resolve());
        var origin = Vector2.Transform(Vector2.Zero, layout.Transform);
        Assert.Equal(120, origin.X, 4);
        Assert.Equal(90, origin.Y, 4);
        Assert.Equal(760, layout.ContentBounds.W * layout.Scale, 4);
        Assert.Equal(520, layout.ContentBounds.H * layout.Scale, 4);
    }

    /// <summary>Reference preservation centers content and limits user scaling.</summary>
    [Fact]
    public void ReferenceAspectIsCenteredAndUserScaleIsClamped()
    {
        var options = Options with { AspectPolicy = ViewportAspectPolicy.PreserveReference };
        var layout = options.Resolve();
        Assert.Equal(new Rect(0, 0, 400, 300), layout.ContentBounds);
        Assert.Equal(153.33333f, layout.Transform.M31, 4);
        Assert.Equal(90, layout.Transform.M32, 4);
        Assert.Equal(4, (options with { UiScale = 100 }).Resolve().Scale);
        Assert.Equal(0.25f, (options with { UiScale = 0.01f }).Resolve().Scale);
        Assert.Equal(3, (options with
        {
            ScaleMode = ReferenceScaleMode.Constant,
            DisplayScale = 2,
            UiScale = 1.5f
        }).Resolve().Scale);
    }

    /// <summary>Safe edge anchors remain reachable across common display shapes.</summary>
    [Theory]
    [InlineData(3440, 1440, 0, 0, 0, 0)]
    [InlineData(1080, 2400, 0, 100, 0, 80)]
    [InlineData(1920, 1080, 96, 54, 96, 54)]
    [InlineData(960, 1080, 0, 0, 0, 0)]
    public void EdgeAnchorsRemainInsideSafeBounds(int width, int height, int left, int top, int right, int bottom)
    {
        var layout = new ViewportOptions
        {
            Bounds = new Rect(0, 0, width, height),
            Insets = new ViewportInsets(left, top, right, bottom)
        }.Resolve();
        var rect = layout.Anchor(Vector2.One, Vector2.One, new Vector2(100, 50), Vector2.One);
        var edge = Vector2.Transform(new Vector2(rect.X + rect.W, rect.Y + rect.H), layout.Transform);
        Assert.Equal(width - right, edge.X, 3);
        Assert.Equal(height - bottom, edge.Y, 3);
    }

    /// <summary>Stretch and pivot calculations honor margins and reject invalid anchors.</summary>
    [Fact]
    public void AnchorsStretchAndApplyOffsets()
    {
        var layout = new ViewportLayout(default, new Rect(0, 0, 400, 300), 1, 1, Matrix3x2.Identity);
        Assert.Equal(new Rect(10, 20, 360, 240), layout.Anchor(Vector2.Zero, Vector2.One,
            Vector2.Zero, Vector2.Zero, new ViewportInsets(10, 20, 30, 40)));
        Assert.Equal(new Rect(150, 0, 100, 300), layout.Anchor(new Vector2(0.5f, 0),
            new Vector2(0.5f, 1), new Vector2(100, 20), new Vector2(0.5f)));
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.Anchor(new Vector2(-1), Vector2.One,
            Vector2.Zero, Vector2.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.Anchor(Vector2.One, Vector2.Zero,
            Vector2.Zero, Vector2.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.Anchor(Vector2.Zero, Vector2.One,
            new Vector2(float.NaN), Vector2.Zero));
    }

    /// <summary>Invalid settings fail predictably and oversized insets collapse safely.</summary>
    [Fact]
    public void InvalidOptionsAreRejectedAndEmptySafeAreasRemainFinite()
    {
        ViewportOptions[] invalid =
        [
            Options with { Bounds = new Rect(float.NaN, 0, 1, 1) },
            Options with { Bounds = new Rect(0, 0, -1, 1) },
            Options with { ReferenceResolution = Vector2.Zero },
            Options with { MinScale = 0 }, Options with { MaxScale = 0.1f },
            Options with { UiScale = float.PositiveInfinity }, Options with { TextScale = -1 },
            Options with { DisplayScale = 0 }, Options with { ScaleMode = (ReferenceScaleMode)99 },
            Options with { AspectPolicy = (ViewportAspectPolicy)99 },
            Options with { Insets = new ViewportInsets(-1, 0, 0, 0) }
        ];
        foreach (var options in invalid) Assert.Throws<ArgumentOutOfRangeException>(() => options.Resolve());
        var layout = (Options with { Insets = new ViewportInsets(1000, 1000, 1000, 1000) }).Resolve();
        Assert.Equal(0, layout.ContentBounds.W);
        Assert.Equal(0, layout.ContentBounds.H);
        Assert.True(float.IsFinite(layout.Scale));
    }

    /// <summary>Sibling viewports isolate scales and preserve pointer mapping when preferences change.</summary>
    [Fact]
    public void SplitViewportsMapPointerAndKeepIndependentTextScalesAcrossFrames()
    {
        using var frame = new FrameHarness(800, 400);
        var userScale = 1f;
        var clicks = 0;
        LayoutNode? target = null;
        void Draw(Gui gui)
        {
            using (gui.Viewport(new ViewportOptions
            {
                Bounds = new Rect(400, 0, 400, 400),
                ReferenceResolution = new Vector2(200),
                Insets = new ViewportInsets(20, 20, 20, 20),
                UiScale = userScale,
                TextScale = 1.5f
            }, "right"))
            {
                Assert.Equal(1.5f, gui.FontScale);
                Assert.Equal(30, gui.GetTextFont(20).Size);
                target = gui.AnchoredNode(Vector2.One, Vector2.One, new Vector2(40, 30), Vector2.One,
                    id: "button");
                using (target.Enter())
                {
                    gui.DrawBackgroundRect(Color.Red);
                    if (gui.Pass == Pass.Pass2Render && target.OnClick()) clicks++;
                }
                gui.Node().WidthPercent(0.5f).HeightPercent(0.5f).AbsoluteScreen(0, 0);
            }
            Assert.Equal(gui.ScreenRect, gui.LayoutBounds);
            Assert.Equal(1, gui.FontScale);
            using (gui.Viewport(new ViewportOptions
            {
                Bounds = new Rect(0, 0, 400, 400),
                ReferenceResolution = new Vector2(200),
                TextScale = 0.8f
            }, "left")) Assert.Equal(0.8f, gui.FontScale);
        }
        frame.Frame(Draw);
        var initial = target!.LocalToScreen(target.Rect);
        Assert.Equal(780, initial.X + initial.W, 3);
        Assert.Equal(380, initial.Y + initial.H, 3);
        frame.Click(Draw, target.LocalToScreen(target.Rect.Center));
        Assert.Equal(1, clicks);
        userScale = 1.5f;
        frame.Frame(Draw);
        var enlarged = target!.LocalToScreen(target.Rect);
        Assert.True(enlarged.W > initial.W);
        Assert.Equal(780, enlarged.X + enlarged.W, 3);
    }

    /// <summary>Viewport boundaries clip pixels and input even for overlays that escape panel clips.</summary>
    [Fact]
    public void ReferenceCroppingClipsDrawingAndEscapingOverlayInput()
    {
        using var frame = new FrameHarness(400, 300);
        var hovered = false;
        void Draw(Gui gui)
        {
            using var viewport = gui.Viewport(new ViewportOptions
            {
                Bounds = new Rect(100, 50, 200, 200),
                ReferenceResolution = new Vector2(400, 200),
                ScaleMode = ReferenceScaleMode.Fill,
                AspectPolicy = ViewportAspectPolicy.PreserveReference
            });
            using var overlay = gui.Node(400, 200).AbsoluteScreen(0, 0).Enter();
            overlay.Set(new LayoutNodeScopeEscapesAncestorClips { Value = true });
            gui.DrawBackgroundRect(Color.Red);
            if (gui.Pass == Pass.Pass2Render) hovered = overlay.Node.OnHover();
        }
        frame.Input.MoveTo(new Vector2(50, 100));
        frame.Frame(Draw);
        Assert.False(hovered);
        using var image = frame.Snapshot();
        using var pixels = SKBitmap.FromImage(image);
        Assert.Equal(0, pixels.GetPixel(50, 100).Alpha);
        Assert.Equal(SKColors.Red, pixels.GetPixel(150, 100));
        frame.Input.MoveTo(new Vector2(150, 100));
        frame.Frame(Draw);
        Assert.True(hovered);
    }

    /// <summary>Docking and centered modal overlays share the safe viewport canvas.</summary>
    [Fact]
    public void DockAndModalOverlayComposeWithSafeBounds()
    {
        using var frame = new FrameHarness(800, 600);
        var dock = new DockLayout { Root = new DockLeaf("game") };
        var open = true;
        frame.Frame(gui =>
        {
            using var viewport = gui.Viewport(new ViewportOptions
            {
                Bounds = new Rect(100, 50, 600, 500),
                Insets = new ViewportInsets(20, 30, 20, 30),
                ReferenceResolution = new Vector2(280, 220)
            });
            gui.DockSpace(dock, id => new DockPanelInfo(id), (_, g) =>
                g.ModalPopup(ref open, () => g.DrawText("Menu"), width: 100, height: 60, id: "menu"),
                id: "dock");
        });
        var root = frame.Gui.RootNode!;
        var space = root.FindChildById("dock")!;
        Assert.Equal(new Rect(120, 80, 560, 440), space.LocalToScreen(space.Rect));
        var popup = root.FindChildById("menu")!;
        Assert.Equal(new Rect(300, 240, 200, 120), popup.LocalToScreen(popup.Rect));
        var overlay = root.FindChildById("menu/overlay")!;
        Assert.Equal(new Rect(120, 80, 560, 440), overlay.LocalToScreen(overlay.Rect));
    }

    /// <summary>Nested world-panel style viewports compose transforms and restore their parent's font scale.</summary>
    [Fact]
    public void NestedViewportsComposeTransformsAndDoNotLeakIntoOtherGuis()
    {
        using var frame = new FrameHarness(800, 600);
        LayoutNode? child = null;
        frame.Frame(gui =>
        {
            using var parent = gui.Viewport(new ViewportOptions
            {
                Bounds = new Rect(100, 100, 400, 400),
                ReferenceResolution = new Vector2(200),
                TextScale = 1.5f
            });
            using (gui.Viewport(new ViewportOptions
            {
                Bounds = new Rect(50, 50, 100, 100),
                ReferenceResolution = new Vector2(100),
                TextScale = 2
            }))
            {
                child = gui.Node().Expand().AbsoluteScreen(0, 0);
                Assert.Equal(2, gui.FontScale);
                Assert.Equal(30, gui.GetTextFont(20, parent).Size);
            }
            Assert.Equal(1.5f, gui.FontScale);
        });
        Assert.Equal(new Rect(200, 200, 200, 200), child!.LocalToScreen(child.Rect));
        using var other = new FrameHarness();
        other.Frame(gui =>
        {
            Assert.Equal(1, gui.FontScale);
            Assert.Equal(new Rect(0, 0, 400, 300), gui.LayoutBounds);
        });
    }
}
