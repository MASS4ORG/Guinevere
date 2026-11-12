using System.Text.Json;
using Guinevere.Profiling;

namespace Guinevere.Tests.Profiling.Core;

public sealed class ProfileAnalysisTests
{
    [Fact]
    public void SummarizeSplitsSelfTimeFromSameTrackNestedScopes()
    {
        var clock = new StepClock(1000);
        using var session = new ProfileSession();
        using var source = session.Register("Engine", "engine", clock);
        source.BeginFrame(1);
        using (source.BeginScope("update"))
        {
            clock.Advance(10);
            using (source.BeginScope("physics"))
            {
                clock.Advance(30);
            }
            clock.Advance(20);
        }
        var capture = source.EndFrame();
        var summary = ProfileAnalysis.Summarize([capture]).ToDictionary(static entry => entry.Name);
        Assert.Equal(60, summary["update"].TotalTicks);
        Assert.Equal(30, summary["update"].SelfTicks);
        Assert.Equal(30, summary["physics"].TotalTicks);
        Assert.Equal(30, summary["physics"].SelfTicks);
    }

    [Fact]
    public void SummarizeAggregatesCallsAcrossFramesAndTracks()
    {
        var clock = new StepClock(1000);
        using var session = new ProfileSession();
        using var source = session.Register("Engine", "engine", clock);
        var captures = new List<ProfileCapture>();
        foreach (var length in new long[] { 10, 30 })
        {
            source.BeginFrame(length);
            using (source.BeginScope("job", track: (int)length))
            {
                clock.Advance(length);
            }
            captures.Add(source.EndFrame());
        }
        var job = Assert.Single(ProfileAnalysis.Summarize(captures));
        Assert.Equal(2, job.Calls);
        Assert.Equal(40, job.TotalTicks);
        Assert.Equal(10, job.MinTicks);
        Assert.Equal(30, job.MaxTicks);
        Assert.Equal(20, job.MeanTicks);
    }

    [Fact]
    public void FramesReportsNearestRankPercentilesAndHitches()
    {
        var clock = new StepClock(1000);
        using var session = new ProfileSession();
        using var source = session.Register("Game", "engine", clock);
        var captures = new List<ProfileCapture>();
        for (var frame = 1; frame <= 100; frame++)
        {
            source.BeginFrame(frame);
            clock.Advance(frame == 100 ? 1000 : 16);
            captures.Add(source.EndFrame());
        }
        var statistics = ProfileAnalysis.Frames(captures);
        Assert.Equal(100, statistics.Frames);
        Assert.Equal(16, statistics.MedianTicks);
        Assert.Equal(16, statistics.P95Ticks);
        Assert.Equal(16, statistics.P99Ticks);
        Assert.Equal(1000, statistics.MaxTicks);
        Assert.Equal(100, Assert.Single(ProfileAnalysis.FindHitches(captures)).Frame);
        Assert.Equal(default, ProfileAnalysis.Frames([]));
    }

    [Fact]
    public void AnalysisRejectsMixedClockDomains()
    {
        using var session = new ProfileSession();
        using var first = session.Register("A", "gui", new StepClock(1000));
        using var second = session.Register("B", "gui", new StepClock(1000));
        first.BeginFrame(1);
        second.BeginFrame(1);
        var captures = new[] { first.EndFrame(), second.EndFrame() };
        Assert.Throws<ArgumentException>(() => ProfileAnalysis.Frames(captures));
        Assert.Throws<ArgumentException>(() => ProfileAnalysis.Summarize(captures));
    }

    [Fact]
    public void ClockInfoConvertsTicksToTime()
    {
        var info = new ProfileClockInfo(Guid.NewGuid(), 2000);
        Assert.Equal(0.5, info.ToSeconds(1000));
        Assert.Equal(500, info.ToMilliseconds(1000));
    }

    [Fact]
    public void ChromeTraceContainsSpansCountersInstantsAndFlows()
    {
        var clock = new StepClock(1_000_000);
        using var session = new ProfileSession();
        using var source = session.Register("Engine", "engine", clock);
        source.BeginFrame(7);
        using (source.BeginScope("update", track: 1, flowId: 5))
        {
            clock.Advance(100);
        }
        using (source.BeginScope("render", track: 2, flowId: 5))
        {
            clock.Advance(50);
        }
        source.Counter("entities", 42, "count");
        source.Diagnostic("warn", "slow");
        var capture = source.EndFrame();
        using var stream = new MemoryStream();
        ChromeTraceExport.Write(stream, [capture]);
        using var document = JsonDocument.Parse(stream.ToArray());
        var events = document.RootElement.GetProperty("traceEvents").EnumerateArray().ToArray();
        static string Phase(JsonElement entry) => entry.GetProperty("ph").GetString()!;
        Assert.Equal(3, events.Count(static entry => Phase(entry) == "X"));
        Assert.Contains(events, static entry => Phase(entry) == "X" && entry.GetProperty("name").GetString() == "update"
            && entry.GetProperty("dur").GetDouble() == 100);
        Assert.Contains(events, static entry => Phase(entry) == "C"
            && entry.GetProperty("name").GetString() == "entities (count)"
            && entry.GetProperty("args").GetProperty("value").GetDouble() == 42);
        Assert.Contains(events, static entry => Phase(entry) == "i");
        Assert.Equal(["s", "f"], [.. events.Select(Phase).Where(static phase => phase is "s" or "t" or "f")]);
        Assert.Contains(events,
            static entry => Phase(entry) == "M" && entry.GetProperty("name").GetString() == "process_name");
    }

    sealed class StepClock(long frequency) : IProfileClock
    {
        long _value;
        public ProfileClockInfo Info { get; } = new(Guid.NewGuid(), frequency, false, 1);
        public long GetTimestamp() => Volatile.Read(ref _value);
        public void Advance(long ticks) => Interlocked.Add(ref _value, ticks);
    }
}
