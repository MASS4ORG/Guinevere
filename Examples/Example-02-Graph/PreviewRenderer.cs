namespace Example02;

/// <summary>An atomically published set of node previews.</summary>
/// <param name="Revision">Number of renders published by the current compilation.</param>
/// <param name="Seconds">The timeline position the previews show.</param>
/// <param name="Previews">Previews keyed by node identity and declared output variable identity.</param>
public sealed record PreviewSnapshot(long Revision, double Seconds, IReadOnlyDictionary<ElementId, NodePreview> Previews)
{
    /// <summary>No previews yet.</summary>
    public static PreviewSnapshot Empty { get; } = new(0, 0, new Dictionary<ElementId, NodePreview>());
}

/// <summary>Renders previews of one compiled artifact in non-overlapping background jobs.</summary>
/// <param name="domain">Creates contexts and converts observations into previews.</param>
public sealed class PreviewRenderer(IPreviewDomain domain)
{
    Job? _job;
    int _running;

    /// <summary>The previews published for the current compilation.</summary>
    public PreviewSnapshot Snapshot => Volatile.Read(ref _job)?.Snapshot ?? PreviewSnapshot.Empty;
    /// <summary>The last background failure for the current compilation, if any.</summary>
    public Exception? Error => Volatile.Read(ref _job)?.Error;
    /// <summary>Whether a render has not finished.</summary>
    public bool Running => Volatile.Read(ref _running) != 0;
    /// <summary>The latest render's completion, for deterministic shutdown and tests.</summary>
    public Task Completion { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// Replaces the artifact with fresh instance state; a render still running for an earlier artifact can only
    /// publish to that artifact's job.
    /// </summary>
    public void Start(ICompiledGraph graph, IReadOnlyDictionary<ElementId, ElementId> ports)
    {
        var context = domain.CreateContext();
        var instances = new InstanceBuffer(graph.InstanceLayout);
        var instance = instances.Allocate();
        graph.Initialize(context, instances, instance);
        Volatile.Write(ref _job, new Job(new PreviewRun(graph, context, instances, instance), ports));
    }

    /// <summary>Drops the artifact after a failed compilation.</summary>
    public void Clear() => Volatile.Write(ref _job, null);

    /// <summary>Starts one background render unless one is running or nothing is compiled.</summary>
    /// <param name="seconds">The timeline position to render.</param>
    /// <param name="published">Called from the background thread after publishing, such as Gui.RequestFrame.</param>
    public bool TryRender(double seconds, Action? published = null)
    {
        var job = Volatile.Read(ref _job);
        if (job is null || Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return false;
        }
        Completion = Task.Run(async () =>
        {
            try
            {
                job.Publish(seconds, await domain.RenderAsync(job.Run, job.Ports, seconds));
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                job.Fail(exception);
            }
            finally
            {
                Volatile.Write(ref _running, 0);
                published?.Invoke();
            }
        });
        return true;
    }

    sealed class Job(PreviewRun run, IReadOnlyDictionary<ElementId, ElementId> ports)
    {
        PreviewSnapshot _snapshot = PreviewSnapshot.Empty;
        Exception? _error;

        public PreviewRun Run { get; } = run;
        public IReadOnlyDictionary<ElementId, ElementId> Ports { get; } = ports;
        public PreviewSnapshot Snapshot => Volatile.Read(ref _snapshot);
        public Exception? Error => Volatile.Read(ref _error);

        public void Publish(double seconds, IReadOnlyDictionary<ElementId, NodePreview> previews)
        {
            Volatile.Write(ref _error, null);
            Volatile.Write(ref _snapshot, new PreviewSnapshot(Snapshot.Revision + 1, seconds, previews));
        }

        public void Fail(Exception exception) => Volatile.Write(ref _error, exception);
    }
}
