using MASS4.Graph.State;

namespace Example02;

/// <summary>A published observation of every instance of one compiled state-machine asset.</summary>
/// <param name="Tick">Completed simulation tick.</param>
/// <param name="States">Active state identities, obtained exclusively from Observe.</param>
public sealed record FsmSnapshot(long Tick, IReadOnlyList<ElementId> States);

/// <summary>Evaluates many instances in one background job, publishing immutable observations without overlap.</summary>
public sealed class FsmSimulation
{
    Run? _run;
    int _running;
    static readonly FsmSnapshot Empty = new(0, Array.Empty<ElementId>());

    /// <summary>The number of instances sharing one asset.</summary>
    public const int InstanceCount = 1000;
    /// <summary>The last atomically published observation.</summary>
    public FsmSnapshot Snapshot => Volatile.Read(ref _run)?.Snapshot ?? Empty;
    /// <summary>Whether a background tick has not finished.</summary>
    public bool Running => Volatile.Read(ref _running) != 0;
    /// <summary>The latest tick's completion for deterministic host shutdown and tests.</summary>
    public Task Completion { get; private set; } = Task.CompletedTask;
    /// <summary>The last evaluation failure, if any.</summary>
    public Exception? Error => Volatile.Read(ref _run)?.Error;
    /// <summary>The most recent compilation diagnostics.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; private set; } = [];

    /// <summary>Compiles on the document-owning thread and starts fresh instance state after authored edits.</summary>
    public void Compile(FsmDomain domain)
    {
        var result = new GraphInterpreter(domain.Data, state: domain.States).Compile(domain.Editor.Document);
        Diagnostics = result.Diagnostics;
        Volatile.Write(ref _run, result.Graph is { } graph
            ? new Run(graph, domain.Registry.Signals.Canonicalize(FsmDomain.LegacySeePlayer)) : null);
    }

    /// <summary>Advances every instance and optionally raises a registered signal on one selected instance.</summary>
    public bool TryAdvance(int selected, StableId? signal = null, Action? published = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(selected);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(selected, InstanceCount);
        var run = Volatile.Read(ref _run);
        if (run is null || Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return false;
        }
        Completion = Task.Run(async () =>
        {
            try
            {
                var states = await Advance(run, selected, signal);
                run.Publish(states);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                run.Fail(exception);
            }
            finally
            {
                Volatile.Write(ref _running, 0);
                published?.Invoke();
            }
        });
        return true;
    }

    static async Task<ElementId[]> Advance(Run run, int selected, StableId? signal)
    {
        var states = new ElementId[InstanceCount];
        var observations = new List<GraphObservation>();
        for (var i = 0; i < states.Length; i++)
        {
            var handle = run.Handles[i];
            if (run.Tick == 0 && i % 2 != 0)
            {
                run.Graph.Raise(run.Instances, handle, run.InitialSignal);
            }

            if (i == selected && signal is { } raised)
            {
                run.Graph.Raise(run.Instances, handle, raised);
            }

            await run.Graph.EvaluateAsync(run.Context, run.Instances, handle);
            observations.Clear();
            run.Graph.Observe(run.Context, run.Instances, handle, observations);
            states[i] = observations.Select(static observation => observation.Value).OfType<ElementId>().Single();
        }
        return states;
    }

    sealed class Run
    {
        FsmSnapshot _snapshot = Empty;
        Exception? _error;
        public Run(ICompiledGraph graph, StableId initialSignal)
        {
            Graph = graph;
            InitialSignal = initialSignal;
            Instances = new InstanceBuffer(graph.InstanceLayout, InstanceCount);
            Context.Set(new StateTime { DeltaSeconds = 1.0 / 30 });
            Handles = new InstanceHandle[InstanceCount];
            for (var i = 0; i < Handles.Length; i++)
            {
                Handles[i] = Instances.Allocate();
                graph.Initialize(Context, Instances, Handles[i]);
            }
        }
        public ICompiledGraph Graph { get; }
        public StableId InitialSignal { get; }
        public InstanceBuffer Instances { get; }
        public EvaluationContext Context { get; } = new();
        public InstanceHandle[] Handles { get; }
        public long Tick { get; set; }
        public FsmSnapshot Snapshot => Volatile.Read(ref _snapshot);
        public Exception? Error => Volatile.Read(ref _error);
        public void Publish(ElementId[] states)
        {
            Volatile.Write(ref _error, null);
            Volatile.Write(ref _snapshot, new FsmSnapshot(++Tick, Array.AsReadOnly(states)));
        }
        public void Fail(Exception exception) => Volatile.Write(ref _error, exception);
    }
}
