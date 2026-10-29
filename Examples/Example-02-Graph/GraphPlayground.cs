namespace Example02;

/// <summary>The Excalibur application shell for the graph playground.</summary>
public sealed class GraphPlayground
{
    static readonly string[] Titles = ["Sandbox", "Running flow", "State machine", "Groups"];
    static readonly string[] Routings = ["Bézier", "Straight", "Orthogonal"];
    static readonly Bitmap Badge = LoadBadge();
    readonly Queue<Action> _pending = new();
    StyleSheet? _appliedStyle;
    int _frameScenario;

    /// <summary>The active sandbox, flow, State or Groups scenario.</summary>
    public int Scenario { get; private set; }
    /// <summary>Retained for consumers that previously enabled unfinished scenarios; all scenarios are available.</summary>
    public bool ShowPendingScenarios { get; set; }
    /// <summary>The real dataflow sandbox and its independent layout.</summary>
    public SandboxScene Sandbox { get; } = new();
    /// <summary>A background-evaluated arithmetic document with an observation overlay.</summary>
    public FlowScene Flow { get; } = new();
    /// <summary>A cyclic State editor with independent observed instances.</summary>
    public FsmScene Fsm { get; } = new();
    /// <summary>A nested-group document sharing the ordinary editing and persistence APIs.</summary>
    public GroupsScene Groups { get; } = new();
    /// <summary>The selected application's stylesheet index.</summary>
    public int StyleIndex { get; private set; }
    /// <summary>The style name shown by the selector.</summary>
    public string StyleName => GraphDemoStyles.Names[StyleIndex];

    GraphDocumentView ActiveView => _frameScenario switch
    {
        0 => Sandbox.View,
        1 => Flow.Sandbox.View,
        2 => Fsm.View,
        _ => Groups.Sandbox.View
    };

    /// <summary>Chooses one of the available scenarios.</summary>
    public void SelectScenario(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Titles.Length);
        Scenario = index;
    }

    /// <summary>Draws compact application chrome and one scenario in both immediate-mode passes.</summary>
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
        ActiveView.Canvas.ProcessInput(gui);
        using var root = gui.StyledNode("graph-playground").Expand().Direction(Axis.Vertical).Enter();
        DrawHeader(gui);
        using var stage = gui.Node().Expand().Direction(Axis.Vertical).Gap(6).Enter();
        Action<string, string, Action> button = (label, id, action) => Button(gui, label, id, action);
        Groups.DrawInspector(gui, ActiveView, button);
        switch (_frameScenario)
        {
            case 0: Sandbox.Draw(gui, button); break;
            case 1: Flow.Draw(gui, button); break;
            case 2: Fsm.Draw(gui, button); break;
            default: Groups.Draw(gui, button); break;
        }
    }

    /// <summary>Selects a complete application PSS sheet.</summary>
    public void SelectStyle(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, GraphDemoStyles.Sheets.Count);
        StyleIndex = index;
    }

    /// <summary>Cycles Blueprint, Paper and High contrast.</summary>
    public void CycleStyle() => SelectStyle((StyleIndex + 1) % GraphDemoStyles.Sheets.Count);

    /// <summary>Cycles Bézier, straight and orthogonal routing across scenarios.</summary>
    public void CycleRouting() => SelectRouting((GraphLinkRouting)(((int)Sandbox.View.Routing + 1) % 3));

    void SelectRouting(GraphLinkRouting routing)
    {
        Sandbox.View.Routing = routing;
        Flow.Sandbox.View.Routing = routing;
        Fsm.View.Routing = routing;
        Groups.Sandbox.View.Routing = routing;
    }

    void ApplyStyle(Gui gui)
    {
        GraphStyles.Ensure(gui);
        ExcaliburStyles.Ensure(gui);
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

    void DrawHeader(Gui gui)
    {
        using var heading = gui.StyledNode("graph-heading", id: "playground-header").ExpandWidth()
            .Direction(Axis.Horizontal).ContentAlignY(0.5f).Gap(8).Wrap(1).Enter();
        gui.Image(Badge, width: 24, height: 24);
        using (gui.Node().Width(270).Height(28).Enter())
        {
            gui.MenuBar(menus => BuildMenus(gui, menus), height: 28, fontSize: 13, padding: 8, id: "playground-main-menu");
        }
        var scenario = _frameScenario;
        gui.Dropdown(Titles, ref scenario, width: 160, height: 28, placeholder: "Scenario", classes: ["scenario-picker"]);
        if (gui.Pass == Pass.Pass1Build && scenario != _frameScenario)
        {
            Queue(gui, () => SelectScenario(scenario));
        }
        var style = StyleIndex;
        gui.Dropdown([.. GraphDemoStyles.Names], ref style, width: 150, height: 28, placeholder: "Style", classes: ["style-picker"]);
        if (gui.Pass == Pass.Pass1Build && style != StyleIndex)
        {
            Queue(gui, () => SelectStyle(style));
        }
    }

    void BuildMenus(Gui gui, MenuBarBuilder menus)
    {
        var commands = Commands(gui);
        foreach (var group in commands.GroupBy(static command => command.Menu))
        {
            menus.Menu(group.Key, menu =>
            {
                foreach (var command in group)
                {
                    menu.Item(command.Label, () => Queue(gui, command.Run));
                }
            });
        }
        menus.Menu("View", menu =>
        {
            var view = ActiveView;
            menu.CheckItem("Minimap", () => view.Canvas.ShowMinimap,
                value => Queue(gui, () => view.Canvas.ShowMinimap = value));
            menu.CheckItem("Avoid replacing connections", () => view.AvoidReplacingConnections,
                value => Queue(gui, () => view.AvoidReplacingConnections = value));
            menu.Submenu("Link routing", options =>
            {
                for (var i = 0; i < Routings.Length; i++)
                {
                    var routing = (GraphLinkRouting)i;
                    options.CheckItem(Routings[i], () => view.Routing == routing,
                        _ => Queue(gui, () => SelectRouting(routing)));
                }
            });
        });
        menus.Menu("Groups", menu => BuildGroupMenu(gui, menu));
    }

    IReadOnlyList<DemoCommand> Commands(Gui gui) => _frameScenario switch
    {
        0 => Sandbox.Commands(gui),
        1 => Flow.Sandbox.Commands(gui),
        2 => Fsm.Commands(gui),
        _ => Groups.Sandbox.Commands(gui)
    };

    void BuildGroupMenu(Gui gui, FlyoutBuilder menu)
    {
        var view = ActiveView;
        menu.Item("Group selected nodes", () => Queue(gui, () => view.GroupSelection("New group")));
        foreach (var group in view.Editor.Document.Groups.OrderBy(static group => group.Title))
        {
            menu.Submenu(group.Title, child =>
            {
                child.Item("Select frame", () => Queue(gui, () => view.Canvas.Navigation.Select(group.Id.ToString())));
                child.CheckItem("Collapsed", () => view.Editor.Document.CollapsedGroups.Contains(group.Id),
                    value => Queue(gui, () => view.SetGroupCollapsed(group.Id, value)));
                child.Item("Move selected nodes here", () => Queue(gui, () => view.MoveSelectionToGroup(group.Id)));
                child.Item("Nest selection here", () => Queue(gui, () => view.GroupSelection("Nested group", group.Id)));
                child.Item("Move frame to top level", () => Queue(gui, () => view.ReparentGroup(group.Id, null)));
                child.Item("Ungroup", () => Queue(gui, () => view.Ungroup(group.Id)));
            });
        }
        menu.Item("Remove selected nodes from groups", () => Queue(gui, () => view.MoveSelectionToGroup(null)));
    }

    void Button(Gui gui, string label, string id, Action action)
    {
        if (DemoControls.Button(gui, label, id))
        {
            Queue(gui, action);
        }
    }

    void Queue(Gui gui, Action action)
    {
        _pending.Enqueue(action);
        gui.RequestFrame();
    }

    static Bitmap LoadBadge()
    {
        using var stream = typeof(GraphPlayground).Assembly.GetManifestResourceStream("GuinevereDemos.guinevere-badge.png")!;
        return Bitmap.FromStream(stream);
    }
}
