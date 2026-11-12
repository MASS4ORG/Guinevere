using System.Collections.ObjectModel;

namespace Guinevere.Profiling;

/// <summary>Identifies a producer independently of GUI-local or engine-local identifiers.</summary>
/// <param name="Id">Session-local producer identity.</param>
/// <param name="Name">Display name.</param>
/// <param name="Kind">Domain such as gui or engine.</param>
/// <param name="Clock">Clock used by this producer.</param>
public sealed record ProfileSourceInfo(Guid Id, string Name, string Kind, ProfileClockInfo Clock);

/// <summary>A completed CPU scope, numeric counter or diagnostic; GPU timing requires its own provider.</summary>
/// <param name="Id">Unique event identity within the source.</param>
/// <param name="ParentId">Parent scope identity, or zero.</param>
/// <param name="Track">Worker or logical track identity.</param>
/// <param name="Kind">scope, counter or diagnostic.</param>
/// <param name="Name">Registered event name.</param>
/// <param name="Start">Start timestamp.</param>
/// <param name="End">End timestamp in the same domain.</param>
/// <param name="Value">Counter value; null for other events.</param>
/// <param name="Message">Diagnostic text or counter unit.</param>
/// <param name="FlowId">Optional cross-track correlation key.</param>
public readonly record struct ProfileEvent(long Id, long ParentId, int Track, string Kind, string Name,
    long Start, long End, double? Value = null, string? Message = null, long FlowId = 0);

/// <summary>Copied inspection data; keys are producer-local and qualified by capture/source identity.</summary>
/// <param name="Id">Inspected object's local identity.</param>
/// <param name="ParentId">Logical parent identity, or empty.</param>
/// <param name="Category">Inspector category.</param>
/// <param name="Name">Property or object name.</param>
/// <param name="Value">Copied display value, never a live runtime object.</param>
/// <param name="X">Optional rectangle x.</param>
/// <param name="Y">Optional rectangle y.</param>
/// <param name="Width">Optional rectangle width.</param>
/// <param name="Height">Optional rectangle height.</param>
public readonly record struct ProfileItem(string Id, string ParentId, string Category, string Name, string Value,
    float? X = null, float? Y = null, float? Width = null, float? Height = null);

/// <summary>Owns one completed frame; eviction and provider disposal cannot mutate its values.</summary>
public sealed class ProfileCapture
{
    internal ProfileCapture(Guid session, ProfileSourceInfo source, long frame, long generation,
        long start, long end, ProfileEvent[] events, ProfileItem[] items, long dropped, long bytes)
    {
        Session = session;
        Source = source;
        Frame = frame;
        Generation = generation;
        Start = start;
        End = end;
        Events = Array.AsReadOnly(events);
        Items = Array.AsReadOnly(items);
        Dropped = dropped;
        RetainedBytes = bytes;
    }

    /// <summary>Session identity preserved by export.</summary>
    public Guid Session { get; }
    /// <summary>Copied producer metadata.</summary>
    public ProfileSourceInfo Source { get; }
    /// <summary>Producer frame number.</summary>
    public long Frame { get; }
    /// <summary>Capture generation independent of a producer's frame numbering.</summary>
    public long Generation { get; }
    /// <summary>Frame start timestamp.</summary>
    public long Start { get; }
    /// <summary>Frame end timestamp.</summary>
    public long End { get; }
    /// <summary>Frame length in clock ticks.</summary>
    public long Duration => End - Start;
    /// <summary>Completed immutable event sequence.</summary>
    public ReadOnlyCollection<ProfileEvent> Events { get; }
    /// <summary>Immutable detailed inspection values.</summary>
    public ReadOnlyCollection<ProfileItem> Items { get; }
    /// <summary>Events/items omitted because of capture limits or unfinished scopes.</summary>
    public long Dropped { get; }
    /// <summary>Conservative payload storage charge used for retention limits.</summary>
    public long RetainedBytes { get; }
}

/// <summary>Bounds each source independently so a busy producer cannot consume another's history.</summary>
public sealed record ProfileLimits
{
    /// <summary>Maximum registered producers per session.</summary>
    public int Sources { get; init; } = 32;
    /// <summary>Maximum completed frames per source.</summary>
    public int Frames { get; init; } = 120;
    /// <summary>Maximum retained payload bytes per source, including the active frame.</summary>
    public int Bytes { get; init; } = 16 * 1024 * 1024;
    /// <summary>Maximum events and open scopes per active frame.</summary>
    public int Events { get; init; } = 4096;
    /// <summary>Maximum inspection properties per active frame.</summary>
    public int Items { get; init; } = 16384;
    /// <summary>Maximum characters in any producer-supplied string.</summary>
    public int TextLength { get; init; } = 4096;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(Sources, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(Frames, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(Bytes, 1024);
        ArgumentOutOfRangeException.ThrowIfLessThan(Events, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(Items, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(TextLength, 1);
    }
}
