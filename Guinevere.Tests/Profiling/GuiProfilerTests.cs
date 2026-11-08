using Guinevere.Profiling;
using MASS4.Profiling;

namespace Guinevere.Tests.Profiling;

public sealed class GuiProfilerTests
{
    [Fact]
    public void CapturesAppliedStylesTextGeometryAndSafeStateWithSourceLocations()
    {
        using var fixture = new ProfilerFixture();
        using var session = new ProfileSession();
        using var profiler = new GuiProfiler(fixture.Gui, session, "Application") { Detailed = true };
        fixture.Gui.StyleSheets.Add(StyleSheet.Parse("button { color = #123456; padding = 4; }",
            new StyleSheetOptions { SourceName = "application.pss" }));
        var text = "Hello";
        fixture.Input.PressKey(KeyboardKey.A);
        void Draw()
        {
            using var node = fixture.Gui.StyledNode("button", id: "button").Width(100).Height(40).Enter();
            fixture.Gui.SetData(42, "answer");
            fixture.Gui.SetValue(true, "enabled");
            fixture.Gui.SetValue<string?>(null, "empty");
            fixture.Gui.SetValue(KeyboardKey.A, "enum");
            fixture.Gui.ControlState("control", static () => "safe text");
            fixture.Gui.GetValue(new OpaqueState(), "opaque");
            fixture.Gui.DrawText(text);
            fixture.Gui.DrawRect(new Rect(0, 0, 12, 12), Color.Blue);
        }
        fixture.Frame(Draw);
        var capture = Assert.IsType<ProfileCapture>(profiler.Latest);
        Assert.Contains(capture.Events, entry => entry.Kind == "scope" && entry.Name == "layout");
        Assert.Contains(capture.Events, entry => entry.Name == "draw.canvas-submissions" && entry.Value > 0);
        AssertCapturedStyleAndState(capture);
        Assert.Contains(capture.Items, item => item.Category == "layout" && item.Value == "button" && item.Width == 100);
        Assert.Contains(capture.Items, item => item.Category == "draw");
        text = "Changed";
        fixture.Frame(Draw);
        Assert.Contains(capture.Items, item => item.Category == "text" && item.Value == "Hello");
        Assert.DoesNotContain(capture.Items, item => item.Value == "Changed");
    }

    static void AssertCapturedStyleAndState(ProfileCapture capture)
    {
        Assert.Contains(capture.Items, static item => item.Category == "style.render" && item.Name == "color" && item.Value == "#123456");
        Assert.Contains(capture.Items, static item => item.Category == "style.source" && item.Value.Contains("application.pss", StringComparison.Ordinal));
        Assert.Contains(capture.Items, static item => item.Category == "text" && item.Value == "Hello");
        Assert.Contains(capture.Items, static item => item.Category == "state" && item.Value == "42");
        Assert.Contains(capture.Items, static item => item.Category == "state" && item.Value.Contains("opaque", StringComparison.Ordinal));
    }

    [Fact]
    public void SummaryNeedsNoViewerAndCountsThreadSafeWorkRequests()
    {
        using var fixture = new ProfilerFixture();
        using var session = new ProfileSession();
        using var profiler = new GuiProfiler(fixture.Gui, session, "Summary");
        Parallel.For(0, 20, _ => fixture.Gui.RequestFrame());
        fixture.Gui.RequestFrameIn(.1);
        fixture.Frame(() => fixture.Gui.Node(30, 20));
        var capture = profiler.Latest!;
        Assert.Empty(capture.Items);
        Assert.Contains(capture.Events, entry => entry.Name == "work.immediate-requests" && entry.Value == 20);
        Assert.Contains(capture.Events, entry => entry.Name == "work.scheduled-requests" && entry.Value == 1);
        Assert.Contains(capture.Events, entry => entry.Name == "layout.nodes" && entry.Value == 2);
        Assert.Throws<InvalidOperationException>(() => new GuiProfiler(fixture.Gui, session, "Duplicate"));
        profiler.Dispose();
        Assert.Null(fixture.Gui.FrameObserver);
        Assert.Empty(session.GetSources());
        fixture.Frame(() => fixture.Gui.Node());
    }

    [Fact]
    public void FailurePreservesLastGoodCaptureAndAllowsTheNextFrame()
    {
        using var fixture = new ProfilerFixture();
        using var session = new ProfileSession();
        using var profiler = new GuiProfiler(fixture.Gui, session, "Target");
        fixture.Frame(() => fixture.Gui.Node());
        var good = profiler.Latest;
        Assert.Throws<InvalidOperationException>(() => fixture.Frame(static () => throw new InvalidOperationException("target failure")));
        Assert.Same(good, profiler.Latest);
        Assert.Contains("target failure", profiler.LastError!, StringComparison.Ordinal);
        Assert.Null(fixture.Gui.Canvas);
        fixture.Frame(() => fixture.Gui.Node());
        Assert.NotSame(good, profiler.Latest);
        Assert.Null(profiler.LastError);
    }

    [Fact]
    public void ExplicitNamesInDifferentParentsKeepDistinctCaptureIdentities()
    {
        using var fixture = new ProfilerFixture();
        using var session = new ProfileSession();
        using var profiler = new GuiProfiler(fixture.Gui, session, "Identity") { Detailed = true };
        void Draw()
        {
            for (var index = 0; index < 2; index++)
            {
                using var parent = fixture.Gui.Node(id: $"parent-{index}").Enter();
                fixture.Gui.StyledNode("button", id: "same-name");
            }
        }
        fixture.Frame(Draw);
        var duplicates = profiler.Latest!.Items.Where(item => item.Category == "layout" && item.Value == "same-name").ToArray();
        Assert.Equal(2, duplicates.Length);
        Assert.NotEqual(duplicates[0].Id, duplicates[1].Id);
        Assert.NotEqual(duplicates[0].ParentId, duplicates[1].ParentId);
    }

    sealed class OpaqueState
    {
        public override string ToString() => throw new InvalidOperationException("Arbitrary ToString must not run.");
    }
}
