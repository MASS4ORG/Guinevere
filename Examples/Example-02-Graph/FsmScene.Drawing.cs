using MASS4.Graph.State;

namespace Example02;

public sealed partial class FsmScene
{
    /// <summary>Draws the editor and typed controls using PSS in both immediate-mode passes.</summary>
    public void Draw(Gui gui, Action<string, string, Action> button)
    {
        View.Canvas.ProcessInput(gui);
        if (gui.Pass == Pass.Pass1Build)
        {
            Pump(gui);
        }

        using (gui.Node().ExpandWidth().Height(UnitValue.Fit).Direction(Axis.Horizontal).Gap(8).Wrap(1).Enter())
        {
            button(AutoRun ? "Pause" : "Run", "fsm-run", () => AutoRun = !AutoRun);
            button("Step", "fsm-step", Step);
            button("Previous instance", "fsm-previous", () => SelectInstance((SelectedInstance + 999) % 1000));
            button("Next instance", "fsm-next", () => SelectInstance((SelectedInstance + 1) % 1000));
            button("Frame all", "fsm-frame-all", View.Canvas.FitAll);
        }
        DrawInspector(gui, button);
        DrawPalette(gui, button);
        using (gui.Node().ExpandWidth().Height(UnitValue.Fit).Direction(Axis.Horizontal).Gap(8).Wrap(1).Enter())
        {
            button("Undo", "fsm-undo", () => View.Undo());
            button("Redo", "fsm-redo", () => View.Redo());
            button("Add state", "fsm-add-state", AddState);
            button("Add transition", "fsm-add-transition", AddTransition);
            button("Copy JSON", "fsm-save", () => WithClipboard(gui, c => c.SetClipboardText(View.Serialize())));
            button("Load JSON", "fsm-load", () => WithClipboard(gui, c => Load(c.GetClipboardText())));
            button("Rename + reload", "fsm-rename", () => Load(View.Serialize(), renamed: true));
        }
        DrawStatus(gui);
        using var host = gui.Node().Expand().Enter();
        View.Draw(gui, "fsm-canvas");
        if (gui.Pass == Pass.Pass2Render && _fitPending)
        {
            View.Canvas.FitAll();
            _fitPending = false;
            gui.RequestFrame();
        }
    }

    void DrawInspector(Gui gui, Action<string, string, Action> button)
    {
        using var inspector = gui.StyledNode("fsm-inspector", id: "fsm-inspector").ExpandWidth()
            .Direction(Axis.Vertical).Gap(6).Enter();
        var state = Domain.Editor.Document.TryGetNode(FrameActiveState, out var active) ? Domain.Title(active) : "Waiting for observation";
        gui.DrawText($"Instance {SelectedInstance} / {FsmSimulation.InstanceCount} · {state} · tick {_frame.Tick}");
        using (gui.Node().ExpandWidth().Height(UnitValue.Fit).Direction(Axis.Horizontal).Gap(8).Enter())
        {
            button("Signal: " + SelectedSignal.DisplayName, "fsm-signal", () => _signalIndex = (_signalIndex + 1) % Domain.Signals.Count);
            button("Send to instance", "fsm-send", RaiseSelected);
            button("Assign trigger", "fsm-assign", () => AssignTrigger());
            button("Clear trigger", "fsm-clear-trigger", () =>
            {
                if (InspectedTransition() is { } transition)
                {
                    Domain.SetTrigger(View, transition.Id, null);
                }
            });
        }
        var transitionNode = InspectedTransition();
        gui.DrawText(transitionNode is null ? "No transition selected." : $"Transition inspector: {Domain.Title(transitionNode)}");
        var population = _frame.States.GroupBy(static id => id).Select(group =>
            Domain.Editor.Document.TryGetNode(group.Key, out var node) ? $"{Domain.Title(node)}: {group.Count()}" : "Unavailable");
        gui.DrawText("Population · " + string.Join(" · ", population));
    }

    void DrawStatus(Gui gui)
    {
        if (_frameStatus.Length == 0)
        {
            return;
        }
        using var status = gui.StyledNode("graph-message").Enter();
        foreach (var message in _frameStatus)
        {
            gui.DrawText(message);
        }
    }

    void AddState() => View.AddNode(FsmDomain.NamedState, View.Canvas.VisibleBounds.Center,
        "State " + Domain.Editor.Document.Nodes.Count);

    void AddTransition()
    {
        var states = Domain.Editor.Document.Nodes.Where(static node =>
            node.TypeId == FsmDomain.NamedState.Id || node.TypeId == StateNodes.State.Id).ToArray();
        if (states.Length > 0)
        {
            Domain.AddTransition(View, states[0].Id, states[^1].Id, SelectedSignal, View.Canvas.VisibleBounds.Center);
        }
    }

    void WithClipboard(Gui gui, Action<IClipboard> action)
    {
        if (gui.Platform.TryGet<IClipboard>(out var clipboard) && clipboard is not null)
        {
            action(clipboard);
        }
        else
        {
            _status = "This host does not provide a clipboard.";
        }
    }

    GraphConnector RouteLink(GraphEdge edge, GraphConnector connector)
    {
        var document = Domain.Editor.Document;
        var from = document.GetNode(document.GetPort(edge.Source).NodeId);
        var to = document.GetNode(document.GetPort(edge.Target).NodeId);
        if (to.TypeId == StateNodes.Transition.Id && SelfTransition(to))
        {
            var right = connector.Source.X + 60;
            var left = connector.Target.X - 40;
            var lane = View.Position(to.Id).Y - 40;
            return new GraphConnector(connector.Source, connector.Target,
                [new(right, connector.Source.Y), new(right, lane), new(left, lane), new(left, connector.Target.Y)]);
        }
        if (connector.Target.X < connector.Source.X)
        {
            var extra = from.TypeId == StateNodes.Transition.Id ? 120 : 60;
            var lane = Math.Max(View.Position(from.Id).Y, View.Position(to.Id).Y) + 100 + extra;
            var right = connector.Source.X + 40;
            var left = connector.Target.X - 40;
            return new GraphConnector(connector.Source, connector.Target,
                [new(right, connector.Source.Y), new(right, lane), new(left, lane), new(left, connector.Target.Y)]);
        }
        return connector;
    }

    bool SelfTransition(GraphNode transition)
    {
        var document = Domain.Editor.Document;
        var from = document.GetEdges(transition.GetPort(StateNodes.FromKey).Id).FirstOrDefault();
        var to = document.GetEdges(transition.GetPort(StateNodes.ToKey).Id).FirstOrDefault();
        return from is not null && to is not null
            && document.GetPort(from.Source).NodeId == document.GetPort(to.Target).NodeId;
    }
}
