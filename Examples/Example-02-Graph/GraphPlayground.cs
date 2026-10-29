namespace Example02;

/// <summary>The scenario shell for the graph-only playground.</summary>
public sealed class GraphPlayground
{
    static readonly string[] Titles = ["Sandbox", "Running flow", "State machine", "Organizers"];
    StyleSheet? _appliedStyle;
    readonly Queue<Action> _pending = new();
    int _frameScenario;

    /// <summary>The active scenario: sandbox, running flow, state machine, or organizers.</summary>
    public int Scenario { get; private set; }
    /// <summary>Also shows tabs for scenarios whose features have not landed yet.</summary>
    public bool ShowPendingScenarios { get; set; }
    /// <summary>The real dataflow sandbox and its independent view layout.</summary>
    public SandboxScene Sandbox { get; } = new();
    /// <summary>A separate arithmetic document evaluated in the background and drawn with a flow overlay.</summary>
    public FlowScene Flow { get; } = new();
    /// <summary>A cyclic State editor with a many-instance observer and typed signals.</summary>
    public FsmScene Fsm { get; } = new();
    /// <summary>The selected application's stylesheet index.</summary>
    public int StyleIndex { get; private set; }
    /// <summary>The style name shown by the selector.</summary>
    public string StyleName => GraphDemoStyles.Names[StyleIndex];

    int TabCount => ShowPendingScenarios ? Titles.Length : 3;

    /// <summary>Chooses an existing scenario.</summary>
    public void SelectScenario(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Titles.Length);
        Scenario = index;
    }

    /// <summary>Draws one immediate-mode pass.</summary>
    public void Draw(Gui gui)
    {
        if (gui.Pass == Pass.Pass1Build)
        {
            while (_pending.TryDequeue(out var action))
            {
                action();
            }
            _frameScenario = Scenario;
        }
        ApplyStyle(gui);
        using var root = gui.StyledNode("graph-playground").Expand().Direction(Axis.Vertical).Enter();
        DrawHeader(gui);
        using (gui.Node().ExpandWidth().Direction(Axis.Horizontal).Gap(8).Enter())
        {
            for (var i = 0; i < TabCount; i++)
            {
                DrawTab(gui, i);
            }
        }
        using var stage = gui.Node().Expand().Direction(Axis.Vertical).Gap(10).Enter();
        Action<string, string, Action> button = (label, id, action) => Button(gui, label, id, action);
        switch (_frameScenario)
        {
            case 0:
                Sandbox.Draw(gui, button);
                DrawOptions(gui, Sandbox.View);
                break;
            case 1:
                Flow.Draw(gui, button);
                DrawOptions(gui, Flow.Sandbox.View);
                break;
            case 2:
                Fsm.Draw(gui, button);
                DrawOptions(gui, Fsm.View);
                break;
            default:
                DrawPendingScenario(gui);
                break;
        }
    }

    /// <summary>Selects a complete PSS sheet for every scenario and its surrounding controls.</summary>
    public void SelectStyle(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, GraphDemoStyles.Sheets.Count);
        StyleIndex = index;
    }

    /// <summary>Cycles Blueprint, Paper and High contrast.</summary>
    public void CycleStyle() => SelectStyle((StyleIndex + 1) % GraphDemoStyles.Sheets.Count);

    void ApplyStyle(Gui gui)
    {
        GraphStyles.Ensure(gui);
        var sheet = GraphDemoStyles.Sheets[StyleIndex];
        if (_appliedStyle != sheet)
        {
            if (_appliedStyle is not null)
            {
                gui.StyleSheets.Remove(_appliedStyle);
            }
            gui.StyleSheets.Add(sheet);
            _appliedStyle = sheet;
        }
    }

    static readonly Bitmap Badge = LoadBadge();

    static void DrawHeader(Gui gui)
    {
        using var heading = gui.StyledNode("graph-heading").ExpandWidth().Height(40)
            .Direction(Axis.Horizontal).ContentAlignY(0.5f).Gap(8).Enter();
        gui.Image(Badge, width: 26, height: 26);
        gui.DrawText("Graph Playground");
        gui.Node().Expand();
        gui.DrawText($"{gui.Time.SmoothFps:N0} FPS");
    }

    static Bitmap LoadBadge()
    {
        using var stream = typeof(GraphPlayground).Assembly.GetManifestResourceStream("GuinevereDemos.guinevere-badge.png")!;
        return Bitmap.FromStream(stream);
    }

    /// <summary>Cycles graph scenarios through Bézier, straight and orthogonal link routing.</summary>
    public void CycleRouting()
    {
        var routing = (GraphLinkRouting)(((int)Sandbox.View.Routing + 1) % 3);
        Sandbox.View.Routing = routing;
        Flow.Sandbox.View.Routing = routing;
        Fsm.View.Routing = routing;
    }

    void DrawOptions(Gui gui, GraphDocumentView view)
    {
        using var options = gui.Node().ExpandWidth().Direction(Axis.Horizontal).Gap(20).Wrap(1).Enter();
        Toggle(gui, "Minimap", "toggle-minimap", view.Canvas.ShowMinimap,
            () => view.Canvas.ShowMinimap = !view.Canvas.ShowMinimap);
        Toggle(gui, "Avoid replacing connections", "protect-connections", view.AvoidReplacingConnections,
            () => view.AvoidReplacingConnections = !view.AvoidReplacingConnections);
        Button(gui, "Style: " + StyleName, "select-style", CycleStyle);
        Button(gui, "Links: " + view.Routing, "cycle-routing", CycleRouting);
    }

    void Toggle(Gui gui, string label, string id, bool value, Action action)
    {
        using var scope = gui.StyledNode("graph-toggle", id: id).Width(280).Height(32).Direction(Axis.Horizontal).Gap(10).ContentAlignY(0.5f).Enter();
        var result = gui.Toggleable(value, new ControlBehaviorOptions(Role: ControlRole.Switch, Label: label));
        if (result.Activated)
        {
            Queue(gui, action);
        }
        using (gui.StyledNode("graph-toggle-indicator", modifiers: value ? ["checked"] : []).Width(20).Height(20).Enter())
        {

        }
        gui.DrawText(label);
    }

    void DrawTab(Gui gui, int index)
    {
        using var scope = gui.StyledNode("graph-tab", id: TabId(index), modifiers: _frameScenario == index ? ["selected"] : []).Width(156).Height(42).AlignContent(0.5f, 0.5f).Enter();
        gui.CurrentNode.On<KeyDownEvent>(e => NavigateTabs(gui, e, index));
        var selected = _frameScenario == index;
        var result = gui.Selectable(selected,
            new ControlBehaviorOptions(Role: ControlRole.Tab, Label: Titles[index]));
        if (result.Activated)
        {
            Queue(gui, () => SelectScenario(index));
        }
        gui.DrawText(Titles[index]);
    }

    void NavigateTabs(Gui gui, KeyDownEvent e, int current)
    {
        var count = TabCount;
        var next = e.Key switch
        {
            KeyboardKey.Right => (current + 1) % count,
            KeyboardKey.Left => (current + count - 1) % count,
            KeyboardKey.Home => 0,
            KeyboardKey.End => count - 1,
            _ => -1
        };
        if (next < 0)
        {
            return;
        }
        SelectScenario(next);
        gui.Focus.RequestFocus(TabId(next), FocusReason.Keyboard);
        e.PreventDefault();
        e.StopPropagation();
    }

    void Button(Gui gui, string label, string id, Action action)
    {
        using var scope = gui.StyledNode("graph-button", id: id).Width(156).Height(36).AlignContent(0.5f, 0.5f).Enter();
        var result = gui.Pressable(new ControlBehaviorOptions(Role: ControlRole.Button, Label: label));
        if (result.Activated)
        {
            Queue(gui, action);
        }
        gui.DrawText(label);
    }

    void DrawPendingScenario(Gui gui)
    {
        using var panel = gui.Node().Expand().Direction(Axis.Vertical).Padding(28).Gap(16).Enter();
        gui.DrawText(Titles[_frameScenario]);
        gui.DrawText("Groups, notes and collapsed containers will have their own scenario here.");
        gui.DrawText("The Sandbox is available now for graph editing and layout interaction.");
        Button(gui, "Open Sandbox", "open-sandbox", () => SelectScenario(0));
    }

    void Queue(Gui gui, Action action)
    {
        _pending.Enqueue(action);
        gui.RequestFrame();
    }

    static string TabId(int index) => $"scenario-{index}";
}
