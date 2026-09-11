using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the tree control: collapse by depth, virtualisation, and the click reporting the panels
/// build on. The virtualisation is the point — a scene tree of thousands of nodes used to build a
/// layout node per row every frame.
/// </summary>
public class TreeViewTests
{
    private const int Width = 400;
    private const int Height = 400;

    private static readonly TreeViewTheme Theme = new() { RowHeight = 20f };

    private static IReadOnlyList<TreeItem> Tree(int roots, int childrenPerRoot)
    {
        var items = new List<TreeItem>();

        for (var r = 0; r < roots; r++)
        {
            items.Add(new TreeItem($"root{r}", $"Root {r}", 0, childrenPerRoot > 0));
            for (var c = 0; c < childrenPerRoot; c++)
                items.Add(new TreeItem($"root{r}/child{c}", $"Child {c}", 1));
        }

        return items;
    }

    private static Gui RunFrames(IReadOnlyList<TreeItem> items, TreeViewState state, int frames = 1,
        IInputHandler? input = null, Action<TreeViewEvent>? onClick = null, Gui? reuse = null)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));

        input ??= OffscreenInput();
        var gui = reuse ?? new TestableGui { Input = input };
        if (gui is TestableGui testable) testable.SetScreenRect(Width, Height);

        for (var frame = 0; frame < frames; frame++)
        {
            void Draw() => gui.TreeView(state, items, Theme, onClick);

            gui.Time.Update(0.016);
            gui.SetStage(Pass.Pass1Build);
            gui.BeginFrame(surface.Canvas);
            Draw();
            gui.CalculateLayout();
            gui.SetStage(Pass.Pass2Render);
            Draw();
            gui.Render();
            gui.EndFrame();
        }

        return gui;
    }

    private static IInputHandler OffscreenInput()
    {
        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(-100, -100));
        input.PrevMousePosition.Returns(new Vector2(-100, -100));
        return input;
    }

    private static void ScrollTo(Gui gui, float y) =>
        gui.ScrollBy(gui.RootNode!.Children[0].Id, new Vector2(0, y));

    private static List<int> RowIndices(Gui gui)
    {
        var indices = new List<int>();
        Visit(gui.RootNode!);
        return indices;

        void Visit(LayoutNode node)
        {
            var match = System.Text.RegularExpressions.Regex.Match(node.Id, @"^treeview/row(\d+)$");
            if (match.Success) indices.Add(int.Parse(match.Groups[1].Value));

            foreach (var child in node.Children) Visit(child);
        }
    }

    private static int RowNodeCount(Gui gui)
    {
        var count = 0;
        Visit(gui.RootNode!);
        return count;

        void Visit(LayoutNode node)
        {
            // Exactly the row nodes: a row's children inherit its id as a prefix.
            if (System.Text.RegularExpressions.Regex.IsMatch(node.Id, @"^treeview/row\d+$")) count++;

            foreach (var child in node.Children) Visit(child);
        }
    }

    [Fact]
    public void ASmallTreeDrawsEveryRow()
    {
        var gui = RunFrames(Tree(roots: 3, childrenPerRoot: 2), new TreeViewState());

        Assert.Equal(9, RowNodeCount(gui));
    }

    [Fact]
    public void CollapsingARootHidesItsChildren()
    {
        var state = new TreeViewState();
        state.SetExpanded("root0", expanded: false);

        var gui = RunFrames(Tree(roots: 3, childrenPerRoot: 2), state);

        Assert.Equal(7, RowNodeCount(gui));
    }

    [Fact]
    public void ALargeTreeOnlyBuildsNodesForTheRowsOnScreen()
    {
        // 20 000 rows through a 400px viewport: without virtualisation this is 20 000 layout nodes a
        // frame, which is what dropped a big scene to a crawl.
        var gui = RunFrames(Tree(roots: 2000, childrenPerRoot: 9), new TreeViewState(), frames: 2);

        var rows = RowNodeCount(gui);
        Assert.InRange(rows, 1, 40);
    }

    [Fact]
    public void TheSpacersKeepTheScrollExtentOfTheWholeTree()
    {
        var gui = RunFrames(Tree(roots: 100, childrenPerRoot: 0), new TreeViewState(), frames: 2);

        var bottom = gui.RootNode!.FindChildById("treeview/padBottom");
        Assert.NotNull(bottom);

        // 100 rows of 20px, minus whatever the viewport already shows.
        Assert.True(bottom.Rect.H > 0, "the tree below the viewport must still take space");
    }

    [Fact]
    public void ClickingARowSelectsItAndReportsTheClick()
    {
        var state = new TreeViewState();
        var items = Tree(roots: 3, childrenPerRoot: 0);

        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(200, 30));
        input.PrevMousePosition.Returns(new Vector2(200, 30));
        input.IsMouseButtonPressed(MouseButton.Left).Returns(true);

        TreeViewEvent? seen = null;
        RunFrames(items, state, frames: 1, input: input, onClick: e => seen = e);

        Assert.NotNull(seen);
        Assert.Equal(MouseButton.Left, seen.Value.Button);
        Assert.Equal(seen.Value.Item.Id, state.SelectedId);
    }

    [Fact]
    public void AnItemCarriesItsTagBackToTheCaller()
    {
        var payload = new object();
        var items = new List<TreeItem> { new("only", "Only", 0, Tag: payload) };

        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(200, 10));
        input.PrevMousePosition.Returns(new Vector2(200, 10));
        input.IsMouseButtonPressed(MouseButton.Left).Returns(true);

        TreeViewEvent? seen = null;
        RunFrames(items, new TreeViewState(), frames: 1, input: input, onClick: e => seen = e);

        Assert.NotNull(seen);
        Assert.Same(payload, seen.Value.Item.Tag);
    }

    [Fact]
    public void ToggleFlipsAndSetExpandedIsExplicit()
    {
        var state = new TreeViewState();
        Assert.False(state.IsCollapsed("a"));

        state.Toggle("a");
        Assert.True(state.IsCollapsed("a"));

        state.Toggle("a");
        Assert.False(state.IsCollapsed("a"));

        state.SetExpanded("a", expanded: false);
        Assert.True(state.IsCollapsed("a"));

        state.ExpandAll();
        Assert.False(state.IsCollapsed("a"));
    }

    [Fact]
    public void ScrollingKeepsTheVisibleRowsBuilt()
    {
        // The rows a scrolled viewport shows must exist. Reading the viewport in the build pass, where
        // a freshly created node has no rect yet, left the window stuck at the top and the rest blank.
        var state = new TreeViewState();
        var items = Tree(roots: 500, childrenPerRoot: 0);

        var gui = RunFrames(items, state, frames: 1);
        ScrollTo(gui, 2000f);

        // One frame to measure the new offset, one to virtualise against it.
        gui = RunFrames(items, state, frames: 2, reuse: gui);

        var rows = RowIndices(gui);
        Assert.NotEmpty(rows);

        // 2000px down at 20px a row is row 100; the built window must bracket it.
        Assert.True(rows.Min() <= 100, $"window starts at {rows.Min()}, too late for a scroll to row 100");
        Assert.True(rows.Max() >= 100, $"window ends at {rows.Max()}, too early for a scroll to row 100");
    }

    [Fact]
    public void ADoubleClickOnTheLabelFoldsTheRow()
    {
        var state = new TreeViewState();
        var items = Tree(roots: 2, childrenPerRoot: 2);

        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(200, 10));
        input.PrevMousePosition.Returns(new Vector2(200, 10));
        input.IsMouseButtonPressed(MouseButton.Left).Returns(true);

        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
        var gui = new TestableGui { Input = input };
        gui.SetScreenRect(Width, Height);

        for (var frame = 0; frame < 2; frame++)
        {
            void Draw() => gui.TreeView(state, items, Theme);

            gui.Time.Update(0.016);
            gui.SetStage(Pass.Pass1Build);
            gui.BeginFrame(surface.Canvas);
            Draw();
            gui.CalculateLayout();
            gui.SetStage(Pass.Pass2Render);
            Draw();
            gui.EndFrame();
        }

        Assert.True(state.IsCollapsed("root0"), "a second click on the label should fold the row");
    }
}
