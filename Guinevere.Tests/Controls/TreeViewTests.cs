using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the tree control: collapse by depth, virtualisation, and the click reporting the panels
/// build on. The virtualisation is the point — a scene tree of thousands of nodes used to build a
/// layout node per row every frame.
/// </summary>
public class TreeViewTests
{
    const int Width = 400;
    const int Height = 400;

    static readonly TreeViewTheme Theme = new() { RowHeight = 20f };

    static IReadOnlyList<TreeItem> Tree(int roots, int childrenPerRoot)
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

    static Gui RunFrames(IReadOnlyList<TreeItem> items, TreeViewState state, int frames = 1,
        IInputHandler? input = null, Action<TreeViewEvent>? onClick = null, Gui? reuse = null,
        Action<TreeItem, string>? onRename = null)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));

        input ??= OffscreenInput();
        var gui = reuse ?? new TestableGui { Input = input };
        gui.Input = input;
        if (gui is TestableGui testable) testable.SetScreenRect(Width, Height);

        for (var frame = 0; frame < frames; frame++)
        {
            void Draw() => gui.TreeView(state, items, Theme, onClick, onRename: onRename);

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

    static IInputHandler OffscreenInput()
    {
        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(-100, -100));
        input.PrevMousePosition.Returns(new Vector2(-100, -100));
        return input;
    }

    static void ScrollTo(Gui gui, float y) =>
        gui.ScrollBy(gui.RootNode!.Children[0].Id, new Vector2(0, y));

    static List<int> RowIndices(Gui gui)
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

    static int RowNodeCount(Gui gui)
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

    /// <summary>
    /// A selection made outside the tree — the inspector pinging what a reference points at — scrolls
    /// into view, which virtualisation otherwise leaves off screen entirely.
    /// </summary>
    [Fact]
    public void RevealScrollsAnOffScreenSelectionIntoView()
    {
        var items = Tree(roots: 200, childrenPerRoot: 0);
        var state = new TreeViewState { SelectedId = "root150" };

        var gui = RunFrames(items, state, frames: 2);
        Assert.DoesNotContain(150, RowIndices(gui));

        state.Reveal();
        RunFrames(items, state, frames: 2, reuse: gui);

        Assert.Contains(150, RowIndices(gui));
    }

    /// <summary>An inline rename replaces the row's label with a field, and Enter reports the new name.</summary>
    [Fact]
    public void ConfirmingAnInlineRenameReportsTheNewName()
    {
        var items = Tree(roots: 2, childrenPerRoot: 0);
        var state = new TreeViewState();
        var renamed = new List<string>();

        state.BeginRename("root0", "Root 0");
        RunFrames(items, state, frames: 2, onRename: (item, name) => renamed.Add($"{item.Id}={name}"));

        state.EditingText = "Renamed";
        RunFrames(items, state, frames: 1, input: WithKey(KeyboardKey.Enter),
            onRename: (item, name) => renamed.Add($"{item.Id}={name}"));

        Assert.Equal(["root0=Renamed"], renamed);
        Assert.Null(state.EditingId);
    }

    /// <summary>Escape abandons an inline rename without reporting anything.</summary>
    [Fact]
    public void EscapeAbandonsAnInlineRename()
    {
        var items = Tree(roots: 2, childrenPerRoot: 0);
        var state = new TreeViewState();
        var renamed = new List<string>();

        state.BeginRename("root0", "Root 0");
        RunFrames(items, state, frames: 1, onRename: (_, name) => renamed.Add(name));
        RunFrames(items, state, frames: 1, input: WithKey(KeyboardKey.Escape),
            onRename: (_, name) => renamed.Add(name));

        Assert.Empty(renamed);
        Assert.Null(state.EditingId);
    }

    static IInputHandler WithKey(KeyboardKey key)
    {
        var input = OffscreenInput();
        input.IsKeyPressed(key).Returns(true);
        return input;
    }

    [Fact]
    public void ASmallTreeDrawsEveryRow()
    {
        var state = new TreeViewState();
        state.ExpandAll();

        var gui = RunFrames(Tree(roots: 3, childrenPerRoot: 2), state);

        Assert.Equal(9, RowNodeCount(gui));
    }

    [Fact]
    public void ATreeStartsWithEveryRowCollapsed()
    {
        var gui = RunFrames(Tree(roots: 3, childrenPerRoot: 2), new TreeViewState());

        Assert.Equal(3, RowNodeCount(gui));
    }

    [Fact]
    public void CollapsingARootHidesItsChildren()
    {
        var state = new TreeViewState();
        state.ExpandAll();
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
        Assert.True(state.IsCollapsed("a", depth: 0), "an untouched row starts collapsed");

        state.Toggle("a");
        Assert.False(state.IsCollapsed("a", depth: 0));

        state.Toggle("a");
        Assert.True(state.IsCollapsed("a", depth: 0));

        state.SetExpanded("a", expanded: true);
        Assert.False(state.IsCollapsed("a", depth: 0));

        state.SetExpanded("a", expanded: false);
        Assert.True(state.IsCollapsed("a", depth: 0));

        state.ExpandAll();
        Assert.False(state.IsCollapsed("a", depth: 0));
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
        state.SetExpanded("root0", expanded: true);
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

    /// <summary>
    /// Clicks the first row so the tree owns focus, then returns the gui to keep driving. Navigation
    /// only answers the focused tree, since a window can hold several.
    /// </summary>
    static Gui Focused(IReadOnlyList<TreeItem> items, TreeViewState state)
    {
        var click = Substitute.For<IInputHandler>();
        click.MousePosition.Returns(new Vector2(200, 10));
        click.PrevMousePosition.Returns(new Vector2(200, 10));
        click.IsMouseButtonPressed(MouseButton.Left).Returns(true);

        return RunFrames(items, state, frames: 1, input: click);
    }

    static IInputHandler KeyInput(KeyboardKey key)
    {
        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(-100, -100));
        input.PrevMousePosition.Returns(new Vector2(-100, -100));
        input.IsKeyPressed(Arg.Any<KeyboardKey>()).Returns(call => call.Arg<KeyboardKey>() == key);
        return input;
    }

    [Fact]
    public void DownAndUpWalkTheVisibleRows()
    {
        var state = new TreeViewState();
        var items = Tree(roots: 3, childrenPerRoot: 0);
        var gui = Focused(items, state);
        state.SelectedId = "root0";

        RunFrames(items, state, frames: 1, input: KeyInput(KeyboardKey.Down), reuse: gui);
        Assert.Equal("root1", state.SelectedId);

        RunFrames(items, state, frames: 1, input: KeyInput(KeyboardKey.Up), reuse: gui);
        Assert.Equal("root0", state.SelectedId);
    }

    [Fact]
    public void RightOpensARowAndThenStepsIntoIt()
    {
        var state = new TreeViewState();
        var items = Tree(roots: 2, childrenPerRoot: 2);
        var gui = Focused(items, state);
        state.SelectedId = "root0";
        state.SetExpanded("root0", expanded: false);

        RunFrames(items, state, frames: 1, input: KeyInput(KeyboardKey.Right), reuse: gui);
        Assert.False(state.IsCollapsed("root0", 0));

        RunFrames(items, state, frames: 1, input: KeyInput(KeyboardKey.Right), reuse: gui);
        Assert.Equal("root0/child0", state.SelectedId);
    }

    [Fact]
    public void LeftClosesARowAndThenStepsOutToTheParent()
    {
        var state = new TreeViewState();
        var items = Tree(roots: 2, childrenPerRoot: 2);
        var gui = Focused(items, state);
        state.SelectedId = "root0";
        state.SetExpanded("root0", expanded: true);

        RunFrames(items, state, frames: 1, input: KeyInput(KeyboardKey.Left), reuse: gui);
        Assert.True(state.IsCollapsed("root0", 0));

        state.SetExpanded("root0", expanded: true);
        state.SelectedId = "root0/child1";

        RunFrames(items, state, frames: 1, input: KeyInput(KeyboardKey.Left), reuse: gui);
        Assert.Equal("root0", state.SelectedId);
    }

    [Fact]
    public void HomeAndEndJumpToTheEnds()
    {
        var state = new TreeViewState();
        var items = Tree(roots: 4, childrenPerRoot: 0);
        var gui = Focused(items, state);
        state.SelectedId = "root1";

        RunFrames(items, state, frames: 1, input: KeyInput(KeyboardKey.End), reuse: gui);
        Assert.Equal("root3", state.SelectedId);

        RunFrames(items, state, frames: 1, input: KeyInput(KeyboardKey.Home), reuse: gui);
        Assert.Equal("root0", state.SelectedId);
    }

    [Fact]
    public void EnterReportsAnActivationOnTheSelectedRow()
    {
        var state = new TreeViewState();
        var items = Tree(roots: 3, childrenPerRoot: 0);
        var gui = Focused(items, state);
        state.SelectedId = "root1";

        TreeViewEvent? seen = null;
        RunFrames(items, state, frames: 1, input: KeyInput(KeyboardKey.Enter), onClick: e => seen = e, reuse: gui);

        Assert.NotNull(seen);
        Assert.Equal("root1", seen.Value.Item.Id);
        Assert.Equal(2, seen.Value.ClickCount);
    }
}
