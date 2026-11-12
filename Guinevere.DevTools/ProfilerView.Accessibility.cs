namespace Guinevere.DevTools;

public sealed partial class ProfilerView
{
    static readonly string[] Commands = ["pause", "step", "live", "detail", "record", "play", "stop", "loop",
        "slower", "faster", "panel", "next-panel", "float", "dock-left", "dock-right", "dock-bottom", "tabs", "skin", "copy"];

    void PublishSemantics()
    {
        if (!Gui.Platform.TryGet<IAccessibilityTreeSink>(out var sink))
        {
            return;
        }
        sink!.PublishElement(new AccessibilityElement("profiler", null, "window", "Profiler",
            "F6 next panel; F8 pause/resume; F10 step. Tab navigates controls; arrows navigate inspection rows.",
            Gui.ScreenRect, ControlVisualState.None, Actions: Commands, ScopeId: "profiler"));
        foreach (var panel in Panels)
        {
            sink.PublishElement(new AccessibilityElement($"profiler/panel/{panel}", "profiler", "panel", panel,
                "Select this panel before using keyboard docking controls.", Rect.Zero,
                panel == ActivePanel ? ControlVisualState.Selected : ControlVisualState.None,
                Virtualized: true, Actions: ["panel"], ScopeId: "profiler", Key: panel));
        }
        foreach (var source in _sources)
        {
            var id = source.Info.Id.ToString();
            sink.PublishElement(new AccessibilityElement($"profiler/source/{id}", "profiler/panel/sources",
                "listitem", source.Info.Name, source.Info.Kind, Rect.Zero,
                source.Info.Id == _sourceId ? ControlVisualState.Selected : ControlVisualState.None,
                Virtualized: true, Actions: ["source"], ScopeId: "profiler", Key: id));
        }
        foreach (var frame in _history)
        {
            var key = frame.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture);
            sink.PublishElement(new AccessibilityElement($"profiler/frame/{key}", "profiler/panel/sources",
                "listitem", $"Frame {frame.Frame}", $"{frame.Dropped} dropped items", Rect.Zero,
                ReferenceEquals(frame, Capture) ? ControlVisualState.Selected : ControlVisualState.None,
                Virtualized: true, Actions: ["frame"], ScopeId: "profiler", Key: key));
        }
        PublishCapturedElements(sink);
    }

    void PublishCapturedElements(IAccessibilityTreeSink sink)
    {
        if (Capture is not { } capture)
        {
            return;
        }
        var prefix = $"profiler/capture/{capture.Session}/{capture.Source.Id}/{capture.Generation}";
        foreach (var item in capture.Items.Where(static item => item.Category == "layout"))
        {
            var parent = item.ParentId.Length > 0 ? $"{prefix}/{item.ParentId}" : "profiler/panel/layout";
            sink.PublishElement(new AccessibilityElement($"{prefix}/{item.Id}", parent,
                "treeitem", $"{item.Name} {item.Value}",
                $"Captured bounds {item.X},{item.Y} {item.Width}×{item.Height}; source {capture.Source.Name}",
                Rect.Zero, item.Id == SelectedObject ? ControlVisualState.Selected : ControlVisualState.None,
                Virtualized: true, Actions: ["object"], ScopeId: "profiler", Key: item.Id));
        }
        for (var index = 0; index < capture.Items.Count; index++)
        {
            var item = capture.Items[index];
            if (item.Category == "layout")
            {
                continue;
            }
            sink.PublishElement(new AccessibilityElement($"{prefix}/property/{index}", "profiler",
                "text", item.Name, $"{item.Category}: {item.Value}", Rect.Zero, ControlVisualState.None,
                Virtualized: true, ScopeId: "profiler"));
        }
    }
}
