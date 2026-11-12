using Guinevere.Profiling;

namespace Guinevere.DevTools;

/// <summary>Timing-only copy of a completed frame, kept after its detailed capture leaves the source history.</summary>
/// <param name="Source">Producer identity.</param>
/// <param name="Name">Producer display name.</param>
/// <param name="Frame">Producer frame number.</param>
/// <param name="Generation">Capture generation used for selection.</param>
/// <param name="Start">Frame start timestamp.</param>
/// <param name="End">Frame end timestamp.</param>
/// <param name="Frequency">Clock ticks per second.</param>
/// <param name="Scopes">Completed CPU scopes only.</param>
/// <param name="Dropped">Events or items omitted by capture limits.</param>
sealed record FrameRecord(Guid Source, string Name, long Frame, long Generation, long Start, long End,
    long Frequency, ProfileEvent[] Scopes, long Dropped)
{
    /// <summary>Frame length in milliseconds.</summary>
    public double Milliseconds => ToMilliseconds(End - Start);

    /// <summary>Converts a tick span of this frame's clock to milliseconds.</summary>
    public double ToMilliseconds(long ticks) => ticks * 1000d / Frequency;

    /// <summary>Copies the timing part of a capture without retaining its inspector items.</summary>
    public static FrameRecord From(ProfileCapture capture) => new(capture.Source.Id, capture.Source.Name,
        capture.Frame, capture.Generation, capture.Start, capture.End, capture.Source.Clock.Frequency,
        [.. capture.Events.Where(static entry => entry.Kind == "scope")], capture.Dropped);
}
