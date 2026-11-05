using GraphPoint = Guinevere.Graphs.GraphPoint;

namespace Example02;

/// <summary>An editable real dataflow graph with separate view layout and selection.</summary>
public sealed partial class SandboxScene
{
    readonly Queue<Action> _pending = new();
    bool _paletteOpen;
    string _query = "";

    /// <summary>The height of a node's preview slot in world units.</summary>
    public const double PreviewHeight = 72;
    const double StepperHeight = 30;

    /// <summary>Creates a domain's initial document; the arithmetic domain is used when none is supplied.</summary>
    public SandboxScene(string name = "Playground sandbox", string canvasId = "sandbox-canvas",
        IDemoDomain? domain = null)
    {
        CanvasId = canvasId;
        Graph = domain ?? new ArithmeticGraph();
        View = CreateView(Graph.Editor, new GraphCanvas { ShowMinimap = true, Name = name });
        Graph.Populate(View);
        Graph.Editor.ClearHistory();
        View.Refresh();
        ConfigureActions();
    }

    /// <summary>The sample's real dataflow domain.</summary>
    public IDemoDomain Graph { get; }
    /// <summary>The latest background-rendered preview of a node, supplied by the hosting scene.</summary>
    public Func<ElementId, NodePreview?>? Previews { get; set; }
    /// <summary>The focusable canvas identifier.</summary>
    public string CanvasId { get; }
    /// <summary>The reusable real-document adapter.</summary>
    public GraphDocumentView View { get; private set; }
    /// <summary>The canvas with view-only camera and navigation.</summary>
    public GraphCanvas Canvas => View.Canvas;
    /// <summary>Current real-node descriptions.</summary>
    public IReadOnlyList<GraphElementView> Nodes => View.Nodes;

    /// <summary>Adds a real Number node through the graph editor.</summary>
    public void AddNode(GraphPoint position) => View.AddNode(ArithmeticGraph.Number, position);
    /// <summary>Resets positions only, retaining document elements, labels and history.</summary>
    public void ResetLayout() => View.ResetLayout();
    /// <summary>Selects a node exclusively or toggles its membership.</summary>
    public void SelectNode(string key, bool toggle) => Canvas.Navigation.Select(key, toggle);
    /// <summary>Moves the current node selection as view metadata.</summary>
    public void MoveSelection(GraphPoint delta) => View.MoveSelection(delta);

    /// <summary>Draws node creation, history controls, diagnostics and the editable document.</summary>
    public void Draw(Gui gui, Action<string, string, Action> button)
    {
        Canvas.ProcessInput(gui);
        if (gui.Pass == Pass.Pass1Build)
        {
            while (_pending.TryDequeue(out var action))
            {
                action();
            }
        }
        using (gui.Node().ExpandWidth().Direction(Axis.Horizontal).Gap(8).Enter())
        {
            button("Add node", "add-node", () => _paletteOpen = !_paletteOpen);
            button("Undo", "undo", () => View.Undo());
            button("Redo", "redo", () => View.Redo());
            button("Frame all", "frame-all", Canvas.FitAll);
        }
        DrawEditingOptions(gui, button);
        if (_paletteOpen)
        {
            DrawPalette(gui, button);
        }
        if (View.Diagnostics.Count > 0)
        {
            using var message = gui.StyledNode("graph-message").Enter();
            gui.DrawText(string.Join("; ", View.Diagnostics.Select(static d => $"{d.Id}: {d.Message}")));
        }
        var host = gui.Node().Expand();
        using var scope = host.Enter();
        View.Draw(gui, CanvasId);
    }

    /// <summary>Changes a steppable node's payload through the real editor, as one undoable step.</summary>
    public CommandResult Step(ElementId node, float delta) =>
        Graph.Stepped(Graph.Editor.Document.GetNode(node), delta) is { } payload
            ? View.Execute(new SetPayloadCommand(node, payload))
            : new CommandResult(false, []);

    bool Steppable(GraphNode node) => Graph.Stepped(node, 0) is not null;

    double ContentHeight(GraphNode node) =>
        (Steppable(node) ? StepperHeight : 0) + (Graph.HasPreview(node) ? PreviewHeight : 0);

    void DrawContent(Gui gui, GraphNodeVisual visual)
    {
        using var column = gui.Node().Expand().Direction(Axis.Vertical).Enter();
        if (Steppable(visual.Node))
        {
            DrawStepper(gui, visual);
        }
        if (Graph.HasPreview(visual.Node))
        {
            using var slot = gui.Node().ExpandWidth().Height((float)PreviewHeight).Enter();
            if (gui.Pass == Pass.Pass2Render && Previews?.Invoke(visual.Node.Id) is { } preview)
            {
                var rect = gui.CurrentNode.Rect;
                preview.Draw(gui, new Rect(rect.X + 4, rect.Y + 4, rect.W - 8, rect.H - 8));
            }
        }
    }

    void DrawStepper(Gui gui, GraphNodeVisual visual)
    {
        using var row = gui.Node().ExpandWidth().Height((float)StepperHeight).Direction(Axis.Horizontal).Gap(8)
            .ContentAlignY(0.5f).Enter();
        StepButton(gui, visual, "-", "decrement", -1);
        using (gui.Node().Expand().AlignContent(0.5f, 0.5f).Enter())
        {
            gui.DrawText(Graph.FormatPayload(visual.Node));
        }
        StepButton(gui, visual, "+", "increment", 1);
    }

    void StepButton(Gui gui, GraphNodeVisual visual, string label, string id, float delta)
    {
        using var scope = gui.Node().Width(30).Height(26).Enter();
        gui.CurrentNode.On<PointerDownEvent>(static e => e.StopPropagation());
        var name = $"{(delta < 0 ? "Decrease" : "Increase")} {visual.View.Name}";
        if (gui.IconButton(DemoIcons.Actions[delta < 0 ? "Decrease" : "Add node"], name, size: 26, id: id))
        {
            _pending.Enqueue(() => Step(visual.Node.Id, delta));
            gui.RequestFrame();
        }
    }

    void DrawPalette(Gui gui, Action<string, string, Action> button)
    {
        using var box = gui.Node().ExpandWidth().Direction(Axis.Vertical).Gap(8).Enter();
        DemoControls.TextInput(gui, ref _query, "Search node types", "node-search", onKey: e => SearchKey(gui, e));
        using var choices = gui.Node().ExpandWidth().Direction(Axis.Horizontal).Gap(8).Wrap(1).Enter();
        foreach (var type in View.Palette(_query))
        {
            button(type.DisplayName, "create-" + type.Id, () => CreateNode(gui, type));
        }
    }

    void CreateNode(Gui gui, NodeType type)
    {
        var id = View.AddNode(type, Canvas.VisibleBounds.Center);
        if (!id.IsEmpty)
        {
            Canvas.Navigation.Select(id.ToString());
            gui.Focus.RequestFocus(CanvasId);
        }
        _paletteOpen = false;
    }

    void SearchKey(Gui gui, KeyDownEvent e)
    {
        switch (e.Key)
        {
            case KeyboardKey.Escape:
                _paletteOpen = false;
                break;
            case KeyboardKey.Enter:
                var type = View.Palette(_query).FirstOrDefault();
                if (type is not null)
                {
                    CreateNode(gui, type);
                }
                break;
            default:
                return;
        }
        e.PreventDefault();
        e.StopPropagation();
    }

    GraphDocumentView CreateView(GraphEditor editor, GraphCanvas canvas)
    {
        return new GraphDocumentView(editor, canvas)
        {
            NodeTitle = Graph.Describe,
            ContentHeight = ContentHeight,
            NodePresenter = DrawContent,
            NodeBadges = static node => node.TypeId == ArithmeticGraph.Result.Id ? ["result"] : [],
            PortShape = static port => port.Direction == PortDirection.Output ? GraphPortShape.Triangle
                : GraphPortShape.Circle
        };
    }
}
