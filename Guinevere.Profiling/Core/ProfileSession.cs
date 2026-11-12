namespace Guinevere.Profiling;

/// <summary>Owns bounded independent producers; no producer registration captures application objects.</summary>
public sealed class ProfileSession : IDisposable
{
    readonly object _sync = new();
    readonly Dictionary<Guid, ProfileSource> _sources = [];
    bool _disposed;

    /// <summary>Creates a session with explicit per-source retention limits.</summary>
    public ProfileSession(ProfileLimits? limits = null)
    {
        Limits = limits ?? new ProfileLimits();
        Limits.Validate();
    }

    /// <summary>Unique identity for captures from this session.</summary>
    public Guid Id { get; } = Guid.NewGuid();
    /// <summary>Immutable collection limits.</summary>
    public ProfileLimits Limits { get; }

    /// <summary>Registers a headless producer with its own clock and history.</summary>
    public ProfileSource Register(string name, string kind, IProfileClock? clock = null)
    {
        clock ??= ProfileClock.Instance;
        ValidateRegistration(name, kind, clock);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_sources.Count >= Limits.Sources)
            {
                throw new InvalidOperationException("Session producer limit reached.");
            }
            var info = new ProfileSourceInfo(Guid.NewGuid(), name, kind, clock.Info);
            var source = new ProfileSource(this, info, clock);
            _sources.Add(info.Id, source);
            return source;
        }
    }

    void ValidateRegistration(string name, string kind, IProfileClock clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        if (name.Length > Limits.TextLength || kind.Length > Limits.TextLength
            || 304L + (name.Length + kind.Length) * 2L > Limits.Bytes)
        {
            throw new ArgumentException("Source metadata exceeds the text limit.");
        }
        if (clock.Info.Frequency <= 0 || clock.Info.Domain == Guid.Empty || clock.Info.ResolutionTicks is <= 0)
        {
            throw new ArgumentException("Clock metadata is invalid.", nameof(clock));
        }
    }

    /// <summary>Copies the active producer list for safe enumeration across threads.</summary>
    public IReadOnlyList<ProfileSource> GetSources()
    {
        lock (_sync)
        {
            return _sources.Values.ToArray();
        }
    }

    internal void Unregister(Guid id)
    {
        lock (_sync)
        {
            _sources.Remove(id);
        }
    }

    /// <summary>Detaches every producer while previously obtained captures remain valid.</summary>
    public void Dispose()
    {
        ProfileSource[] sources;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            sources = [.. _sources.Values];
            _sources.Clear();
        }
        foreach (var source in sources)
        {
            source.Dispose();
        }
    }
}
