using System.Globalization;
using Guinevere.DevTools;
using MASS4.Profiling;

namespace Guinevere.Tests.Profiling;

public sealed class ProfilerViewFramesTests
{
    [Fact]
    public void StripKeepsTimingAfterCaptureEvictionAndClickSelectsAFrame()
    {
        using var host = new ProfilerFixture(1100, 800);
        using var session = new ProfileSession(new ProfileLimits { Frames = 2 });
        using var view = new ProfilerView(session);
        view.Gui.Input = host.Input;
        view.Gui.ConfigureFonts(host.Font);
        using var engine = session.Register("Engine", "engine");
        var generations = new List<long>();
        for (var frame = 1; frame <= 6; frame++)
        {
            engine.BeginFrame(frame);
            using (var update = engine.BeginScope("Update", track: 1))
            {
                using (engine.BeginScope("Physics", track: 1, parentId: update.Id)) { }
            }
            generations.Add(engine.EndFrame().Generation);
            Frame(view, host);
        }
        Assert.Equal(2, engine.GetHistory().Count);

        view.Command("frame", generations[0].ToString(CultureInfo.InvariantCulture));
        view.Command("panel", "timeline");
        Frame(view, host);
        Assert.Null(view.Capture);
        Assert.False(view.FollowLatest);
        Frame(view, host);

        view.Command("frame", generations[^1].ToString(CultureInfo.InvariantCulture));
        Frame(view, host);
        Assert.Equal(generations[^1], view.Capture!.Generation);

        var strip = Assert.IsType<LayoutNode>(FindNode(view.Gui, "profiler-frames")).Rect;
        var slot = strip.W / 120;
        host.Input.MoveTo(strip.X + strip.W - slot * 1.5f, strip.Y + strip.H - 2);
        Frame(view, host);
        host.Input.PressButton();
        Frame(view, host);
        host.Input.ReleaseButton();
        Frame(view, host);
        Assert.Equal(generations[^2], view.Capture!.Generation);
    }

    [Fact]
    public void ClickingAFlameScopeCopiesItsTotalAndSelfTime()
    {
        using var host = new ProfilerFixture(1100, 800);
        var clock = new StepClock();
        using var session = new ProfileSession();
        using var view = new ProfilerView(session);
        view.Gui.Input = host.Input;
        view.Gui.ConfigureFonts(host.Font);
        using var engine = session.Register("Engine", "engine", clock);
        engine.BeginFrame(1);
        using (var update = engine.BeginScope("Update"))
        {
            using (engine.BeginScope("Physics", parentId: update.Id)) { }
        }
        engine.EndFrame();
        view.Command("panel", "timeline");
        Frame(view, host);
        Frame(view, host);

        var chart = Assert.IsType<LayoutNode>(FindNode(view.Gui, "profiler-timeline")).Rect;
        host.Input.MoveTo(chart.X + chart.W / 2, chart.Y + 16 + 9);
        Frame(view, host);
        host.Input.PressButton();
        Frame(view, host);
        host.Input.ReleaseButton();
        view.Command("copy");
        Frame(view, host);
        var copied = host.Input.GetClipboardText();
        Assert.StartsWith("Update · track 0 · 3.000 ms total · 2.000 ms self", copied, StringComparison.Ordinal);
    }

    static void Frame(ProfilerView view, ProfilerFixture host)
    {
        view.Gui.UpdateFrameTime(.016);
        view.Gui.RenderFrame(host.Surface.Canvas, view.Draw);
        host.Input.NewFrame();
    }

    static LayoutNode? FindNode(Gui gui, string id)
    {
        var nodes = new Stack<LayoutNode>();
        nodes.Push(gui.RootNode!);
        while (nodes.TryPop(out var node))
        {
            if (node.Id == id) { return node; }
            foreach (var child in node.ChildNodes) { nodes.Push(child); }
        }
        return null;
    }

    /// <summary>Advances one millisecond per read so scope lengths are exact.</summary>
    sealed class StepClock : IProfileClock
    {
        long _now;
        public ProfileClockInfo Info { get; } = new(Guid.NewGuid(), 1000);
        public long GetTimestamp() => _now++;
    }
}
