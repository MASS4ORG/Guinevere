using System.Text;
using System.Text.Json.Nodes;
using Guinevere.Profiling;

namespace Guinevere.Tests.Profiling.Core;

public sealed class ProfileSessionTests
{
    [Fact]
    public void MixedProducersShareContractWithIndependentDomainsAndTracks()
    {
        using var session = new ProfileSession();
        using var gui = session.Register("UI", "gui", new FakeClock());
        using var engine = session.Register("Engine", "engine", new FakeClock());
        gui.BeginFrame(10);
        engine.BeginFrame(42);
        using (var root = engine.BeginScope("update"))
        {
            Parallel.For(0, 8, track =>
            {
                using var worker = engine.BeginScope("worker", track + 1, root.Id, 90);
                engine.Counter("entities", track, "count", track + 1);
            });
        }
        gui.Inspect(new ProfileItem("1", "", "layout", "button", "Accept", 1, 2, 100, 24));
        gui.Diagnostic("layout", "source.cs:12");
        var guiFrame = gui.EndFrame();
        var engineFrame = engine.EndFrame();
        Assert.Equal(guiFrame.Session, engineFrame.Session);
        Assert.NotEqual(guiFrame.Source.Id, engineFrame.Source.Id);
        Assert.NotEqual(guiFrame.Source.Clock.Domain, engineFrame.Source.Clock.Domain);
        Assert.Equal(8, engineFrame.Events.Count(entry => entry.FlowId == 90));
        Assert.All(engineFrame.Events, entry => Assert.True(entry.End >= entry.Start));
        Assert.Equal("Accept", Assert.Single(guiFrame.Items).Value);
        Assert.Contains(guiFrame.Events, entry => entry.Kind == "diagnostic" && entry.Message == "source.cs:12");
    }

    [Fact]
    public void CountAndByteBudgetsExposeLossAndOwnedCapturesSurviveEviction()
    {
        using var session = new ProfileSession(new ProfileLimits { Frames = 1, Bytes = 2048, Events = 2, Items = 2 });
        using var source = session.Register("UI", "gui");
        source.BeginFrame(1);
        source.Inspect(new ProfileItem("1", "", "state", "value", "original"));
        source.Counter("first", 1, "count");
        source.Counter("second", 2, "count");
        source.Counter("third", 3, "count");
        source.Inspect(new ProfileItem("2", "", "state", "oversized", new string('x', 4097)));
        var original = source.EndFrame();
        Assert.Equal(2, original.Dropped);
        source.BeginFrame(2);
        source.Inspect(new ProfileItem("1", "", "state", "value", "changed"));
        var next = source.EndFrame();
        Assert.Same(next, Assert.Single(source.GetHistory()));
        Assert.True(next.RetainedBytes <= session.Limits.Bytes);
        source.Dispose();
        Assert.Empty(session.GetSources());
        Assert.Equal("original", original.Items[0].Value);
        Assert.Throws<NotSupportedException>(() => ((IList<ProfileItem>)original.Items)[0] = default);
    }

    [Fact]
    public void SourceLimitsAreFairAndChurnReleasesRegistration()
    {
        using var session = new ProfileSession(new ProfileLimits { Sources = 2, Frames = 2, Bytes = 1024 });
        using var quiet = session.Register("quiet", "gui");
        quiet.BeginFrame(1);
        var quietFrame = quiet.EndFrame();
        for (var i = 0; i < 30; i++)
        {
            using var busy = session.Register("busy", "engine");
            Assert.Throws<InvalidOperationException>(() => session.Register("overflow", "engine"));
            for (var frame = 0; frame < 10; frame++)
            {
                busy.BeginFrame(frame);
                for (var eventIndex = 0; eventIndex < 20; eventIndex++)
                {
                    busy.Counter("work", 1, "count");
                }
                var capture = busy.EndFrame();
                Assert.True(capture.Dropped > 0);
                Assert.True(busy.GetHistory().Sum(item => item.RetainedBytes) <= 1024);
            }
        }
        Assert.Same(quietFrame, Assert.Single(quiet.GetHistory()));
        Assert.Single(session.GetSources());
    }

    [Fact]
    public void ScopeCopiesAndAbandonedFramesCannotWriteIntoLaterFrames()
    {
        using var session = new ProfileSession();
        using var source = session.Register("test", "engine");
        Assert.Throws<InvalidOperationException>(() => source.Counter("invalid", 1, "count"));
        source.BeginFrame(1);
        Assert.Throws<InvalidOperationException>(() => source.BeginFrame(2));
        var scope = source.BeginScope("once");
        var copy = scope;
        scope.Dispose();
        copy.Dispose();
        var unfinished = source.BeginScope("unfinished");
        var capture = source.EndFrame();
        Assert.Single(capture.Events);
        Assert.Equal(1, capture.Dropped);
        source.BeginFrame(2);
        unfinished.Dispose();
        source.CancelFrame();
        source.BeginFrame(3);
        Assert.Empty(source.EndFrame().Events);
        source.Dispose();
        source.Dispose();
        Assert.Throws<ObjectDisposedException>(() => source.BeginFrame(4));
    }

    [Fact]
    public void ClockAndLimitValidationRejectFabricatedMeasurements()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProfileSession(new ProfileLimits { Frames = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProfileSession(new ProfileLimits { Bytes = 10 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProfileSession(new ProfileLimits { Events = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProfileSession(new ProfileLimits { Items = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProfileSession(new ProfileLimits { Sources = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProfileSession(new ProfileLimits { TextLength = 0 }));
        using var session = new ProfileSession(new ProfileLimits { TextLength = 10 });
        Assert.Throws<ArgumentException>(() => session.Register("", "gui"));
        Assert.Throws<ArgumentException>(() => session.Register(new string('x', 11), "gui"));
        Assert.Throws<ArgumentException>(() => session.Register("x", "gui", new FakeClock(0)));
        using var source = session.Register("coarse", "engine", new FakeClock(100));
        source.BeginFrame(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.Counter("invalid", double.NaN, "count"));
        source.Inspect(new ProfileItem("1", "", "layout", "rect", "", float.NaN));
        Assert.Equal(1, source.EndFrame().Dropped);
        session.Dispose();
        Assert.Throws<ObjectDisposedException>(() => session.Register("x", "engine"));
    }

    [Fact]
    public void ExportRoundtripsOwnedDataAndIgnoresUnknownOptionalProperties()
    {
        var capture = Capture();
        using var stream = new MemoryStream();
        ProfileExport.Write(stream, capture);
        var node = JsonNode.Parse(stream.ToArray())!;
        node["OptionalFutureProperty"] = "ignored by version 1";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(node.ToJsonString()));
        var imported = ProfileExport.Read(input);
        Assert.Equal(capture.Session, imported.Session);
        Assert.Equal(capture.Source, imported.Source);
        Assert.Equal(capture.Events, imported.Events);
        Assert.Equal(capture.Items, imported.Items);
    }

    [Theory]
    [InlineData("Schema", "2")]
    [InlineData("Session", "\"00000000-0000-0000-0000-000000000000\"")]
    [InlineData("Source", "null")]
    [InlineData("Generation", "0")]
    [InlineData("Dropped", "-1")]
    [InlineData("RetainedBytes", "-1")]
    [InlineData("Events", "null")]
    [InlineData("Items", "null")]
    [InlineData("Events", "[{}]")]
    [InlineData("Items", "[{}]")]
    public void ImportRejectsInvalidRequiredHeaders(string property, string value)
    {
        using var output = new MemoryStream();
        ProfileExport.Write(output, Capture());
        var node = JsonNode.Parse(output.ToArray())!;
        node[property] = JsonNode.Parse(value);
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(node.ToJsonString()));
        Assert.Throws<InvalidDataException>(() => ProfileExport.Read(input));
    }

    [Fact]
    public void ImportRejectsAnInvalidClockFrequency()
    {
        using var output = new MemoryStream();
        ProfileExport.Write(output, Capture());
        var node = JsonNode.Parse(output.ToArray())!;
        node["Source"]!["Clock"]!["Frequency"] = 0;
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(node.ToJsonString()));
        Assert.Throws<InvalidDataException>(() => ProfileExport.Read(input));
    }
    [Fact]
    public void EscapedDefaultBudgetCaptureRoundtripsWithinTheImportLimit()
    {
        using var session = new ProfileSession();
        using var source = session.Register("escaped", "engine");
        source.BeginFrame(1);
        var text = new string('\u0001', 4096);
        for (var index = 0; index < 1800; index++)
        {
            source.Inspect(new ProfileItem("1", "", "state", "escaped", text));
        }
        var capture = source.EndFrame();
        Assert.Equal(0, capture.Dropped);
        using var output = new MemoryStream();
        ProfileExport.Write(output, capture);
        Assert.True(output.Length > 32 * 1024 * 1024);
        output.Position = 0;
        Assert.Equal(capture.Items, ProfileExport.Read(output).Items);
    }
    [Fact]
    public void ImportRejectsAnOverflowingJsonMeasurement()
    {
        using var output = new MemoryStream();
        ProfileExport.Write(output, Capture());
        var node = JsonNode.Parse(output.ToArray())!;
        node["Events"]![0]!["Value"] = JsonNode.Parse("1e400");
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(node.ToJsonString()));
        Assert.Throws<InvalidDataException>(() => ProfileExport.Read(input));
    }
    [Fact]
    public void ClockRegressionIsRejectedAndCancellationAllowsRecovery()
    {
        var clock = new FakeClock();
        using var session = new ProfileSession();
        using var source = session.Register("clock", "engine", clock);
        source.BeginFrame(1);
        var scope = source.BeginScope("scope");
        clock.Value = -10000;
        Assert.Throws<InvalidOperationException>(() => scope.Dispose());
        Assert.Throws<InvalidOperationException>(() => source.EndFrame());
        source.CancelFrame();
        clock.Value = 10000;
        source.BeginFrame(2);
        Assert.Empty(source.EndFrame().Events);
    }

    [Fact]
    public void WarmedSynchronousScopeCollectionAllocatesZeroBytes()
    {
        using var session = new ProfileSession();
        using var source = session.Register("allocation", "engine");
        source.BeginFrame(1);
        MeasureScopes(source);
        source.EndFrame();
        source.BeginFrame(2);
        Assert.Equal(0, MeasureScopes(source));
        source.EndFrame();
    }

    static long MeasureScopes(ProfileSource source)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1024; i++)
        {
            using var scope = source.BeginScope("update");
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    static ProfileCapture Capture()
    {
        using var session = new ProfileSession();
        using var source = session.Register("capture", "engine", new FakeClock());
        source.BeginFrame(1);
        using (source.BeginScope("work"))
        {
            source.Counter("entities", 42, "count");
        }
        source.Diagnostic("test", "source.cs:20");
        source.Inspect(new ProfileItem("1", "", "state", "value", "copied"));
        return source.EndFrame();
    }

    sealed class FakeClock(long frequency = 1000) : IProfileClock
    {
        internal long Value;
        public ProfileClockInfo Info { get; } = new(Guid.NewGuid(), frequency, false, 100);
        public long GetTimestamp() => Interlocked.Add(ref Value, 100);
    }
}
