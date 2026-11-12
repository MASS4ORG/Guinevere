namespace Guinevere.Profiling;

/// <summary>Totals for every scope sharing one name across analyzed frames.</summary>
/// <param name="Name">Scope name.</param>
/// <param name="Calls">Number of completed scopes.</param>
/// <param name="TotalTicks">Summed scope length including nested scopes.</param>
/// <param name="SelfTicks">Summed length excluding scopes nested on the same track.</param>
/// <param name="MinTicks">Shortest single scope.</param>
/// <param name="MaxTicks">Longest single scope.</param>
public readonly record struct ScopeSummary(string Name, long Calls, long TotalTicks, long SelfTicks,
    long MinTicks, long MaxTicks)
{
    /// <summary>Average scope length in ticks.</summary>
    public double MeanTicks => Calls == 0 ? 0 : (double)TotalTicks / Calls;
}

/// <summary>Frame length distribution of one source's captures; percentiles use the nearest-rank method.</summary>
/// <param name="Frames">Number of analyzed frames.</param>
/// <param name="MinTicks">Shortest frame.</param>
/// <param name="MaxTicks">Longest frame.</param>
/// <param name="MeanTicks">Average frame length.</param>
/// <param name="MedianTicks">50th percentile.</param>
/// <param name="P95Ticks">95th percentile.</param>
/// <param name="P99Ticks">99th percentile.</param>
public readonly record struct FrameStatistics(int Frames, long MinTicks, long MaxTicks, double MeanTicks,
    long MedianTicks, long P95Ticks, long P99Ticks);

/// <summary>Headless analysis over completed captures; all captures must share one clock domain.</summary>
public static class ProfileAnalysis
{
    /// <summary>Aggregates scopes by name, longest total first; self time subtracts same-track nested scopes.</summary>
    public static IReadOnlyList<ScopeSummary> Summarize(IEnumerable<ProfileCapture> captures)
    {
        var totals = new Dictionary<string, Accumulator>();
        foreach (var capture in Checked(captures))
        {
            AddCapture(totals, capture);
        }
        return [.. totals.Select(static pair => pair.Value.ToSummary(pair.Key))
            .OrderByDescending(static entry => entry.TotalTicks)
            .ThenBy(static entry => entry.Name, StringComparer.Ordinal)];
    }

    /// <summary>Computes the frame length distribution; an empty input yields zero frames.</summary>
    public static FrameStatistics Frames(IEnumerable<ProfileCapture> captures)
    {
        var lengths = Checked(captures).Select(static capture => capture.Duration).Order().ToArray();
        if (lengths.Length == 0)
        {
            return default;
        }
        return new FrameStatistics(lengths.Length, lengths[0], lengths[^1], lengths.Average(),
            Percentile(lengths, 50), Percentile(lengths, 95), Percentile(lengths, 99));
    }

    /// <summary>Returns frames longer than the given multiple of the median frame, in input order.</summary>
    public static IReadOnlyList<ProfileCapture> FindHitches(IEnumerable<ProfileCapture> captures,
        double medianFactor = 2.0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(medianFactor, 0);
        var list = Checked(captures).ToArray();
        var limit = Frames(list).MedianTicks * medianFactor;
        return [.. list.Where(capture => capture.Duration > limit)];
    }

    static long Percentile(long[] sorted, int percent) =>
        sorted[Math.Max(0, (int)Math.Ceiling(percent / 100.0 * sorted.Length) - 1)];

    static ProfileCapture[] Checked(IEnumerable<ProfileCapture> captures)
    {
        ArgumentNullException.ThrowIfNull(captures);
        var list = captures.ToArray();
        if (list.Select(static capture => capture.Source.Clock.Domain).Distinct().Skip(1).Any())
        {
            throw new ArgumentException("Captures from different clock domains cannot be compared.", nameof(captures));
        }
        return list;
    }

    static void AddCapture(Dictionary<string, Accumulator> totals, ProfileCapture capture)
    {
        var scopes = capture.Events.Where(static entry => entry.Kind == "scope");
        foreach (var track in scopes.GroupBy(static entry => entry.Track))
        {
            var ordered = track.OrderBy(static entry => entry.Start).ThenByDescending(static entry => entry.End)
                .ThenBy(static entry => entry.Id).ToArray();
            var selves = SelfTicks(ordered);
            for (var i = 0; i < ordered.Length; i++)
            {
                totals.TryGetValue(ordered[i].Name, out var current);
                totals[ordered[i].Name] = current.Add(ordered[i].End - ordered[i].Start, selves[i]);
            }
        }
    }

    static long[] SelfTicks(ProfileEvent[] ordered)
    {
        var self = new long[ordered.Length];
        var open = new Stack<int>();
        for (var i = 0; i < ordered.Length; i++)
        {
            var length = ordered[i].End - ordered[i].Start;
            self[i] = length;
            while (open.Count > 0 && ordered[open.Peek()].End <= ordered[i].Start)
            {
                open.Pop();
            }
            if (open.Count > 0 && ordered[i].End <= ordered[open.Peek()].End)
            {
                self[open.Peek()] -= length;
            }
            open.Push(i);
        }
        return self;
    }

    readonly record struct Accumulator(long Calls, long Total, long Self, long Min, long Max)
    {
        public Accumulator Add(long length, long self) => Calls == 0
            ? new Accumulator(1, length, self, length, length)
            : new Accumulator(Calls + 1, Total + length, Self + self, Math.Min(Min, length), Math.Max(Max, length));

        public ScopeSummary ToSummary(string name) => new(name, Calls, Total, Self, Min, Max);
    }
}
