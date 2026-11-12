using System.Diagnostics;

namespace Guinevere.Profiling;

/// <summary>Describes a clock domain without promising precision beyond its provider's capability.</summary>
/// <param name="Domain">Identity shared only by timestamps from this clock.</param>
/// <param name="Frequency">Integer ticks per second.</param>
/// <param name="HighResolution">Provider-reported capability, or null when unknown.</param>
/// <param name="ResolutionTicks">Known effective resolution in ticks, or null when unmeasured.</param>
public sealed record ProfileClockInfo(Guid Domain, long Frequency, bool? HighResolution = null,
    long? ResolutionTicks = null)
{
    /// <summary>Converts a tick count in this clock's domain to seconds.</summary>
    public double ToSeconds(long ticks) => (double)ticks / Frequency;

    /// <summary>Converts a tick count in this clock's domain to milliseconds.</summary>
    public double ToMilliseconds(long ticks) => ticks * 1000.0 / Frequency;
}

/// <summary>Supplies monotonic integer timestamps and their declared domain.</summary>
public interface IProfileClock
{
    /// <summary>Clock metadata required to interpret timestamps.</summary>
    ProfileClockInfo Info { get; }

    /// <summary>Reads the current timestamp in this clock's domain.</summary>
    long GetTimestamp();
}

/// <summary>Uses the process performance counter; effective accuracy remains unmeasured.</summary>
public sealed class ProfileClock : IProfileClock
{
    /// <summary>Process-wide clock domain for local CPU measurements.</summary>
    public static ProfileClock Instance { get; } = new();

    /// <inheritdoc />
    public ProfileClockInfo Info { get; } = new(Guid.NewGuid(), Stopwatch.Frequency, Stopwatch.IsHighResolution);

    /// <inheritdoc />
    public long GetTimestamp() => Stopwatch.GetTimestamp();
}
