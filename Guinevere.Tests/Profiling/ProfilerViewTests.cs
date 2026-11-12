using Guinevere.DevTools;
using Guinevere.Profiling;

namespace Guinevere.Tests.Profiling;

public sealed class ProfilerViewTests
{
    [Fact]
    public void ViewerConsumesExportedCapturesAcrossAllPanelsAndBothPssSkins()
    {
        using var target = new ProfilerFixture();
        using var host = new ProfilerFixture(1100, 800);
        using var session = new ProfileSession();
        using var view = new ProfilerView(session);
        view.Gui.Input = host.Input;
        view.Gui.ConfigureFonts(host.Font);
        using var registration = view.Attach(target.Gui, "Application");
        registration.Profiler.Detailed = true;
        target.Frame(() =>
        {
            target.Gui.DrawText("Target text");
            if (target.Gui.Pass == Pass.Pass1Build) { registration.Profiler.Source.Diagnostic("application", "Copied diagnostic"); }
        });
        Frame(view, host);
        Assert.NotNull(view.Capture);
        using var stream = new MemoryStream();
        view.Export(stream);
        stream.Position = 0;
        view.Import(stream);
        var generation = view.Capture!.Generation;
        foreach (var skin in new[] { ProfilerSkins.Light, ProfilerSkins.Dark })
        {
            view.SetSkin(skin);
            foreach (var panel in new[] { "sources", "timeline", "preview", "layout", "interactables", "styles", "draw", "text", "state", "input", "memory", "diagnostics" })
            {
                view.Command("panel", panel);
                Frame(view, host);
                Assert.Equal(panel, view.ActivePanel);
                Assert.Equal(generation, view.Capture.Generation);
            }
        }
        var custom = StyleSheet.Parse("profiler { background-color = #abcdef; color = #010203; }");
        view.SetSkin(custom);
        Assert.Contains(custom, view.Gui.StyleSheets);
        Assert.DoesNotContain(custom, target.Gui.StyleSheets);
        Assert.Null(view.Gui.FrameObserver);
        Assert.Equal(1, registration.Profiler.Latest!.Frame);
        var saved = view.Layout.ToJson();
        Assert.True(view.RestoreLayout(saved));
        Assert.False(view.RestoreLayout("broken"));
        Assert.Throws<ArgumentException>(() => view.Attach(view.Gui, "Self"));
    }

    [Fact]
    public void KeyboardAndAssistiveActionsControlTargetsAndDockVirtualizedItems()
    {
        using var target = new ProfilerFixture();
        using var host = new ProfilerFixture(1100, 800);
        using var session = new ProfileSession();
        using var view = new ProfilerView(session);
        view.Gui.Input = host.Input;
        view.Gui.ConfigureFonts(host.Font);
        var bridge = new Bridge();
        view.Gui.Platform.Register<IAccessibilityTreeSink>(bridge);
        view.Gui.Platform.Register<IAccessibilityActionSource>(bridge);
        using var registration = view.Attach(target.Gui, "Application");
        registration.Profiler.Detailed = true;
        target.Frame(() => target.Gui.Node(40, 30, "test-node"));
        Frame(view, host);
        var item = bridge.Elements.Values.Single(element => element.Role == "treeitem" && element.Name.Contains("test-node", StringComparison.Ordinal));
        Assert.True(item.Virtualized);
        bridge.Actions.Enqueue(new AccessibilityAction("object", item.Key));
        bridge.Actions.Enqueue(new AccessibilityAction("panel", "text"));
        Frame(view, host);
        Assert.Equal(item.Key, view.SelectedObject);
        Assert.Equal("text", view.ActivePanel);
        host.Input.PressKey(KeyboardKey.F8);
        Frame(view, host);
        Assert.True(registration.Controller.Paused);
        host.Input.ReleaseKey(KeyboardKey.F8);
        bridge.Actions.Enqueue(new AccessibilityAction("float"));
        Frame(view, host);
        Assert.Single(view.Layout.Floating);
        bridge.Actions.Enqueue(new AccessibilityAction("dock-right"));
        Frame(view, host);
        Assert.Empty(view.Layout.Floating);
        Assert.Contains(view.Layout.Leaves(), leaf => leaf.PanelIds.Contains("text"));
        Assert.Contains("step", bridge.Elements["profiler"].Actions!);
    }

    [Fact]
    public void IdenticalLocalIdsAndDetachCannotCrossTargetsOrInvalidateHistoricalCapture()
    {
        using var first = new ProfilerFixture();
        using var second = new ProfilerFixture();
        using var host = new ProfilerFixture(1100, 800);
        using var session = new ProfileSession();
        using var view = new ProfilerView(session);
        view.Gui.Input = host.Input;
        view.Gui.ConfigureFonts(host.Font);
        var one = view.Attach(first.Gui, "One");
        var two = view.Attach(second.Gui, "Two");
        one.Profiler.Detailed = two.Profiler.Detailed = true;
        first.Frame(() => first.Gui.Node(10, 10, "same"));
        second.Frame(() => second.Gui.Node(20, 20, "same"));
        Frame(view, host);
        view.Command("source", two.Profiler.Source.Info.Id.ToString());
        Frame(view, host);
        Assert.Equal(two.Profiler.Source.Info.Id, view.Capture!.Source.Id);
        var captured = view.Capture;
        view.Command("pause");
        Frame(view, host);
        Assert.True(two.Controller.Paused);
        Assert.False(one.Controller.Paused);
        Assert.True(view.Detach(two.Profiler.Source.Info.Id));
        Assert.False(view.Detach(two.Profiler.Source.Info.Id));
        Frame(view, host);
        Assert.Same(captured, view.Capture);
        Assert.Contains(captured.Items, item => item.Value == "same" && item.Width == 20);
        Assert.Null(second.Gui.FrameController);
        Assert.Null(second.Gui.FrameObserver);
    }

    [Fact]
    public void KeyboardTreeSelectionRoutesSourcesFramesObjectsAndCopy()
    {
        using var target = new ProfilerFixture();
        using var host = new ProfilerFixture(1100, 800);
        using var session = new ProfileSession();
        using var view = new ProfilerView(session);
        view.Gui.Input = host.Input;
        view.Gui.ConfigureFonts(host.Font);
        using var registration = view.Attach(target.Gui, "Keyboard target");
        registration.Profiler.Detailed = true;
        target.Frame(() => { target.Gui.SetValue(42, "answer"); target.Gui.Node(10, 10, "selectable"); });
        Frame(view, host);
        SelectTree("sources");
        Assert.Equal(registration.Profiler.Source.Info.Id, view.Capture!.Source.Id);
        SelectTree("history");
        Assert.False(view.FollowLatest);
        SelectTree("layout");
        Assert.NotNull(view.SelectedObject);
        view.Command("panel", "state");
        Frame(view, host);
        SelectTree("state");
        view.Command("copy");
        Frame(view, host);
        Assert.Contains("42", host.Input.GetClipboardText());
        var skinButton = Assert.IsType<LayoutNode>(FindNode(view.Gui, "profiler:skin"));
        view.Gui.RequestFocus(skinButton.Id);
        Frame(view, host);
        host.Input.PressKey(KeyboardKey.Enter);
        Frame(view, host);
        host.Input.ReleaseKey(KeyboardKey.Enter);
        Frame(view, host);
        Assert.Contains(ProfilerSkins.Light, view.Gui.StyleSheets);

        void SelectTree(string name)
        {
            var found = Assert.IsType<LayoutNode>(FindNode(view.Gui, "profiler-tree:" + name));
            view.Gui.RequestFocus(found.Id);
            Frame(view, host);
            host.Input.PressKey(KeyboardKey.Down);
            Frame(view, host);
            host.Input.ReleaseKey(KeyboardKey.Down);
            Frame(view, host);
        }
    }

    [Fact]
    public void RejectedAttachmentAndFullCommandQueueLeaveTheViewerUsable()
    {
        using var target = new ProfilerFixture();
        using var host = new ProfilerFixture(1100, 800);
        using var session = new ProfileSession();
        using var view = new ProfilerView(session);
        view.Gui.Input = host.Input;
        view.Gui.ConfigureFonts(host.Font);
        using var controller = new GuiFrameController(target.Gui);
        Assert.Throws<InvalidOperationException>(() => view.Attach(target.Gui, "Invalid target"));
        Assert.Null(target.Gui.FrameObserver);
        Assert.Empty(session.GetSources());
        for (var index = 0; index < 257; index++) { view.Command("live"); }
        Assert.Contains("full", view.Message);
        Frame(view, host);
        Assert.Null(view.Message);
    }

    [Fact]
    public void DeferredCommandsControlReplaySelectionAndWorkspaceWithoutAPointer()
    {
        using var target = new ProfilerFixture();
        using var host = new ProfilerFixture(1100, 800);
        using var session = new ProfileSession();
        using var view = new ProfilerView(session);
        view.Gui.Input = host.Input;
        view.Gui.ConfigureFonts(host.Font);
        using var registration = view.Attach(target.Gui, "Application");
        registration.Controller.Recording = true;
        target.Frame(() => target.Gui.Node(10, 10, "recorded"));
        Frame(view, host);
        var generation = view.Capture!.Generation;
        foreach (var command in new[] { "detail", "record", "slower", "faster", "loop", "play", "stop" })
        {
            view.Command(command);
            Frame(view, host);
        }
        Assert.True(registration.Profiler.Detailed);
        Assert.False(registration.Controller.Recording);
        Assert.False(registration.Controller.Playing);
        Assert.Equal(1, registration.Controller.TimeScale);
        registration.Controller.ResetTarget = static () => { };
        view.Command("loop");
        Frame(view, host);
        Assert.True(registration.Controller.Loop);
        registration.Controller.ClearRecording();
        view.Command("play");
        Frame(view, host);
        Assert.Contains("empty", view.Message);
        view.Command("frame", generation.ToString());
        Frame(view, host);
        Assert.False(view.FollowLatest);
        view.Command("live");
        Frame(view, host);
        Assert.True(view.FollowLatest);
        foreach (var command in new[] { "next-panel", "dock-left", "dock-bottom", "tabs", "skin", "unknown" })
        {
            view.Command(command);
            Frame(view, host);
        }
        Assert.Contains("Unavailable command", view.Message);
        host.Input.PressKey(KeyboardKey.F10);
        Frame(view, host);
        Assert.True(registration.Controller.Paused);
        host.Input.ReleaseKey(KeyboardKey.F10);
        var count = 0;
        target.Frame(() => { if (target.Gui.Pass == Pass.Pass1Build) { count++; } });
        target.Frame(() => { if (target.Gui.Pass == Pass.Pass1Build) { count++; } });
        Assert.Equal(1, count);
    }

    [Fact]
    public void FileCommandsRoundtripCapturesAndBothInputFormatsAndContainFailures()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var target = new ProfilerFixture();
            using var host = new ProfilerFixture(1100, 800);
            using var session = new ProfileSession();
            var files = new FileDialogState();
            using var view = new ProfilerView(session, files);
            view.Gui.Input = host.Input;
            view.Gui.ConfigureFonts(host.Font);
            using var registration = view.Attach(target.Gui, "Application");
            registration.Controller.Recording = true;
            target.Frame(() => target.Gui.DrawText("Recorded target"));
            Frame(view, host);
            var generation = view.Capture!.Generation;
            var capture = Path.Combine(directory, "capture.json");
            var recording = Path.Combine(directory, "input.json");
            FileCommand("save-capture", capture);
            FileCommand("save-input", recording);
            Assert.True(File.Exists(capture));
            Assert.True(File.Exists(recording));
            FileCommand("open-capture", capture);
            Assert.Equal(generation, view.Capture!.Generation);
            Assert.False(view.FollowLatest);
            FileCommand("open-input", recording);
            Assert.True(registration.Controller.Playing);
            registration.Controller.Stop();
            var legacy = Path.Combine(directory, "legacy.json");
            File.WriteAllText(legacy, "{\"steps\":[{\"action\":\"key\",\"key\":\"a\"}]}");
            FileCommand("open-input", legacy);
            Assert.True(registration.Controller.Playing);
            File.WriteAllText(legacy, "{\"steps\":null}");
            view.Command("file:open-input", legacy);
            Frame(view, host);
            Assert.Contains("malformed", view.Message);
            view.Command("file:open-capture", Path.Combine(directory, "missing.json"));
            Frame(view, host);
            Assert.NotNull(view.Message);
            Assert.Equal(generation, view.Capture.Generation);
            view.Command("save-capture");
            Frame(view, host);
            Assert.True(files.IsOpen);
            files.Close(null);
            Frame(view, host);
            Assert.False(files.IsOpen);

            void FileCommand(string command, string path)
            {
                view.Command(command);
                Frame(view, host);
                Assert.True(files.IsOpen);
                files.Close(path);
                Frame(view, host);
                Assert.StartsWith("Completed", view.Message);
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
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

    sealed class Bridge : IAccessibilityTreeSink, IAccessibilityActionSource
    {
        internal Dictionary<string, AccessibilityElement> Elements { get; } = [];
        internal Queue<AccessibilityAction> Actions { get; } = [];
        public void PublishElement(AccessibilityElement element) => Elements[element.Id] = element;
        public void Announce(string scopeId, string message, string? elementId = null) { }
        public bool TryDequeue(string scopeId, out AccessibilityAction action) => Actions.TryDequeue(out action!);
    }
}
