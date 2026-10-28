using Guinevere;
using Guinevere.Graphs;
using MASS4.Graph;
using GraphPoint = Guinevere.Graphs.GraphPoint;

namespace Example02;

/// <summary>An editable real dataflow graph with separate view layout and selection.</summary>
public sealed class SandboxScene
{
    bool _paletteOpen;
    string _query = "";

    /// <summary>Creates the arithmetic graph and its initial connections.</summary>
    public SandboxScene()
    {
        View = new GraphDocumentView(Graph.Editor,
            new GraphCanvas { ShowMinimap = true, Name = "Playground sandbox" }) { NodeTitle = Graph.Describe };
        var first = View.AddNode(ArithmeticGraph.Number, new GraphPoint(40, 40), 2f);
        var second = View.AddNode(ArithmeticGraph.Number, new GraphPoint(40, 240), 3f);
        var multiply = View.AddNode(ArithmeticGraph.Multiply, new GraphPoint(360, 120));
        var result = View.AddNode(ArithmeticGraph.Result, new GraphPoint(700, 140));
        View.Connect(Port(first, ArithmeticGraph.ValueKey), Port(multiply, ArithmeticGraph.AKey));
        View.Connect(Port(second, ArithmeticGraph.ValueKey), Port(multiply, ArithmeticGraph.BKey));
        View.Connect(Port(multiply, ArithmeticGraph.ValueKey), Port(result, ArithmeticGraph.AKey));
        Graph.Editor.ClearHistory();
        View.Refresh();
        Canvas.Navigation.SearchRequested = _ => _paletteOpen = true;
    }

    /// <summary>The sample's real arithmetic domain.</summary>
    public ArithmeticGraph Graph { get; } = new();
    /// <summary>The reusable real-document adapter.</summary>
    public GraphDocumentView View { get; }
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
        using (gui.Node().ExpandWidth().Direction(Axis.Horizontal).Gap(8).Enter())
        {
            button("Add node", "add-node", () => _paletteOpen = !_paletteOpen);
            button("Undo", "undo", () => View.Undo());
            button("Redo", "redo", () => View.Redo());
            button("Frame all", "frame-all", Canvas.FitAll);
            button("Reset layout", "reset-layout", ResetLayout);
        }
        if (_paletteOpen)
        {
            DrawPalette(gui, button);
        }
        if (View.Diagnostics.Count > 0)
        {
            gui.DrawText(string.Join("; ", View.Diagnostics.Select(static d => $"{d.Id}: {d.Message}")),
                13, Color.Gold);
        }
        var host = gui.Node().Expand().On<KeyDownEvent>(e => Nudge(gui, e), capture: true);
        using var scope = host.Enter();
        View.Draw(gui, "sandbox-canvas");
    }

    void DrawPalette(Gui gui, Action<string, string, Action> button)
    {
        using var box = gui.Node().ExpandWidth().Direction(Axis.Vertical).Gap(8).Enter();
        using (var search = gui.Node(360, 32, "node-search").Padding(8).Enter())
        {
            gui.DrawBackgroundRect(new Color(0x29384eff), 6);
            if (gui.Pass == Pass.Pass2Render)
            {
                gui.RegisterFocusable();
                gui.ControlSemantics?.Publish(new ControlSemantics("node-search", ControlRole.None,
                    "Search node types", _query, gui.CurrentNode.Rect,
                    gui.HasFocus() ? ControlVisualState.Focused : ControlVisualState.None));
            }
            gui.CurrentNode.On<PointerDownEvent>(_ => gui.Focus.RequestFocus("node-search"));
            gui.CurrentNode.On<TextInputEvent>(e =>
            {
                _query += e.Text;
                e.PreventDefault();
            });
            gui.CurrentNode.On<KeyDownEvent>(e => SearchKey(gui, e));
            gui.DrawText(_query.Length == 0 ? "Search node types…" : _query, 14, Color.White);
        }
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
            gui.Focus.RequestFocus("sandbox-canvas");
        }
        _paletteOpen = false;
    }

    void SearchKey(Gui gui, KeyDownEvent e)
    {
        switch (e.Key)
        {
            case KeyboardKey.Backspace when _query.Length > 0:
                _query = _query[..System.Globalization.StringInfo.ParseCombiningCharacters(_query)[^1]];
                break;
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

    void Nudge(Gui gui, KeyDownEvent e)
    {
        if (e.TargetId != "sandbox-canvas" || !ShiftHeld(gui))
        {
            return;
        }
        var delta = e.Key switch
        {
            KeyboardKey.Left => new GraphPoint(-16, 0),
            KeyboardKey.Right => new GraphPoint(16, 0),
            KeyboardKey.Up => new GraphPoint(0, -16),
            KeyboardKey.Down => new GraphPoint(0, 16),
            _ => default
        };
        if (delta == default)
        {
            return;
        }
        MoveSelection(delta);
        e.PreventDefault();
        e.StopPropagation();
    }

    ElementId Port(ElementId node, StableId key) => Graph.Editor.Document.GetNode(node).GetPort(key).Id;

    static bool ShiftHeld(Gui gui) => gui.Input.IsKeyDown(KeyboardKey.LeftShift)
        || gui.Input.IsKeyDown(KeyboardKey.RightShift);
}
