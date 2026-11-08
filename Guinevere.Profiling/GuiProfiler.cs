using System.Globalization;
using MASS4.Profiling;

namespace Guinevere.Profiling;

/// <summary>Opt-in frame collection independent of viewer assemblies; dispose before target code unload.</summary>
public sealed partial class GuiProfiler : IGuiFrameObserver, IDisposable
{
    Gui? _gui;
    Gui TargetGui => _gui ?? throw new ObjectDisposedException(nameof(GuiProfiler));
    readonly Stack<LayoutNode> _nodes = [];
    ProfileScope _phase;
    long _frame, _allocated, _observerBytes;
    long _immediateRequests, _scheduledRequests;
    bool _active, _disposed;

    /// <summary>Attaches an exclusive collector to a GUI and registers its source in the shared session.</summary>
    public GuiProfiler(Gui gui, ProfileSession session, string name, IProfileClock? clock = null)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(session);
        if (gui.FrameObserver is not null)
        {
            throw new InvalidOperationException("This GUI already has a frame observer.");
        }
        Source = session.Register(name, "gui", clock);
        _gui = gui;
        gui.FrameObserver = this;
    }

    /// <summary>Shared producer and bounded completed history.</summary>
    public ProfileSource Source { get; }
    /// <summary>Whether subsequent frames include copied detailed inspector values.</summary>
    public bool Detailed { get; set; }
    /// <summary>Latest successful capture, preserved if a later target frame fails.</summary>
    public ProfileCapture? Latest { get; private set; }
    /// <summary>Copied diagnostic for the latest failed frame, or null after success.</summary>
    public string? LastError { get; private set; }
    /// <inheritdoc />
    public bool CaptureDetails => Detailed && _active;

    /// <inheritdoc />
    public void BeginFrame(Gui gui)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Source.BeginFrame(++_frame);
        _allocated = GC.GetAllocatedBytesForCurrentThread();
        _observerBytes = 0;
        _active = true;
        _phase = Source.BeginScope("input");
    }

    /// <inheritdoc />
    public void Phase(string name)
    {
        if (!_active)
        {
            return;
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        _phase.Dispose();
        _phase = Source.BeginScope(name);
        _observerBytes += GC.GetAllocatedBytesForCurrentThread() - before;
    }

    /// <inheritdoc />
    public void EndFrame(Gui gui)
    {
        if (!_active)
        {
            return;
        }
        _phase.Dispose();
        var total = GC.GetAllocatedBytesForCurrentThread() - _allocated;
        Source.Counter("thread.allocated.frame-including-observer", total, "bytes");
        Source.Counter("thread.allocated.observer-internal", _observerBytes, "bytes");
        Source.Counter("work.immediate-requests", Interlocked.Exchange(ref _immediateRequests, 0), "count");
        Source.Counter("work.scheduled-requests", Interlocked.Exchange(ref _scheduledRequests, 0), "count");
        CollectSummary(gui);
        if (Detailed)
        {
            CollectDetails(gui);
        }
        Latest = Source.EndFrame();
        LastError = null;
        _active = false;
    }

    void CollectSummary(Gui gui)
    {
        var statistics = gui.RenderStatistics;
        Source.Counter("draw.commands", statistics.Commands, "count");
        Source.Counter("draw.canvas-submissions", statistics.PrimitiveDrawCalls, "count");
        Source.Counter("draw.opaque-drawables", statistics.Drawables, "count");
        Source.Counter("draw.callbacks", statistics.Callbacks, "count");
        Source.Counter("gui.elapsed", gui.Clock.Elapsed, "seconds");
        Source.Counter("gui.delta", gui.Clock.DeltaTime, "seconds");
        Source.Counter("layout.nodes", gui.RenderedNodeCount, "count");
    }

    /// <inheritdoc />
    public void Inspect(string id, string parent, string category, string name, string value, Rect? bounds = null)
    {
        if (!CaptureDetails)
        {
            return;
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        Source.Inspect(new ProfileItem(id, parent, category, name, value,
            bounds?.X, bounds?.Y, bounds?.W, bounds?.H));
        _observerBytes += GC.GetAllocatedBytesForCurrentThread() - before;
    }

    /// <inheritdoc />
    public void FrameRequested(double delaySeconds)
    {
        if (delaySeconds > 0)
        {
            Interlocked.Increment(ref _scheduledRequests);
        }
        else
        {
            Interlocked.Increment(ref _immediateRequests);
        }
    }

    /// <summary>Cancels partial collection and copies the error without retaining its application exception.</summary>
    public void Failed(Exception exception)
    {
        LastError = exception.ToString();
        Source.CancelFrame();
        _active = false;
    }

    /// <summary>Detaches all hooks and releases producer history; already held captures stay valid.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        if (ReferenceEquals(TargetGui.FrameObserver, this))
        {
            TargetGui.FrameObserver = null;
        }
        _nodes.Clear();
        _phase = default;
        _active = false;
        _disposed = true;
        Source.Dispose();
        _gui = null;
    }

    static string Number<T>(T value) where T : IFormattable => value.ToString(null, CultureInfo.InvariantCulture);
}
