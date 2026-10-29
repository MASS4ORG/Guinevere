using System.Text.Json;
using MASS4.Graph.Dataflow;
using MASS4.Graph.State;
using GraphPoint = Guinevere.Graphs.GraphPoint;

namespace Example02;

/// <summary>A small cyclic state-machine asset using registered signals and the headless State semantics.</summary>
public sealed class FsmDomain
{
    /// <summary>The original signal identity, retained as a rename entry by the upgraded registry.</summary>
    public static StableId LegacySeePlayer { get; } = Id(2);
    /// <summary>The upgraded signal declaration.</summary>
    public static SignalType TargetSpotted { get; } = new(Id(3), "Target spotted");
    /// <summary>A signal returning the machine to Patrol.</summary>
    public static SignalType LosePlayer { get; } = new(Id(4), "Lose player");
    /// <summary>A signal producing the Chase self-transition.</summary>
    public static SignalType Hit { get; } = new(Id(5), "Hit");
    /// <summary>A named state with an immutable text payload.</summary>
    public static NodeType NamedState { get; } = new(Id(1), "Named state", [StateNodes.Enter(), StateNodes.Exit()])
    {
        DefaultPayload = static () => "State",
        PayloadSerializer = new StateNameSerializer()
    };

    /// <summary>Registers the original or renamed signal vocabulary and creates an empty real editor.</summary>
    public FsmDomain(bool renamed = false)
    {
        StateNodes.Register(Registry);
        Registry.NodeTypes.Register(NamedState);
        Registry.Signals.Register(renamed ? TargetSpotted : new SignalType(LegacySeePlayer, "See player"));
        Registry.Signals.Register(LosePlayer);
        Registry.Signals.Register(Hit);
        if (renamed)
        {
            Registry.Signals.AddRename(LegacySeePlayer, TargetSpotted.Id);
        }
        States.AddState(NamedState.Id, new PassiveState());
        Editor = new GraphEditor(new GraphDocument(Registry), Data.Conversions, States.Rules);
    }

    /// <summary>The registry shared by authoring, serialization and execution.</summary>
    public GraphTypeRegistry Registry { get; } = new();
    /// <summary>The interpreter's data plane, with no data nodes required by this machine.</summary>
    public DataflowSemantics Data { get; } = new();
    /// <summary>The actual state behaviors and structural rules.</summary>
    public StateSemantics States { get; } = new();
    /// <summary>The sole semantic mutation entry point.</summary>
    public GraphEditor Editor { get; private set; }
    /// <summary>The registered signals, ordered for a keyboard-operable picker.</summary>
    public IReadOnlyList<SignalType> Signals => Registry.Signals.Entries.OrderBy(static signal => signal.DisplayName,
        StringComparer.Ordinal).ToArray();

    /// <summary>Adopts an editor loaded with this domain's registry and validation rules.</summary>
    public void UseEditor(GraphEditor editor) => Editor = editor;

    /// <summary>Populates Patrol ↔ Chase and Chase → Chase through ordinary reversible editor commands.</summary>
    public void Populate(GraphDocumentView view)
    {
        var entry = view.AddNode(StateNodes.Entry, new GraphPoint(20, 220));
        var patrol = view.AddNode(NamedState, new GraphPoint(250, 220), "Patrol");
        var chase = view.AddNode(NamedState, new GraphPoint(850, 220), "Chase");
        view.Connect(Port(entry, StateNodes.StartKey), Port(patrol, StateNodes.EnterKey));
        AddTransition(view, patrol, chase, Registry.Signals.Resolve(LegacySeePlayer), new GraphPoint(530, 80));
        AddTransition(view, chase, patrol, LosePlayer, new GraphPoint(530, 420));
        AddTransition(view, chase, chase, Hit, new GraphPoint(900, 420));
        Editor.ClearHistory();
        view.Refresh();
    }

    /// <summary>Adds a complete, typed transition as one validated undo step.</summary>
    public CommandResult AddTransition(GraphDocumentView view, ElementId from, ElementId to, SignalType signal,
        GraphPoint position)
    {
        var transition = new AddNodeCommand(StateNodes.Transition) { Annotations = StateNodes.Annotations(signal.Id) };
        var command = new CompositeCommand("Add transition",
        [
            transition,
            new ConnectCommand(Port(from, StateNodes.ExitKey), transition.PortId(StateNodes.FromKey)),
            new ConnectCommand(transition.PortId(StateNodes.ToKey), Port(to, StateNodes.EnterKey))
        ]);
        var result = view.Execute(command);
        if (result.Succeeded)
        {
            view.SetPosition(transition.NodeId, position);
            view.Refresh();
        }
        return result;
    }

    /// <summary>Uses only a registered identity to change a transition's trigger.</summary>
    public CommandResult SetTrigger(GraphDocumentView view, ElementId transition, SignalType? signal) =>
        view.Execute(new SetAnnotationCommand(transition, StateNodes.Trigger.Id, signal?.Id));

    /// <summary>Formats domain titles without placing State semantics in the graph controls.</summary>
    public string Title(GraphNode node) => node.TypeId == NamedState.Id ? (string?)node.Payload ?? "State"
        : node.TypeId == StateNodes.Transition.Id ? "On " + TriggerName(node) : Registry.NodeTypes.Resolve(node.TypeId).DisplayName;

    /// <summary>The currently resolved registered signal name, or an automatic transition.</summary>
    public string TriggerName(GraphNode node) => node.Annotations.TryGetValue(StateNodes.Trigger.Id, out var value)
        && value is StableId id && Registry.Signals.TryResolve(id, out var signal) ? signal.DisplayName : "Automatic";

    ElementId Port(ElementId node, StableId key) => Editor.Document.GetNode(node).GetPort(key).Id;
    static StableId Id(int value) => StableId.Parse($"7445e156-a92d-4ad0-83ec-{value:000000000000}");

    sealed class PassiveState : IStateNode;

    sealed class StateNameSerializer : IValueSerializer
    {
        /// <inheritdoc />
        public object? Read(JsonElement value) => value.GetString();
        /// <inheritdoc />
        public void Write(Utf8JsonWriter writer, object? value) => writer.WriteStringValue((string?)value);
    }
}
