using Guinevere;
using Guinevere.Graphs;
using GuinevereDemos;

namespace Example02;

/// <summary>The scenario shell for the graph-only playground.</summary>
public sealed class GraphPlayground
{
    static readonly string[] Titles = ["Sandbox", "Running flow", "Organizers"];
    static readonly Color Background = new(0x10151fff);
    static readonly Color Panel = new(0x1c2535ff);
    static readonly Color Accent = new(0x70c4ffff);
    readonly Queue<Action> _pending = new();
    int _frameScenario;

    /// <summary>The active scenario: sandbox, running flow, or organizers.</summary>
    public int Scenario { get; private set; }
    /// <summary>Also shows tabs for scenarios whose features have not landed yet.</summary>
    public bool ShowPendingScenarios { get; set; }
    /// <summary>The real dataflow sandbox and its independent view layout.</summary>
    public SandboxScene Sandbox { get; } = new();
    /// <summary>A separate arithmetic document evaluated in the background and drawn with a flow overlay.</summary>
    public FlowScene Flow { get; } = new();
    /// <summary>Whether both scenarios use the light theme.</summary>
    public bool LightTheme => Sandbox.View.Theme == GraphTheme.Light;

    int TabCount => ShowPendingScenarios ? Titles.Length : 2;

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
        using var root = gui.Node().Expand().Direction(Axis.Vertical).Padding(20).Gap(12).Enter();
        gui.DrawBackgroundRect(Background);
        DemoHeader.Header(gui, "Graph Playground");
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
                DrawOptions(gui, Sandbox);
                break;
            case 1:
                Flow.Draw(gui, button);
                DrawOptions(gui, Flow.Sandbox);
                break;
            default:
                DrawPendingScenario(gui);
                break;
        }
    }

    /// <summary>Switches both scenarios between the dark and light themes.</summary>
    public void ToggleTheme()
    {
        var theme = LightTheme ? GraphTheme.Dark : GraphTheme.Light;
        Sandbox.View.Theme = theme;
        Flow.Sandbox.View.Theme = theme;
    }

    /// <summary>Cycles both scenarios through Bézier, straight and orthogonal link routing.</summary>
    public void CycleRouting()
    {
        var routing = (GraphLinkRouting)(((int)Sandbox.View.Routing + 1) % 3);
        Sandbox.View.Routing = routing;
        Flow.Sandbox.View.Routing = routing;
    }

    void DrawOptions(Gui gui, SandboxScene scene)
    {
        using var options = gui.Node().ExpandWidth().Direction(Axis.Horizontal).Gap(20).Wrap(1).Enter();
        Toggle(gui, "Minimap", "toggle-minimap", scene.Canvas.ShowMinimap,
            () => scene.Canvas.ShowMinimap = !scene.Canvas.ShowMinimap);
        Toggle(gui, "Avoid replacing connections", "protect-connections", scene.View.AvoidReplacingConnections,
            () => scene.View.AvoidReplacingConnections = !scene.View.AvoidReplacingConnections);
        Toggle(gui, "Light theme", "toggle-theme", LightTheme, ToggleTheme);
        Button(gui, "Links: " + scene.View.Routing, "cycle-routing", CycleRouting);
    }

    void Toggle(Gui gui, string label, string id, bool value, Action action)
    {
        using var scope = gui.Node(280, 32, id).Direction(Axis.Horizontal).Gap(10).ContentAlignY(0.5f).Enter();
        var result = gui.Toggleable(value, new ControlBehaviorOptions(Role: ControlRole.Switch, Label: label));
        if (result.Activated)
        {
            Queue(gui, action);
        }
        FocusRing(gui);
        using (gui.Node(20, 20).Enter())
        {
            gui.DrawBackgroundRect(value ? Accent : Panel, 4);
        }
        gui.DrawText(label, 14, Color.White);
    }

    void DrawTab(Gui gui, int index)
    {
        using var scope = gui.Node(156, 42, TabId(index)).AlignContent(0.5f, 0.5f).Enter();
        gui.CurrentNode.On<KeyDownEvent>(e => NavigateTabs(gui, e, index));
        var selected = _frameScenario == index;
        var result = gui.Selectable(selected,
            new ControlBehaviorOptions(Role: ControlRole.Tab, Label: Titles[index]));
        if (result.Activated)
        {
            Queue(gui, () => SelectScenario(index));
        }
        gui.DrawBackgroundRect(selected ? new Color(0x294c70ff) : Panel, 8);
        FocusRing(gui);
        gui.DrawText(Titles[index], 16, selected ? Color.White : Accent);
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
        using var scope = gui.Node(156, 36, id).AlignContent(0.5f, 0.5f).Enter();
        var result = gui.Pressable(new ControlBehaviorOptions(Role: ControlRole.Button, Label: label));
        if (result.Activated)
        {
            Queue(gui, action);
        }
        gui.DrawBackgroundRect(result.Is(ControlVisualState.Hovered) ? new Color(0x294c70ff) : Panel, 6);
        FocusRing(gui);
        gui.DrawText(label, 14, Color.White);
    }

    void DrawPendingScenario(Gui gui)
    {
        using var panel = gui.Node().Expand().Direction(Axis.Vertical).Padding(28).Gap(16).Enter();
        gui.DrawBackgroundRect(Panel, 12);
        gui.DrawText(Titles[_frameScenario], 24, Color.White);
        gui.DrawText("Groups, notes and collapsed containers will have their own scenario here.", 16, Accent);
        gui.DrawText("The Sandbox is available now for graph editing and layout interaction.", 14, Color.White);
        Button(gui, "Open Sandbox", "open-sandbox", () => SelectScenario(0));
    }

    void Queue(Gui gui, Action action)
    {
        _pending.Enqueue(action);
        gui.RequestFrame();
    }

    static void FocusRing(Gui gui)
    {
        if (gui.HasFocus())
        {
            gui.DrawRectBorder(gui.CurrentNode.Rect, Accent, 2);
        }
    }

    static string TabId(int index) => $"scenario-{index}";
}
