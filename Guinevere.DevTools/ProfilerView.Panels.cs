using System.Globalization;
using MASS4.Profiling;

namespace Guinevere.DevTools;

public sealed partial class ProfilerView
{
    void Toolbar()
    {
        using (Gui.StyledNode("profiler-toolbar").ExpandWidth().Direction(Axis.Horizontal).Wrap(1).Enter())
        {
            ToolButton(Target?.Controller.Paused == true ? "Resume (F8)" : "Pause (F8)", "pause", Target is not null);
            ToolButton("Step (F10)", "step", Target is not null);
            ToolButton("Detail", "detail", Target is not null);
            ToolButton("Live", "live");
            ToolButton("Light / dark", "skin");
            ToolButton("Copy selected", "copy", _selectedText is not null);
            ToolButton("Save capture", "save-capture", Capture is not null);
            ToolButton("Open capture", "open-capture");
        }
        using (Gui.StyledNode("profiler-toolbar").ExpandWidth().Direction(Axis.Horizontal).Wrap(1).Enter())
        {
            ToolButton($"Panel: {ActivePanel} (F6)", "next-panel");
            ToolButton("Float", "float");
            ToolButton("Dock left", "dock-left");
            ToolButton("Dock right", "dock-right");
            ToolButton("Dock bottom", "dock-bottom");
            ToolButton("Tab group", "tabs");
        }
    }

    void ToolButton(string label, string command, bool enabled = true)
    {
        if (Gui.Button(label, enabled: enabled, id: $"profiler:{command}"))
        {
            Command(command);
        }
    }

    void DrawPanel(string panel, Gui gui)
    {
        using (gui.StyledNode("profiler-panel", id: $"profiler-panel:{panel}").Expand()
                   .Direction(Axis.Vertical).Enter())
        {
            switch (panel)
            {
                case "sources": Sources(); break;
                case "timeline": Timeline(); break;
                case "preview":
                    Gui.Label("Live target image; historical captures retain inspector values only.");
                    using (Gui.StyledNode("profiler-preview").Expand().Enter())
                    {
                        Target?.Controller.DrawPreview(Gui, Gui.CurrentNode.Rect);
                    }
                    break;
                case "layout": LayoutPanel(); break;
                case "diagnostics": Diagnostics(); break;
                case "input": InputPanel(); break;
                default: Inspector(panel); break;
            }
        }
    }

    void Sources()
    {
        var sources = _sources.Select(static source => new TreeItem(source.Info.Id.ToString(),
            $"{source.Info.Name} ({source.Info.Kind})", 0)).ToArray();
        DrawTree("sources", sources, "source");
        Gui.Label(Capture is null ? "No completed frame" :
            $"Frame {Capture.Frame} · {Capture.Items.Count} details · {Capture.Dropped} dropped");
        var history = _history.Select(static frame => new TreeItem(frame.Generation.ToString(CultureInfo.InvariantCulture),
            string.Create(CultureInfo.InvariantCulture, $"#{frame.Frame}  {(frame.End - frame.Start) * 1000d / frame.Source.Clock.Frequency:F3} ms"), 0)).ToArray();
        DrawTree("history", history, "frame");
    }

    void Timeline()
    {
        if (SelectedFrame is not { } frame)
        {
            Gui.Label("Waiting for a completed capture.");
            return;
        }
        Gui.Label(string.Create(CultureInfo.InvariantCulture,
            $"{frame.Name} · frame #{frame.Frame} · {frame.Milliseconds:F3} ms · {frame.Dropped} dropped"));
        FlameChart(frame);
        if (Capture is not { } capture)
        {
            Gui.Label("Inspector details for this frame were evicted; timing only.");
            return;
        }
        Gui.Label($"Clock {capture.Source.Clock.Domain} · {capture.Source.Clock.Frequency} Hz · resolution {capture.Source.Clock.ResolutionTicks?.ToString(CultureInfo.InvariantCulture) ?? "unmeasured"} ticks; CPU scopes include inline instrumentation.");
        var events = capture.Events.Select(entry => new TreeItem(entry.Id.ToString(CultureInfo.InvariantCulture),
            EventLabel(entry, capture.Source.Clock.Frequency), 0)).ToArray();
        DrawTree("events", events, "event");
    }

    static string EventLabel(ProfileEvent entry, long frequency) => entry.Kind switch
    {
        "scope" => string.Create(CultureInfo.InvariantCulture,
            $"track {entry.Track}: {entry.Name} {(entry.End - entry.Start) * 1000d / frequency:F3} ms"),
        "counter" => string.Create(CultureInfo.InvariantCulture, $"{entry.Name}: {entry.Value} {entry.Message}"),
        _ => $"{entry.Name}: {entry.Message}"
    };

    void LayoutPanel()
    {
        var items = Capture?.Items.Where(static item => item.Category == "layout").ToArray() ?? [];
        DrawTree("layout", LayoutRows(items), "object");
        using (Gui.StyledNode("profiler-preview", id: "profiler-layout-preview").ExpandWidth().Height(120).Enter())
        {
            if (Gui.Pass == Pass.Pass2Render && items.Length > 0)
            {
                DrawBounds(items, Gui.CurrentNode.Rect);
            }
        }
    }

    static List<TreeItem> LayoutRows(ProfileItem[] items)
    {
        var depths = new Dictionary<string, int>(StringComparer.Ordinal);
        var parents = items.Select(static item => item.ParentId).ToHashSet(StringComparer.Ordinal);
        var rows = new List<TreeItem>();
        foreach (var item in items)
        {
            var depth = item.ParentId.Length > 0 ? depths.GetValueOrDefault(item.ParentId) + 1 : 0;
            depths[item.Id] = depth;
            rows.Add(new TreeItem(item.Id, $"{item.Name} {item.Value} ({item.Width}×{item.Height})", depth,
                parents.Contains(item.Id)));
        }
        return rows;
    }

    void DrawBounds(ProfileItem[] items, Rect area)
    {
        var root = items[0];
        var scale = Math.Min(area.W / Math.Max(1, root.Width ?? 1), area.H / Math.Max(1, root.Height ?? 1));
        var color = Gui.ResolveStyle("profiler-bounds").GetColor("border-color") ?? Color.Transparent;
        foreach (var item in items)
        {
            if (SelectedObject is null || item.Id == SelectedObject)
            {
                Gui.DrawRectBorder(new Rect(area.X + (item.X ?? 0) * scale, area.Y + (item.Y ?? 0) * scale,
                    (item.Width ?? 0) * scale, (item.Height ?? 0) * scale), color, item.Id == SelectedObject ? 2 : 1);
            }
        }
    }

    void Inspector(string panel)
    {
        var values = Capture?.Items.Where(item => Matches(panel, item.Category)
            && (SelectedObject is null || panel is "memory" or "state" || item.Id == SelectedObject)).ToArray() ?? [];
        Gui.Label(values.Length == 0 ? "No captured values for this selection; enable Detail for future frames." :
            $"{values.Length} captured values · selection {SelectedObject ?? "all"}");
        var rows = values.Select((item, index) => new TreeItem(index.ToString(CultureInfo.InvariantCulture),
            $"{item.Id} · {item.Name}: {item.Value}", 0)).ToArray();
        DrawTree(panel, rows, "copy");
    }

    static bool Matches(string panel, string category) => panel switch
    {
        "styles" => category.StartsWith("style.", StringComparison.Ordinal),
        "layout" => category is "layout" or "constraints" or "source",
        _ => panel == category
    };

    void Diagnostics()
    {
        Gui.Label(DiagnosticMessage(), selectable: true);
        var events = Capture?.Events.Where(static entry => entry.Kind == "diagnostic")
            .Select(static entry => new TreeItem(entry.Id.ToString(CultureInfo.InvariantCulture),
                $"{entry.Name}: {entry.Message}", 0)).ToArray() ?? [];
        DrawTree("diagnostics", events, "copy");
    }

    string DiagnosticMessage() => Message ?? Target?.Controller.LastError
        ?? Target?.Profiler.LastError ?? "No target diagnostics.";

    void InputPanel()
    {
        using (Gui.Node().ExpandWidth().Direction(Axis.Horizontal).Wrap(1).Enter())
        {
            ToolButton(Target?.Controller.Recording == true ? "Stop recording" : "Record", "record", Target is not null);
            ToolButton("Play", "play", Target is not null);
            ToolButton("Stop", "stop", Target is not null);
            ToolButton("Loop", "loop", Target is not null);
            ToolButton("Slower", "slower", Target is not null);
            ToolButton("Faster", "faster", Target is not null);
        }
        InputStatus();
        using (Gui.Node().ExpandWidth().Direction(Axis.Horizontal).Wrap(1).Enter())
        {
            ToolButton("Save input", "save-input", Target is not null);
            ToolButton("Open input / script", "open-input", Target is not null);
        }
        Inspector("input");
    }

    void InputStatus()
    {
        Gui.Label($"Speed {Target?.Controller.TimeScale ?? 1} · dropped frames {Target?.Controller.DroppedInputFrames ?? 0} · dropped events {Target?.Controller.DroppedInputEvents ?? 0}");
        Gui.Label($"Script checks failed: {Target?.Controller.ScriptFailures ?? 0} · {Target?.Controller.PlaybackMessage ?? ""}");
    }

    void DrawTree(string id, IReadOnlyList<TreeItem> rows, string action)
    {
        if (!_trees.TryGetValue(id, out var tree))
        {
            _trees.Add(id, tree = new TreeViewState { DefaultExpandedDepth = 4 });
        }
        var previous = tree.SelectedId;
        Gui.TreeView(tree, rows, id: $"profiler-tree:{id}");
        if (Gui.Pass == Pass.Pass2Render && previous != tree.SelectedId && tree.SelectedId is { } selected)
        {
            SelectTreeRow(rows, action, selected);
        }
    }

    void SelectTreeRow(IReadOnlyList<TreeItem> rows, string action, string selected)
    {
        if (action is "source" or "frame" or "object")
        {
            Command(action, selected);
        }
        else if (rows.FirstOrDefault(item => item.Id == selected) is { Id: not null } row)
        {
            _selectedText = row.Label;
        }
    }
}
