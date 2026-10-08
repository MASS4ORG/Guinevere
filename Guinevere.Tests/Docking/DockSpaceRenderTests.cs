using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Docking;

/// <summary>
/// Checks that a dock tree turns into the rects it describes: split fractions divide the space,
/// the splitter takes its thickness out of the middle, and only the active tab's body is drawn.
/// </summary>
public class DockSpaceRenderTests
{
    /// <summary>A drop on a sibling tab reorders without creating a split or floating window.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DraggingSiblingTabsReorders(bool reverse)
    {
        using var h = new FrameHarness();
        var leaf = new DockLeaf("scene", "game", "output");
        var layout = new DockLayout { Root = leaf };
        void Draw(Gui gui) => gui.DockSpace(layout, id => new DockPanelInfo(id), (_, _) => { }, Theme);
        h.Frame(Draw);
        var source = h.Gui.RootNode!.FindChildById($"dock:root/tabs/{(reverse ? "output" : "scene")}")!;
        var target = h.Gui.RootNode.FindChildById($"dock:root/tabs/{(reverse ? "scene" : "output")}")!;
        h.Input.MoveTo(source.Center);
        h.Input.PressButton(MouseButton.Left);
        h.Frame(Draw);
        h.Input.MoveTo(target.Center);
        h.Frame(Draw);
        h.Frame(Draw);
        var markerX = reverse ? target.Rect.X : target.Rect.X + target.Rect.W - 2;
        using (var snapshot = h.Snapshot())
        using (var bitmap = SKBitmap.FromImage(snapshot))
            Assert.Equal((SKColor)h.Gui.StyleSheets.GetTokenColor("accent")!.Value, bitmap.GetPixel((int)Math.Ceiling(markerX),
                (int)target.Rect.Y + 4));
        h.Input.ReleaseButton(MouseButton.Left);
        h.Frame(Draw);
        h.Frame(Draw);
        Assert.Equal(reverse ? ["output", "scene", "game"] : new[] { "game", "output", "scene" }, leaf.PanelIds);
        Assert.Same(leaf, layout.Root);
        Assert.Empty(layout.Floating);
        Assert.Equal(reverse ? "output" : "scene", leaf.ActivePanelId);
    }

    /// <summary>The close hover stays inside the tab and leaves its active border visible.</summary>
    [Fact]
    public void CloseButtonIsInsetFromTabBorders()
    {
        var gui = RenderDock(new DockLayout { Root = new DockLeaf("scene") }, out _);
        var tab = RectOf(gui, "dock:root/tabs/scene");
        var close = RectOf(gui, "dock:root/tabs/scene/close");
        Assert.True(close.Y >= tab.Y + 4);
        Assert.True(close.Y + close.H <= tab.Y + tab.H - 4);
    }

    /// <summary>Modified tabs keep their status dot and close through the inset hover target.</summary>
    [Fact]
    public void ModifiedTabCanCloseFromItsInsetButton()
    {
        using var h = new FrameHarness();
        TabStripResult result = default;
        void Draw(Gui gui) => result = gui.TabStrip(
            [new TabStripItem("scene", "Scene", Modified: true)], "scene");
        h.Frame(Draw);
        var close = h.Gui.RootNode!.FindChildById("tabstrip/scene/close")!;
        h.Input.MoveTo(close.Center);
        h.Frame(Draw);
        h.Input.PressButton();
        h.Frame(Draw);
        Assert.Equal("scene", result.Closed?.Id);
        Assert.Null(result.Activated);
    }

    const float SplitterThickness = 6f;

    static readonly DockTheme Theme = new() { SplitterThickness = SplitterThickness, TabHeight = 20f };

    static Gui RenderDock(DockLayout layout, out List<string> rendered, int width = 400, int height = 300,
        Func<string, DockPanelInfo?>? panelInfo = null, Action<DockTabStrip, Gui>? tabStripActions = null)
    {
        var painted = new List<string>();
        using var surface = SKSurface.Create(new SKImageInfo(width, height));

        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(-1, -1));
        input.PrevMousePosition.Returns(new Vector2(-1, -1));

        var gui = new TestableGui { Input = input };
        gui.SetScreenRect(width, height);

        void Draw() => gui.DockSpace(layout,
            panelInfo ?? (id => new DockPanelInfo(id)),
            (id, g) =>
            {
                if (g.Pass == Pass.Pass2Render) painted.Add(id);
            },
            Theme,
            tabStripActions);

        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas, Font.FromFamilyName("sans-serif"), Font.FromFamilyName("sans-serif"));
        Draw();
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        Draw();
        gui.Render();
        gui.EndFrame();

        rendered = painted;
        return gui;
    }

    static Rect RectOf(Gui gui, string id) =>
        (gui.RootNode!.FindChildById(id) ?? throw new InvalidOperationException($"no node '{id}'")).Rect;

    [Fact]
    public void AHorizontalSplitDividesTheWidthByItsFractionMinusTheSplitter()
    {
        var layout = new DockLayout
        {
            Root = new DockSplit(Axis.Horizontal, new DockLeaf("tree"), new DockLeaf("scene"), 0.25f)
        };

        var gui = RenderDock(layout, out _);

        var left = RectOf(gui, "dock:root/a");
        var right = RectOf(gui, "dock:root/b");
        var available = 400f - SplitterThickness;

        Assert.Equal(available * 0.25f, left.W, 1);
        Assert.Equal(available * 0.75f, right.W, 1);
        Assert.Equal(left.X + left.W + SplitterThickness, right.X, 1);
        Assert.Equal(300f, left.H, 1);
    }

    [Fact]
    public void AVerticalSplitDividesTheHeight()
    {
        var layout = new DockLayout
        {
            Root = new DockSplit(Axis.Vertical, new DockLeaf("scene"), new DockLeaf("output"), 0.7f)
        };

        var gui = RenderDock(layout, out _);

        var top = RectOf(gui, "dock:root/a");
        var bottom = RectOf(gui, "dock:root/b");
        var available = 300f - SplitterThickness;

        Assert.Equal(available * 0.7f, top.H, 1);
        Assert.Equal(available * 0.3f, bottom.H, 1);
        Assert.Equal(400f, top.W, 1);
    }

    [Fact]
    public void OnlyTheActiveTabOfAGroupHasItsBodyRendered()
    {
        var layout = new DockLayout { Root = new DockLeaf("scene", "game") { ActiveIndex = 1 } };

        RenderDock(layout, out var rendered);

        Assert.Equal(["game"], rendered);
    }

    [Fact]
    public void EveryVisibleGroupRendersItsOwnActivePanel()
    {
        var layout = new DockLayout
        {
            Root = new DockSplit(Axis.Horizontal, new DockLeaf("tree"), new DockLeaf("scene", "game"))
        };

        RenderDock(layout, out var rendered);

        Assert.Equal(["tree", "scene"], rendered);
    }

    [Fact]
    public void AFloatingWindowIsPlacedAtItsOwnBoundsAndRendersItsPanel()
    {
        var layout = new DockLayout { Root = new DockLeaf("scene") };
        layout.Float("inspector", new Rect(120, 60, 200, 150));

        var gui = RenderDock(layout, out var rendered);

        var window = RectOf(gui, "dock:float/0");
        Assert.Equal(new Rect(120, 60, 200, 150), window);
        Assert.Contains("inspector", rendered);
    }

    [Fact]
    public void ATabStripTakesItsHeightFromTheThemeAndTheBodyTakesTheRest()
    {
        var layout = new DockLayout { Root = new DockLeaf("scene") };

        var gui = RenderDock(layout, out _);

        Assert.Equal(Theme.TabHeight, RectOf(gui, "dock:root/tabs").H, 1);
        Assert.Equal(300f - Theme.TabHeight, RectOf(gui, "dock:root/body").H, 1);
    }

    [Fact]
    public void AnEmptyLayoutRendersAPlaceholderInsteadOfThrowing()
    {
        var gui = RenderDock(new DockLayout(), out var rendered);

        Assert.Empty(rendered);
        Assert.NotNull(gui.RootNode!.FindChildById("dock:empty"));
    }

    [Fact]
    public void ATabWithAnIconReservesTheThemesIconBoxAndDrawsItOncePerPass()
    {
        var layout = new DockLayout { Root = new DockLeaf("scene") };
        var drawn = 0;

        var gui = RenderDock(layout, out _, panelInfo: _ =>
            new DockPanelInfo("Scene", Icon: g =>
            {
                drawn++;
                g.DrawBackgroundRect(Color.Red);
            }));

        var icon = gui.RootNode!.FindChildById("dock:root/tabs/scene/icon");
        Assert.NotNull(icon);
        Assert.Equal(Theme.TabIconSize, icon.Rect.W, 1);
        Assert.Equal(Theme.TabIconSize, icon.Rect.H, 1);

        // Built in both passes: a node that only exists in the render pass never gets a rect.
        Assert.Equal(2, drawn);
    }

    [Fact]
    public void AnIconWidensTheTabItSitsOn()
    {
        var withIcon = new DockLayout { Root = new DockLeaf("scene") };
        var plain = new DockLayout { Root = new DockLeaf("scene") };

        var iconGui = RenderDock(withIcon, out _,
            panelInfo: _ => new DockPanelInfo("Scene", Icon: g => g.DrawBackgroundRect(Color.Red)));
        var plainGui = RenderDock(plain, out _, panelInfo: _ => new DockPanelInfo("Scene"));

        var iconWidth = RectOf(iconGui, "dock:root/tabs/scene").W;
        var plainWidth = RectOf(plainGui, "dock:root/tabs/scene").W;

        Assert.True(iconWidth > plainWidth, $"icon tab {iconWidth} should be wider than plain {plainWidth}");
    }

    [Fact]
    public void TabStripActionsGetTheGroupTheActivePanelAndTheLeftoverWidth()
    {
        var layout = new DockLayout { Root = new DockLeaf("scene", "game") { ActiveIndex = 1 } };
        DockTabStrip captured = default;

        var gui = RenderDock(layout, out _, tabStripActions: (strip, g) =>
        {
            if (g.Pass == Pass.Pass2Render) captured = strip;
        });

        Assert.Same(layout.FindLeaf("game"), captured.Leaf);
        Assert.Equal("game", captured.ActivePanelId);

        var strip = RectOf(gui, "dock:root/tabs");
        var tabs = RectOf(gui, "dock:root/tabs/scene").W + RectOf(gui, "dock:root/tabs/game").W;
        Assert.Equal(strip.W - tabs, captured.FreeArea.W, 1);
        Assert.Equal(strip.X + tabs, captured.FreeArea.X, 1);
    }

    [Fact]
    public void TabStripActionChildrenSitAtTheRightEdgeOfTheStrip()
    {
        var layout = new DockLayout { Root = new DockLeaf("scene") };

        var gui = RenderDock(layout, out _, tabStripActions: (_, g) =>
        {
            using (g.Node(20, 20, "dock:test/menuButton").Enter())
            {
            }
        });

        var strip = RectOf(gui, "dock:root/tabs");
        var button = RectOf(gui, "dock:test/menuButton");

        Assert.Equal(strip.X + strip.W, button.X + button.W, 1);
    }

    [Fact]
    public void TheTabStripHasItsActionRegionEvenWithNoCallback()
    {
        // The strip's structure must not change when a host adds or drops a callback.
        var gui = RenderDock(new DockLayout { Root = new DockLeaf("scene") }, out _);

        Assert.NotNull(gui.RootNode!.FindChildById("dock:root/tabs/actions"));
    }
}
