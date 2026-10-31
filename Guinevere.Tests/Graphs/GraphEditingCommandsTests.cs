using Example02;
using Guinevere.Graphs;
using Guinevere.Tests.Mocks;
using MASS4.Graph;
using GraphPoint = Guinevere.Graphs.GraphPoint;

namespace Guinevere.Tests.Graphs;

public sealed class GraphEditingCommandsTests
{
    [Theory]
    [InlineData(GraphAlignment.Left)]
    [InlineData(GraphAlignment.Center)]
    [InlineData(GraphAlignment.Right)]
    [InlineData(GraphAlignment.Top)]
    [InlineData(GraphAlignment.Middle)]
    [InlineData(GraphAlignment.Bottom)]
    public void AlignmentIsUndoableAndDoesNotInvalidateTheDocument(GraphAlignment alignment)
    {
        var scene = new SandboxScene();
        var view = scene.View;
        var before = view.Nodes.ToDictionary(static node => node.Key, static node => node.Bounds);
        var changes = 0;
        view.Editor.Document.Changed += (_, _) => changes++;
        view.SelectAll();
        view.AlignSelection(alignment);
        Assert.Equal(0, changes);
        var coordinates = view.Nodes.Select(node => alignment switch
        {
            GraphAlignment.Left => node.Bounds.X,
            GraphAlignment.Right => node.Bounds.Right,
            GraphAlignment.Top => node.Bounds.Y,
            GraphAlignment.Bottom => node.Bounds.Bottom,
            GraphAlignment.Center => node.Bounds.Center.X,
            _ => node.Bounds.Center.Y
        }).Distinct().ToArray();
        Assert.Single(coordinates);
        view.Undo();
        view.Refresh();
        Assert.All(view.Nodes, node => Assert.Equal(before[node.Key], node.Bounds));
        view.Redo();
        Assert.Equal(0, changes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DistributionAndSnappingKeepHistoryAndValidateTheGrid(bool horizontal)
    {
        var scene = new SandboxScene();
        var view = scene.View;
        var ids = view.Editor.Document.Nodes.Select(static node => node.Id).ToArray();
        for (var i = 0; i < ids.Length; i++)
        {
            view.SetPosition(ids[i], new GraphPoint(i * i * 300 + 3, i * i * 240 + 5));
        }
        view.Refresh();
        view.SelectAll();
        var before = view.SerializeLayout();
        view.DistributeSelection(horizontal);
        var cards = view.Nodes.OrderBy(node => horizontal ? node.Bounds.X : node.Bounds.Y).ToArray();
        var gaps = cards.Zip(cards.Skip(1), (a, b) => horizontal ? b.Bounds.X - a.Bounds.Right : b.Bounds.Y - a.Bounds.Bottom);
        Assert.All(gaps, gap => Assert.Equal(gaps.First(), gap, 5));
        view.Undo();
        Assert.Equal(before, view.SerializeLayout());
        view.SnapSelection();
        Assert.All(ids, id =>
        {
            Assert.Equal(0, view.Position(id).X % view.GridSize);
            Assert.Equal(0, view.Position(id).Y % view.GridSize);
        });
        view.Undo();
        Assert.Equal(before, view.SerializeLayout());
        view.GridSize = 0;
        Assert.Throws<ArgumentOutOfRangeException>(view.SnapSelection);
        view.GridSize = double.NaN;
        Assert.Throws<ArgumentOutOfRangeException>(view.SnapSelection);
        view.Canvas.Navigation.ClearSelection();
        view.AlignSelection(GraphAlignment.Left);
        view.DistributeSelection(true);
        Assert.False(view.DeleteSelection().Succeeded);
    }

    [Fact]
    public void RectangleSelectionIncludesCrossingLinksAndSupportsUnionAndToggle()
    {
        var scene = new SandboxScene();
        var view = scene.View;
        var first = view.Nodes[0];
        view.SelectRectangle(first.Bounds);
        Assert.Contains(first.Key, scene.Canvas.Navigation.SelectedKeys);
        var second = view.Nodes[1];
        view.SelectRectangle(second.Bounds, additive: true);
        Assert.Contains(second.Key, scene.Canvas.Navigation.SelectedKeys);
        view.SelectRectangle(second.Bounds, toggle: true);
        Assert.DoesNotContain(second.Key, scene.Canvas.Navigation.SelectedKeys);
        var edge = view.Editor.Document.Edges.First();
        var point = view.Connector(edge.Id).Point(0.5);
        view.SelectRectangle(new GraphBounds(point.X - 2, point.Y - 2, 4, 4));
        Assert.Contains(edge.Id.ToString(), scene.Canvas.Navigation.SelectedKeys);
        view.SelectRectangle(new GraphBounds(50_000, 50_000, 1, 1));
        Assert.Empty(scene.Canvas.Navigation.SelectedKeys);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(2)]
    public void MarqueeAndDuplicateDragUseCapturedWorldCoordinatesAndSingleUndo(double zoom)
    {
        using var harness = new FrameHarness(1400, 1000);
        var scene = new SandboxScene();
        scene.Canvas.ShowMinimap = false;
        scene.Canvas.Camera.SetView(default, zoom);
        void Draw(Gui gui) => scene.View.Draw(gui, "edit-canvas");
        harness.Frame(Draw);
        harness.Frame(Draw);
        Drag(harness, Draw, scene.Canvas.WorldToScreen(new GraphPoint(20, 20)),
            scene.Canvas.WorldToScreen(new GraphPoint(290, 370)));
        Assert.Contains(scene.Nodes[0].Key, scene.Canvas.Navigation.SelectedKeys);
        Assert.Contains(scene.Nodes[1].Key, scene.Canvas.Navigation.SelectedKeys);
        scene.Canvas.Navigation.Select(scene.Nodes[0].Key);
        var original = scene.Graph.Editor.Document.Nodes.First().Id;
        var before = scene.View.Position(original);
        harness.Input.PressKey(KeyboardKey.LeftAlt);
        var start = scene.Canvas.WorldToScreen(scene.Nodes[0].Bounds.Center);
        Drag(harness, Draw, start, start + new Vector2(80, 40));
        harness.Input.ReleaseKey(KeyboardKey.LeftAlt);
        harness.Frame(Draw);
        Assert.Equal(5, scene.Graph.Editor.Document.Nodes.Count);
        var copy = ElementId.Parse(Assert.Single(scene.Canvas.Navigation.SelectedKeys));
        Assert.Equal(new GraphPoint(before.X + 80 / zoom, before.Y + 40 / zoom), scene.View.Position(copy));
        Assert.Equal(before, scene.View.Position(original));
        Assert.True(scene.View.Undo().Succeeded);
        Assert.Equal(4, scene.Graph.Editor.Document.Nodes.Count);
        Assert.Equal([original.ToString()], scene.Canvas.Navigation.SelectedKeys);
        Assert.False(scene.View.Editor.CanUndo);
        Assert.True(scene.View.Redo().Succeeded);
        Assert.Equal([copy.ToString()], scene.Canvas.Navigation.SelectedKeys);
    }

    [Fact]
    public void KeyboardClipboardAndContextActionsKeepTopologyAndSelection()
    {
        using var harness = new FrameHarness(1100, 800);
        var scene = new SandboxScene();
        void Draw(Gui gui) => scene.View.Draw(gui, "edit-canvas");
        harness.Frame(Draw);
        harness.Frame(Draw);
        harness.Gui.Focus.RequestFocus("edit-canvas");
        harness.Frame(Draw);
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.A, KeyboardKey.LeftControl);
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.C, KeyboardKey.LeftControl);
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.V, KeyboardKey.LeftControl);
        Assert.Equal(8, scene.View.Editor.Document.Nodes.Count);
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.X, KeyboardKey.LeftControl);
        Assert.Equal(4, scene.View.Editor.Document.Nodes.Count);
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.Z, KeyboardKey.LeftControl);
        Assert.Equal(8, scene.View.Editor.Document.Nodes.Count);
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.Delete);
        Assert.Equal(4, scene.View.Editor.Document.Nodes.Count);
        GraphPoint? context = null;
        scene.View.ContextRequested = p => context = p;
        harness.Click(Draw, scene.Canvas.WorldToScreen(scene.Nodes[0].Bounds.Center), MouseButton.Right);
        Assert.NotNull(context);
        Assert.Single(scene.Canvas.Navigation.SelectedKeys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EscapeCancelsMoveAndDuplicatePreviewsWithoutHistory(bool duplicate)
    {
        using var harness = new FrameHarness(1100, 800);
        var scene = new SandboxScene();
        void Draw(Gui gui) => scene.View.Draw(gui, "edit-canvas");
        harness.Frame(Draw);
        harness.Frame(Draw);
        var id = scene.Graph.Editor.Document.Nodes.First().Id;
        var before = scene.View.Position(id);
        var start = scene.Canvas.WorldToScreen(scene.Nodes[0].Bounds.Center);
        if (duplicate)
        {
            harness.Input.PressKey(KeyboardKey.LeftAlt);
        }
        harness.Input.MoveTo(start);
        harness.Input.PressButton();
        harness.Frame(Draw);
        harness.Input.MoveTo(start + new Vector2(80, 40));
        harness.Frame(Draw);
        Assert.Equal(4, scene.View.Editor.Document.Nodes.Count);
        Assert.NotEqual(before, scene.View.Position(id));
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.Escape);
        harness.Input.MoveTo(start + new Vector2(100, 60));
        harness.Frame(Draw);
        harness.Input.ReleaseButton();
        harness.Input.ReleaseKey(KeyboardKey.LeftAlt);
        harness.Frame(Draw);
        Assert.Equal(before, scene.View.Position(id));
        Assert.False(scene.View.Editor.CanUndo);
        Assert.False(scene.View.Editor.CanRedo);
    }

    [Fact]
    public void EscapeCancelsMarqueeAndMissingClipboardReportsDiagnostics()
    {
        using var harness = new FrameHarness(1100, 800);
        var scene = new SandboxScene();
        void Draw(Gui gui) => scene.View.Draw(gui, "edit-canvas");
        harness.Frame(Draw);
        harness.Frame(Draw);
        harness.Input.MoveTo(scene.Canvas.WorldToScreen(new GraphPoint(10, 10)));
        harness.Input.PressButton();
        harness.Frame(Draw);
        harness.Input.MoveTo(scene.Canvas.WorldToScreen(new GraphPoint(300, 400)));
        harness.Frame(Draw);
        Assert.NotEmpty(scene.Canvas.Navigation.SelectedKeys);
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.Escape);
        harness.Input.ReleaseButton();
        harness.Frame(Draw);
        Assert.Empty(scene.Canvas.Navigation.SelectedKeys);
        harness.Gui.Platform.Remove<IClipboard>();
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.C, KeyboardKey.LeftControl);
        Assert.Contains(scene.View.Diagnostics, static d => d.Id == "GVG1001");
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.F10, KeyboardKey.LeftControl);
        Assert.Equal(4, scene.View.Editor.Document.Nodes.Count);
    }

    [Fact]
    public void DuplicateShortcutAndShiftControlRedoRestoreTheCopies()
    {
        using var harness = new FrameHarness(1100, 800);
        var scene = new SandboxScene();
        void Draw(Gui gui) => scene.View.Draw(gui, "edit-canvas");
        harness.Frame(Draw);
        harness.Frame(Draw);
        scene.Canvas.Navigation.Select(scene.Nodes[0].Key);
        harness.Gui.Focus.RequestFocus("edit-canvas");
        harness.Frame(Draw);
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.D, KeyboardKey.LeftControl);
        Assert.Equal(5, scene.View.Editor.Document.Nodes.Count);
        Assert.Equal("Paste nodes", scene.View.Editor.NextUndo!.Description);
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.Z, KeyboardKey.LeftControl);
        Assert.Equal(4, scene.View.Editor.Document.Nodes.Count);
        harness.Input.PressKey(KeyboardKey.LeftShift);
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.Z, KeyboardKey.LeftControl);
        harness.Input.ReleaseKey(KeyboardKey.LeftShift);
        harness.Frame(Draw);
        Assert.Equal(5, scene.View.Editor.Document.Nodes.Count);
    }

    [Fact]
    public void BlankClicksCancelConnectionsAndKeepAdditiveSelectionAndLogicalPortDeletion()
    {
        using var harness = new FrameHarness(1100, 800);
        var scene = new SandboxScene();
        void Draw(Gui gui) => scene.View.Draw(gui, "edit-canvas");
        harness.Frame(Draw);
        harness.Frame(Draw);
        var source = scene.View.Editor.Document.Edges.First().Source;
        harness.Click(Draw, scene.Canvas.WorldToScreen(scene.View.PortPosition(source)));
        Assert.NotNull(scene.Canvas.Navigation.ConnectionSourceKey);
        var blank = scene.Canvas.WorldToScreen(new GraphPoint(500, 500));
        harness.Click(Draw, blank);
        Assert.Null(scene.Canvas.Navigation.ConnectionSourceKey);
        scene.Canvas.Navigation.Select(scene.Nodes[0].Key);
        harness.Input.PressKey(KeyboardKey.LeftShift);
        harness.Click(Draw, blank);
        harness.Input.ReleaseKey(KeyboardKey.LeftShift);
        harness.Frame(Draw);
        Assert.Single(scene.Canvas.Navigation.SelectedKeys);
        harness.Click(Draw, blank);
        Assert.Empty(scene.Canvas.Navigation.SelectedKeys);
        scene.Canvas.Navigation.Focus(source.ToString());
        Assert.True(scene.Canvas.Navigation.Delete());
        Assert.Empty(scene.View.Editor.Document.GetEdges(source));
        Assert.True(scene.View.Undo().Succeeded);
        Assert.NotEmpty(scene.View.Editor.Document.GetEdges(source));
    }

    static void Drag(FrameHarness harness, Action<Gui> draw, Vector2 start, Vector2 end)
    {
        harness.Input.MoveTo(start);
        harness.Input.PressButton();
        harness.Frame(draw);
        harness.Input.MoveTo((start + end) / 2);
        harness.Frame(draw);
        harness.Input.MoveTo(end);
        harness.Frame(draw);
        harness.Input.ReleaseButton();
        harness.Frame(draw);
    }
}
