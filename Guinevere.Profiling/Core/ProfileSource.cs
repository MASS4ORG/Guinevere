namespace Guinevere.Profiling;

/// <summary>Collects concurrent tracks into one frame with bounded payload and immutable publication.</summary>
public sealed class ProfileSource : IDisposable
{
    readonly object _sync = new();
    readonly ProfileSession _session;
    IProfileClock? _clock;
    readonly ProfileLimits _limits;
    readonly List<ProfileEvent> _events = [];
    readonly List<ProfileItem> _items = [];
    readonly Dictionary<long, ProfileEvent> _open = [];
    readonly Queue<ProfileCapture> _history = [];
    long _sequence, _generation, _frame, _start, _bytes, _historyBytes, _dropped;
    bool _active, _disposed;

    internal ProfileSource(ProfileSession session, ProfileSourceInfo info, IProfileClock clock)
    {
        _session = session;
        Info = info;
        _clock = clock;
        _limits = session.Limits;
    }

    /// <summary>Source identity and clock declaration.</summary>
    public ProfileSourceInfo Info { get; }

    /// <summary>Begins a frame after the previous frame has completed or been cancelled.</summary>
    public void BeginFrame(long frame)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_active)
            {
                throw new InvalidOperationException("A frame is already active.");
            }
            _events.Clear();
            _items.Clear();
            _open.Clear();
            _frame = frame;
            _start = _clock!.GetTimestamp();
            _bytes = 256 + TextBytes(Info.Name) + TextBytes(Info.Kind);
            _dropped = 0;
            while (_history.Count > 0 && _historyBytes + _bytes > _limits.Bytes)
            {
                _historyBytes -= _history.Dequeue().RetainedBytes;
            }
            _active = true;
        }
    }

    /// <summary>Starts a CPU scope; callers may explicitly correlate parents and cross-track flows.</summary>
    public ProfileScope BeginScope(string name, int track = 0, long parentId = 0, long flowId = 0)
    {
        lock (_sync)
        {
            RequireFrame();
            var charge = 128 + TextBytes(name);
            if (!Accept(name, charge, _events.Count + _open.Count, _limits.Events))
            {
                return default;
            }
            var id = ++_sequence;
            _open.Add(id, new ProfileEvent(id, parentId, track, "scope", name,
                _clock!.GetTimestamp(), 0, FlowId: flowId));
            return new ProfileScope(this, id);
        }
    }

    internal void CompleteScope(long id)
    {
        lock (_sync)
        {
            if (!_active || !_open.Remove(id, out var entry))
            {
                return;
            }
            var end = _clock!.GetTimestamp();
            if (end < entry.Start)
            {
                _dropped++;
                throw new InvalidOperationException("The clock moved backwards.");
            }
            _events.Add(entry with { End = end });
        }
    }

    /// <summary>Adds a measured numeric counter with an explicit unit and track.</summary>
    public void Counter(string name, double value, string unit, int track = 0)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
        AddEvent("counter", name, unit, value, track);
    }

    /// <summary>Adds a diagnostic without retaining exceptions, source objects or delegates.</summary>
    public void Diagnostic(string name, string message, int track = 0) =>
        AddEvent("diagnostic", name, message, null, track);

    void AddEvent(string kind, string name, string message, double? value, int track)
    {
        lock (_sync)
        {
            RequireFrame();
            if (!AcceptText(message))
            {
                _dropped++;
                return;
            }
            if (Accept(name, 128 + TextBytes(name) + TextBytes(message),
                _events.Count + _open.Count, _limits.Events))
            {
                var now = _clock!.GetTimestamp();
                _events.Add(new ProfileEvent(++_sequence, 0, track, kind, name, now, now, value, message));
            }
        }
    }

    /// <summary>Adds copied inspector data; oversized items are dropped with visible accounting.</summary>
    public void Inspect(in ProfileItem item)
    {
        lock (_sync)
        {
            RequireFrame();
            if (!ValidItemText(item) || !ValidItemGeometry(item))
            {
                _dropped++;
                return;
            }
            var bytes = 128 + TextBytes(item.Id) + TextBytes(item.ParentId) + TextBytes(item.Category)
                + TextBytes(item.Name) + TextBytes(item.Value);
            if (Accept(item.Name, bytes, _items.Count, _limits.Items))
            {
                _items.Add(item);
            }
        }
    }

    bool ValidItemText(ProfileItem item) => AcceptText(item.Id) && AcceptText(item.ParentId)
        && AcceptText(item.Category) && AcceptText(item.Name) && AcceptText(item.Value);

    static bool ValidItemGeometry(ProfileItem item) => Finite(item.X) && Finite(item.Y)
        && Finite(item.Width) && Finite(item.Height);

    static bool Finite(float? value) => value is null || float.IsFinite(value.Value);
    bool Accept(string name, long bytes, int count, int maximum)
    {
        if (!AcceptText(name) || count >= maximum || bytes > _limits.Bytes - _bytes)
        {
            _dropped++;
            return false;
        }
        while (_history.Count > 0 && _historyBytes + _bytes + bytes > _limits.Bytes)
        {
            _historyBytes -= _history.Dequeue().RetainedBytes;
        }
        _bytes += bytes;
        return true;
    }

    bool AcceptText(string? text) => text is not null && text.Length <= _limits.TextLength;
    static long TextBytes(string text) => 24L + text.Length * 2L;

    /// <summary>Publishes a completed capture and evicts older history by both count and byte budget.</summary>
    public ProfileCapture EndFrame()
    {
        lock (_sync)
        {
            RequireFrame();
            var end = _clock!.GetTimestamp();
            if (end < _start)
            {
                throw new InvalidOperationException("The clock moved backwards.");
            }
            _dropped += _open.Count;
            _open.Clear();
            _events.Sort(static (a, b) => a.Id.CompareTo(b.Id));
            var capture = new ProfileCapture(_session.Id, Info, _frame, ++_generation, _start, end,
                [.. _events], [.. _items], _dropped, _bytes);
            _history.Enqueue(capture);
            _historyBytes += capture.RetainedBytes;
            while (_history.Count > _limits.Frames || _historyBytes > _limits.Bytes)
            {
                _historyBytes -= _history.Dequeue().RetainedBytes;
            }
            _active = false;
            _events.Clear();
            _items.Clear();
            return capture;
        }
    }

    /// <summary>Cancels incomplete work without replacing the last successful capture.</summary>
    public void CancelFrame()
    {
        lock (_sync)
        {
            _active = false;
            _open.Clear();
            _events.Clear();
            _items.Clear();
        }
    }

    /// <summary>Copies the completed history, oldest first, for safe concurrent reading.</summary>
    public IReadOnlyList<ProfileCapture> GetHistory()
    {
        lock (_sync)
        {
            return _history.ToArray();
        }
    }

    void RequireFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_active)
        {
            throw new InvalidOperationException("No profiling frame is active.");
        }
    }

    /// <summary>Detaches this producer and releases its history; previously held captures stay valid.</summary>
    public void Dispose()
    {
        lock (_sync)
        {
            _disposed = true;
            CancelFrame();
            _history.Clear();
            _historyBytes = 0;
            _clock = null;
        }
        _session.Unregister(Info.Id);
    }
}

/// <summary>Completes a scope exactly once even when a value copy is disposed repeatedly.</summary>
public readonly struct ProfileScope : IDisposable
{
    readonly ProfileSource? _source;
    internal ProfileScope(ProfileSource source, long id) { _source = source; Id = id; }
    /// <summary>Identity used to correlate explicit child scopes.</summary>
    public long Id { get; }
    /// <summary>Closes the scope, or does nothing after its frame was completed/cancelled.</summary>
    public void Dispose() => _source?.CompleteScope(Id);
}
