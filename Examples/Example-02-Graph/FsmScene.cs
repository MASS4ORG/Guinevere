using MASS4.Graph.State;
using GraphPoint = Guinevere.Graphs.GraphPoint;

namespace Example02;

/// <summary>A real cyclic FSM editor with typed triggers and an observed inspector for 1000 shared-asset instances.</summary>
public sealed partial class FsmScene
{
    readonly Queue<(int Instance, StableId Signal)> _signals = new();
    FsmSnapshot _frame = new(0, Array.Empty<ElementId>());
    bool _dirty = true;
    bool _step = true;
    int _signalIndex;
    string? _status;
    int _announcedInstance = -1;
    ElementId _announcedState;
    bool _fitPending = true;
    string[] _frameStatus = [];

    /// <summary>Builds the initial cyclic document with a self-transition.</summary>
    public FsmScene()
    {
        View = new GraphDocumentView(Domain.Editor, new GraphCanvas { Name = "State machine", ShowMinimap = true });
        ConfigureView();
        Domain.Populate(View);
        Domain.Editor.Document.Changed += DocumentChanged;
        View.Canvas.Camera.SetView(new GraphPoint(-20, -20), 0.8);
    }

    /// <summary>The sample-owned State domain.</summary>
    public FsmDomain Domain { get; private set; } = new();
    /// <summary>The ordinary graph document view and command surface.</summary>
    public GraphDocumentView View { get; private set; }
    /// <summary>The non-overlapping background simulation.</summary>
    public FsmSimulation Simulation { get; } = new();
    /// <summary>The inspected instance index; every instance shares the authored asset.</summary>
    public int SelectedInstance { get; private set; }
    /// <summary>Runs simulation ticks continuously, or only on Step/Send when false.</summary>
    public bool AutoRun { get; set; } = true;
    /// <summary>The registered typed signal selected in the picker.</summary>
    public SignalType SelectedSignal => Domain.Signals[_signalIndex];
    /// <summary>The state observed for the selected instance in the latest published snapshot.</summary>
    public ElementId ActiveState => Simulation.Snapshot.States.ElementAtOrDefault(SelectedInstance);

    /// <summary>Selects an existing instance without modifying the authored document.</summary>
    public void SelectInstance(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, FsmSimulation.InstanceCount);
        SelectedInstance = index;
    }

    /// <summary>Selects only identities supplied by the registry.</summary>
    public void SelectSignal(SignalType signal)
    {
        var signals = Domain.Signals;
        var id = Domain.Registry.Signals.Canonicalize(signal.Id);
        var index = signals.ToList().FindIndex(candidate => candidate.Id == id);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        _signalIndex = index;
    }

    /// <summary>Queues a typed signal for the currently inspected instance's next tick.</summary>
    public void RaiseSelected()
    {
        _signals.Enqueue((SelectedInstance, SelectedSignal.Id));
        _step = true;
    }

    /// <summary>Requests one background tick while paused.</summary>
    public void Step() => _step = true;

    /// <summary>Applies the selected signal to the transition under logical focus, through an undoable command.</summary>
    public CommandResult AssignTrigger() => InspectedTransition() is { } transition
        ? Domain.SetTrigger(View, transition.Id, SelectedSignal) : new CommandResult(false, []);

    /// <summary>Loads atomically, optionally resolving the original signal through an upgraded registry rename.</summary>
    public GraphViewLoadResult Load(string json, bool renamed = false)
    {
        var domain = new FsmDomain(renamed);
        var loaded = GraphDocumentView.Load(json, domain.Registry,
            compatibility: domain.Data.Conversions, rules: domain.States.Rules);
        _status = string.Join("; ", loaded.Diagnostics.Select(static d => $"{d.Id}: {d.Message}"));
        if (loaded.View is not { } view)
        {
            return loaded;
        }
        Domain.Editor.Document.Changed -= DocumentChanged;
        var origin = View.Canvas.Camera.Origin;
        var zoom = View.Canvas.Camera.Zoom;
        domain.UseEditor(view.Editor);
        Domain = domain;
        View = view;
        ConfigureView();
        View.Canvas.Camera.SetView(origin, zoom);
        Domain.Editor.Document.Changed += DocumentChanged;
        _signals.Clear();
        _signalIndex = 0;
        _dirty = true;
        _step = true;
        return loaded;
    }

    void ConfigureView()
    {
        View.StyleClasses = ["fsm"];
        View.Canvas.Name = "State machine";
        View.Canvas.ShowMinimap = true;
        View.NodeTitle = Domain.Title;
        View.NodeClasses = NodeClasses;
        View.NodeState = DescribeState;
        View.NodeBadges = node => node.Id == FrameActiveState ? ["active"] : [];
        View.PortShape = static port => port.Direction == PortDirection.Output ? GraphPortShape.Triangle : GraphPortShape.Circle;
        View.RouteLink = RouteLink;
        View.Canvas.Navigation.SearchRequested = _ => _paletteOpen = true;
        View.ContextRequested = _ => _paletteOpen = true;
    }

    ElementId FrameActiveState => _frame.States.ElementAtOrDefault(SelectedInstance);

    IReadOnlyList<string> NodeClasses(GraphNode node)
    {
        var role = node.TypeId == StateNodes.Transition.Id ? "fsm-transition"
            : node.TypeId == StateNodes.Entry.Id ? "fsm-entry" : "fsm-state";
        return node.Id == FrameActiveState ? [role, "fsm-active"] : [role];
    }

    string DescribeState(GraphNode node) => node.TypeId == StateNodes.Transition.Id
        ? $"Trigger: {Domain.TriggerName(node)}."
        : $"Active in {_frame.States.Count(id => id == node.Id)} of {FsmSimulation.InstanceCount} instances."
            + (node.Id == FrameActiveState ? $" Selected instance {SelectedInstance} is active here." : "");

    void DocumentChanged(GraphDocument document, IReadOnlyList<GraphChange> changes) =>
        _dirty |= changes.Any(static change => change.Kind is not (GraphChangeKind.LayoutChanged
            or GraphChangeKind.GroupAdded or GraphChangeKind.GroupRemoved or GraphChangeKind.GroupChanged));

    void Pump(Gui gui)
    {
        if (_dirty)
        {
            _dirty = false;
            Simulation.Compile(Domain);
            View.Validation = Simulation.Diagnostics;
            _step = true;
        }
        if (AutoRun || _step)
        {
            var pending = _signals.TryPeek(out var signal);
            if (Simulation.TryAdvance(pending ? signal.Instance : SelectedInstance, pending ? signal.Signal : null,
                    gui.RequestFrame))
            {
                if (pending)
                {
                    _signals.Dequeue();
                }

                _step = false;
            }
        }
        _frame = Simulation.Snapshot;
        _frameStatus = StatusMessages();
        AnnounceState();
    }

    string[] StatusMessages()
    {
        var messages = View.Diagnostics.Concat(View.Validation).Distinct()
            .Select(static diagnostic => $"{diagnostic.Id}: {diagnostic.Message}").ToList();
        if (Simulation.Error is { } error)
        {
            messages.Add(error.Message);
        }

        if (!string.IsNullOrEmpty(_status))
        {
            messages.Add(_status);
        }

        return [.. messages];
    }

    void AnnounceState()
    {
        var state = FrameActiveState;
        if ((_announcedInstance != SelectedInstance || _announcedState != state)
            && Domain.Editor.Document.TryGetNode(state, out var node))
        {
            _announcedInstance = SelectedInstance;
            _announcedState = state;
            View.Canvas.Announce(new(GraphSemanticEventKind.StateChanged, state.ToString(), null,
                $"Instance {SelectedInstance}: {Domain.Title(node)}."));
        }
    }

    GraphNode? InspectedTransition()
    {
        var focused = TransitionAt(View.Canvas.Navigation.FocusedKey);
        return focused ?? View.Canvas.Navigation.SelectedKeys.Select(TransitionAt).FirstOrDefault(static n => n is not null)
            ?? Domain.Editor.Document.Nodes.FirstOrDefault(static n => n.TypeId == StateNodes.Transition.Id);
    }

    GraphNode? TransitionAt(string? key)
    {
        if (!ElementId.TryParse(key, out var id))
        {
            return null;
        }

        var document = Domain.Editor.Document;
        if (document.TryGetPort(id, out var port))
        {
            id = port.NodeId;
        }

        if (document.TryGetEdge(id, out var edge))
        {
            return TransitionAt(document.GetPort(edge.Source).NodeId.ToString())
                ?? TransitionAt(document.GetPort(edge.Target).NodeId.ToString());
        }
        return document.TryGetNode(id, out var node) && node.TypeId == StateNodes.Transition.Id ? node : null;
    }
}
