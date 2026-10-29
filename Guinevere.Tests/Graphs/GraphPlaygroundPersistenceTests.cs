using Example02;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Graphs;

public sealed class GraphPlaygroundPersistenceTests
{
    [Fact]
    public async Task LoadingRebindsTheFlowToTheLoadedDocumentAndContinuesObserving()
    {
        using var harness = new FrameHarness(1200, 900);
        var playground = new GraphPlayground();
        playground.SelectScenario(1);
        harness.Frame(playground.Draw);
        await playground.Flow.Flow.Completion;
        harness.Frame(playground.Draw);
        var flow = playground.Flow;
        var oldEditor = flow.Sandbox.Graph.Editor;
        var oldOverlay = flow.Flow;
        var json = flow.Sandbox.View.Serialize();
        Assert.Null(flow.Sandbox.Load("malformed").View);
        Assert.Same(oldEditor, flow.Sandbox.Graph.Editor);
        Assert.NotNull(flow.Sandbox.Load(json).View);
        Assert.NotSame(oldEditor, flow.Sandbox.Graph.Editor);
        harness.Frame(playground.Draw);
        await flow.Flow.Completion;
        harness.Frame(playground.Draw);
        Assert.NotSame(oldOverlay, flow.Flow);
        Assert.NotEmpty(flow.Flow.Values);
        var node = flow.Sandbox.Graph.Editor.Document.Nodes.First(static n => n.TypeId == ArithmeticGraph.Number.Id);
        var other = flow.Sandbox.Graph.Editor.Document.Nodes.Single(n => n.TypeId == ArithmeticGraph.Number.Id && n.Id != node.Id);
        var expected = ((float)node.Payload! + 2) * (float)other.Payload!;
        flow.Sandbox.Step(node.Id, 2);
        harness.Frame(playground.Draw);
        await flow.Flow.Completion;
        harness.Frame(playground.Draw);
        var result = flow.Sandbox.Graph.Editor.Document.Nodes.Single(static n => n.TypeId == ArithmeticGraph.Result.Id);
        Assert.Equal(expected, flow.Flow.Values[result.GetPort(ArithmeticGraph.ValueKey).Id]);
    }

    [Fact]
    public void PersistenceAndEditingToolbarActionsWorkFromTheKeyboard()
    {
        using var harness = new FrameHarness(1400, 1000);
        var playground = new GraphPlayground();
        Action<Gui> draw = playground.Draw;
        harness.Frame(draw);
        harness.Frame(draw);
        Activate(harness, draw, "save-json");
        var json = harness.Input.GetClipboardText();
        Assert.Contains("mass4.graph", json, StringComparison.Ordinal);
        var original = playground.Sandbox.View.Editor;
        Activate(harness, draw, "load-json");
        Assert.NotSame(original, playground.Sandbox.View.Editor);
        playground.Sandbox.View.SelectAll();
        Activate(harness, draw, "edit-actions");
        Activate(harness, draw, "copy-selection");
        Activate(harness, draw, "paste-selection");
        Assert.Equal(8, playground.Sandbox.Graph.Editor.Document.Nodes.Count);
        Activate(harness, draw, "align-left");
        Activate(harness, draw, "align-top");
        Activate(harness, draw, "distribute-x");
        Activate(harness, draw, "distribute-y");
        Activate(harness, draw, "snap-grid");
        Activate(harness, draw, "duplicate-selection");
        Assert.Equal(12, playground.Sandbox.Graph.Editor.Document.Nodes.Count);
        Activate(harness, draw, "cut-selection");
        Assert.Equal(8, playground.Sandbox.Graph.Editor.Document.Nodes.Count);
        Activate(harness, draw, "delete-selection");
        harness.Input.SetClipboardText("bad JSON");
        var before = playground.Sandbox.View.Editor;
        Activate(harness, draw, "load-json");
        Assert.Same(before, playground.Sandbox.View.Editor);
        harness.Gui.Platform.Remove<IClipboard>();
        Activate(harness, draw, "save-json");
        Activate(harness, draw, "load-json");
        Assert.Same(before, playground.Sandbox.View.Editor);
    }

    static void Activate(FrameHarness harness, Action<Gui> draw, string id)
    {
        harness.Gui.Focus.RequestFocus(id);
        harness.Frame(draw);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Enter);
        harness.Frame(draw);
    }
}
