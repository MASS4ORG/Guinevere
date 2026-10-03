using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Cascade travel tolerance, nested keyboard navigation and compact menu presentation.</summary>
public class MenuNavigationTests
{
    static IEnumerable<LayoutNode> Groups(LayoutNode node)
    {
        if (node.Id.StartsWith("/menubar/", StringComparison.Ordinal) && node.Id.Split('/')[^1].StartsWith('v'))
            yield return node;
        foreach (var child in node.Children)
            foreach (var group in Groups(child)) yield return group;
    }

    static LayoutNode Group(FrameHarness h, int depth) =>
        Groups(h.Gui.RootNode!).Single(node => node.Id.EndsWith($"/v{depth}", StringComparison.Ordinal));

    static void AssertMeasured(LayoutNode node)
    {
        Assert.True(node.Rect.W > 0 && node.Rect.H > 0, node.Id);
        foreach (var child in node.Children)
            if (!child.Id.Contains("/node_", StringComparison.Ordinal)) AssertMeasured(child);
    }

    /// <summary>Closes an empty cascade instead of leaving an invisible open menu.</summary>
    [Fact]
    public void EmptyCascadeClosesWithoutCreatingMenuNodes()
    {
        using var h = new FrameHarness();
        var open = true;
        h.Frame(gui => gui.CascadeMenu(ref open, new Vector2(20, 20), _ => { }));
        Assert.False(open);
        Assert.Empty(Groups(h.Gui.RootNode!));
    }

    /// <summary>Checks submenu retention during travel and eventual hover switching.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiagonalTravelPreservesTheWholeCascadeAndSwitchesAfterGrace(bool flyout)
    {
        using var h = new FrameHarness(width: 700);
        var open = true;
        void Build(FlyoutBuilder menu) => menu
            .Submenu("First", first => first.Submenu("Second", second => second.Item("Leaf")))
            .Item("Neighbor");
        void Draw(Gui gui)
        {
            if (flyout) gui.Flyout(ref open, new Vector2(20, 20), Build);
            else gui.CascadeMenu(ref open, new Vector2(20, 20), Build);
        }
        h.Frame(Draw);
        h.Input.MoveTo(Group(h, 0).Children[0].Center);
        h.Frame(Draw);
        h.Input.MoveTo(Group(h, 1).Children[0].Center);
        h.Frame(Draw);
        var leaf = Group(h, 2).Children[0].Center;
        h.Input.MoveTo(leaf);
        h.Frame(Draw);
        h.Input.MoveTo(Group(h, 0).Children[1].Center);
        h.Frame(Draw);
        Assert.Equal(3, Groups(h.Gui.RootNode!).Count());
        h.Input.MoveTo(leaf);
        h.Frame(Draw);
        Assert.Equal(3, Groups(h.Gui.RootNode!).Count());
        h.Input.MoveTo(Group(h, 0).Children[1].Center);
        for (var i = 0; i < 22; i++) h.Frame(Draw);
        Assert.Single(Groups(h.Gui.RootNode!));
        Assert.True(open);
    }

    /// <summary>Checks left-opening submenu geometry and pointer retention.</summary>
    [Fact]
    public void SubmenuFlipsLeftAtTheScreenEdgeAndStaysAttached()
    {
        using var h = new FrameHarness(width: 500);
        var open = true;
        void Draw(Gui gui) => gui.CascadeMenu(ref open, new Vector2(330, 20),
            menu => menu.Submenu("More", child => child.Item("Nested")));
        h.Frame(Draw);
        h.Input.MoveTo(Group(h, 0).Children[0].Center);
        h.Frame(Draw);
        var parent = Group(h, 0).Rect;
        var child = Group(h, 1).Rect;
        Assert.Equal(parent.X, child.X + child.W, 2);
        h.Input.MoveTo(Group(h, 1).Children[0].Center);
        h.Frame(Draw);
        Assert.Equal(2, Groups(h.Gui.RootNode!).Count());
    }

    /// <summary>Checks nested keyboard navigation and leaf activation.</summary>
    [Fact]
    public void KeyboardEntersThreeLevelsSkipsDisabledRowsAndActivatesTheLeaf()
    {
        using var h = new FrameHarness(width: 700);
        var open = true;
        var runs = 0;
        void Draw(Gui gui) => gui.CascadeMenu(ref open, new Vector2(20, 20), menu => menu
            .Item("Off", enabled: false).Separator()
            .Submenu("First", first => first.Submenu("Second", second => second.Item("Run", () => runs++))));
        void Key(KeyboardKey key)
        {
            h.Input.PressKey(key);
            h.Frame(Draw);
            h.Input.ReleaseKey(key);
            h.Frame(Draw);
        }
        h.Frame(Draw);
        Key(KeyboardKey.Down);
        Key(KeyboardKey.Right);
        Assert.Equal(2, Groups(h.Gui.RootNode!).Count());
        Key(KeyboardKey.Right);
        Assert.Equal(3, Groups(h.Gui.RootNode!).Count());
        Key(KeyboardKey.Left);
        Assert.Equal(2, Groups(h.Gui.RootNode!).Count());
        Key(KeyboardKey.Right);
        Key(KeyboardKey.Enter);
        Assert.Equal(1, runs);
        Assert.False(open);
    }

    /// <summary>Checks the compact toggle and title restoration after dismissal.</summary>
    [Theory]
    [InlineData("☰")]
    [InlineData("Zed")]
    public void CompactToggleRevealsTitlesAndDismissalRestoresItsSmallWidth(string label)
    {
        using var h = new FrameHarness();
        void Draw(Gui gui) => gui.MenuBar(bar => bar.Collapsible(label)
            .Menu("File", menu => menu.Item("Open"))
            .Menu("Edit", menu => menu.Item("Copy")));
        h.Frame(Draw);
        var collapsed = h.Gui.RootNode!.Children[0];
        Assert.Equal(30, collapsed.Rect.W);
        h.Click(Draw, collapsed.Center);
        Assert.NotEmpty(Groups(h.Gui.RootNode!));
        h.Frame(Draw);
        Assert.True(h.Gui.RootNode!.Children[0].Rect.W > 30);
        Assert.NotEmpty(Groups(h.Gui.RootNode!));
        AssertMeasured(Group(h, 0));
        h.Input.PressKey(KeyboardKey.Escape);
        h.Frame(Draw);
        h.Input.ReleaseKey(KeyboardKey.Escape);
        h.Frame(Draw);
        Assert.Equal(30, h.Gui.RootNode!.Children[0].Rect.W);
        Assert.Empty(Groups(h.Gui.RootNode!));
    }

    /// <summary>Expanded compact menus anchor their dropdown under the selected title.</summary>
    [Fact]
    public void ExpandedCompactMenuAnchorsUnderTheSelectedTitle()
    {
        using var h = new FrameHarness(width: 600);
        void Draw(Gui gui) => gui.MenuBar(bar => bar.Collapsible()
            .Menu("File", menu => menu.Item("Open"))
            .Menu("Edit", menu => menu.Item("Copy"))
            .Menu("Help", menu => menu.Item("About")));
        h.Frame(Draw);
        h.Click(Draw, h.Gui.RootNode!.Children[0].Center);
        h.Frame(Draw);
        var help = h.Gui.RootNode!.Children[0].Children[2];
        h.Input.MoveTo(help.Center);
        h.Frame(Draw);
        h.Frame(Draw);
        Assert.Equal(help.Rect.X, Group(h, 0).Rect.X, 2);
        Assert.Equal(help.Rect.Y + help.Rect.H, Group(h, 0).Rect.Y, 2);
    }

    /// <summary>Checks that the default toggle remains visible without a hamburger font glyph.</summary>
    [Fact]
    public void CompactHamburgerRendersAboveItsButtonBackground()
    {
        using var surface = SKSurface.Create(new SKImageInfo(60, 60));
        var gui = new TestableGui { ControlPalette = ControlPalette.Dark, Input = new ScriptedInputHandler() };
        gui.SetScreenRect(60, 60);
        void Draw() => gui.MenuBar(bar => bar.Collapsible().Menu("File", menu => menu.Item("Open")));
        surface.Canvas.Clear(SKColors.Black);
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        Draw();
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        Draw();
        gui.Render();
        gui.EndFrame();
        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        Assert.True(bitmap.GetPixel(15, 15).Red > 100);
    }

    /// <summary>Checks that Escape preserves the frame snapshot through rendering.</summary>
    [Fact]
    public void EscapeKeepsTheSameCascadeNodesInBothPasses()
    {
        using var h = new FrameHarness(width: 700);
        var open = true;
        string[] built = [];
        void Draw(Gui gui)
        {
            gui.CascadeMenu(ref open, new Vector2(20, 20),
                menu => menu.Submenu("More", child => child.Item("Nested")));
            var ids = Groups(gui.RootNode!).Select(node => node.Id).ToArray();
            if (gui.Pass == Pass.Pass1Build) built = ids;
            else Assert.Equal(built, ids);
        }
        h.Frame(Draw);
        h.Input.MoveTo(Group(h, 0).Children[0].Center);
        h.Frame(Draw);
        h.Input.PressKey(KeyboardKey.Escape);
        h.Frame(Draw);
        Assert.False(open);
    }

    /// <summary>Checks that check marks are measured before they render.</summary>
    [Fact]
    public void TogglingACheckItemKeepsTheSameNodesInBothPasses()
    {
        using var h = new FrameHarness();
        var open = true;
        var value = false;
        string[] built = [];
        static IEnumerable<string> Ids(LayoutNode node)
        {
            yield return node.Id;
            foreach (var child in node.Children)
                foreach (var id in Ids(child)) yield return id;
        }
        void Draw(Gui gui)
        {
            gui.CascadeMenu(ref open, new Vector2(20, 20),
                menu => menu.CheckItem("Mode", () => value, next => value = next));
            var ids = Ids(gui.RootNode!).ToArray();
            if (gui.Pass == Pass.Pass1Build) built = ids;
            else Assert.Equal(built, ids);
        }
        h.Frame(Draw);
        h.Click(Draw, Group(h, 0).Children[0].Center);
        Assert.True(value);
    }
}
