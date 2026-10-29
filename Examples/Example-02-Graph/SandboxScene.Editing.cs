using GraphPoint = Guinevere.Graphs.GraphPoint;

namespace Example02;

public sealed partial class SandboxScene
{
    bool _actionsOpen;
    string? _status;

    /// <summary>Notifies hosts that loading replaced the document and its editor.</summary>
    public event Action? DocumentLoaded;

    /// <summary>Loads a complete document atomically, retaining the current editor if any part cannot be loaded.</summary>
    public GraphViewLoadResult Load(string json, string? layout = null, string? viewState = null)
    {
        var loaded = GraphDocumentView.Load(json, Graph.Editor.Document.Registry, layout, viewState,
            Graph.Semantics.Conversions, Graph.Semantics.Rules);
        _status = loaded.Diagnostics.Count == 0 ? "Document loaded."
            : string.Join("; ", loaded.Diagnostics.Select(static d => $"{d.Id}: {d.Message}"));
        if (loaded.View is not { } view)
        {
            return loaded;
        }
        var old = View;
        View = view;
        Graph.UseEditor(view.Editor);
        View.NodeTitle = Graph.Describe;
        View.ContentHeight = old.ContentHeight;
        View.NodePresenter = old.NodePresenter;
        View.NodeBadges = old.NodeBadges;
        View.NodeClasses = old.NodeClasses;
        View.PortShape = old.PortShape;
        View.Routing = old.Routing;
        View.StyleClasses = old.StyleClasses;
        View.ShowArrows = old.ShowArrows;
        View.LinkRenderer = old.LinkRenderer;
        View.LinkHitTolerance = old.LinkHitTolerance;
        View.PortHitRadius = old.PortHitRadius;
        View.AvoidReplacingConnections = old.AvoidReplacingConnections;
        View.RemapClipboardPayload = old.RemapClipboardPayload;
        View.SnapToGrid = old.SnapToGrid;
        View.GridSize = old.GridSize;
        View.Canvas.CompactZoom = old.Canvas.CompactZoom;
        View.Canvas.OutlineZoom = old.Canvas.OutlineZoom;
        View.Canvas.ShowGrid = old.Canvas.ShowGrid;
        View.Canvas.ShowAxes = old.Canvas.ShowAxes;
        View.Canvas.ShowMinimap = old.Canvas.ShowMinimap;
        View.Canvas.Name = old.Canvas.Name;
        View.Refresh();
        ConfigureActions();
        DocumentLoaded?.Invoke();
        return loaded;
    }

    void ConfigureActions()
    {
        Canvas.Navigation.SearchRequested = kind =>
        {
            _paletteOpen = true;
            _actionsOpen = kind == GraphSearchKind.Commands;
        };
        View.ContextRequested = _ => _actionsOpen = true;
    }

    void DrawEditingOptions(Gui gui, Action<string, string, Action> button)
    {
        using (gui.Node().ExpandWidth().Direction(Axis.Horizontal).Gap(8).Wrap(1).Enter())
        {
            button("Edit actions", "edit-actions", () => _actionsOpen = !_actionsOpen);
            button("Copy JSON", "save-json", () => WithClipboard(gui, c => c.SetClipboardText(View.Serialize())));
            button("Load JSON", "load-json", () => WithClipboard(gui, c => Load(c.GetClipboardText())));
        }
        if (_actionsOpen)
        {
            DrawActions(gui, button);
        }
        if (_status is { } status)
        {
            using var text = gui.StyledNode("graph-message").Enter();
            gui.DrawText(status);
        }
    }

    void DrawActions(Gui gui, Action<string, string, Action> button)
    {
        using var actions = gui.Node().ExpandWidth().Direction(Axis.Horizontal).Gap(8).Wrap(1).Enter();
        button("Copy", "copy-selection", () => WithClipboard(gui, c => c.SetClipboardText(View.CopySelection())));
        button("Cut", "cut-selection", () => WithClipboard(gui, c => View.CutSelection(c.SetClipboardText)));
        button("Paste", "paste-selection", () => WithClipboard(gui, c => View.Paste(c.GetClipboardText(), new GraphPoint(16, 16))));
        button("Duplicate", "duplicate-selection", () => View.DuplicateSelection(new GraphPoint(16, 16)));
        button("Delete", "delete-selection", () => View.DeleteSelection());
        button("Align left", "align-left", () => View.AlignSelection(GraphAlignment.Left));
        button("Align top", "align-top", () => View.AlignSelection(GraphAlignment.Top));
        button("Distribute X", "distribute-x", () => View.DistributeSelection(horizontal: true));
        button("Distribute Y", "distribute-y", () => View.DistributeSelection(horizontal: false));
        button("Snap to grid", "snap-grid", View.SnapSelection);
    }

    void WithClipboard(Gui gui, Action<IClipboard> action)
    {
        if (gui.Platform.TryGet<IClipboard>(out var clipboard) && clipboard is not null)
        {
            action(clipboard);
        }
        else
        {
            _status = "This host does not provide a clipboard.";
        }
    }
}
