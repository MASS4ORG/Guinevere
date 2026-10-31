namespace Example02;

public sealed partial class FsmScene
{
    bool _paletteOpen;
    string _query = "";

    void DrawPalette(Gui gui, Action<string, string, Action> button)
    {
        if (!_paletteOpen)
        {
            return;
        }
        using var palette = gui.Node().ExpandWidth().Height(UnitValue.Fit).Direction(Axis.Vertical).Gap(6).Enter();
        DemoControls.TextInput(gui, ref _query, "Search registered FSM node types", "fsm-node-search",
            onKey: e => SearchKey(gui, e));
        using var choices = gui.Node().ExpandWidth().Height(UnitValue.Fit).Direction(Axis.Horizontal).Gap(8).Wrap(1).Enter();
        foreach (var type in View.Palette(_query))
        {
            button(type.DisplayName, "fsm-create-" + type.Id, () => CreateNode(gui, type));
        }
    }

    void SearchKey(Gui gui, KeyDownEvent e)
    {
        switch (e.Key)
        {
            case KeyboardKey.Escape:
                _paletteOpen = false;
                break;
            case KeyboardKey.Enter:
                if (View.Palette(_query).FirstOrDefault() is { } type)
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

    void CreateNode(Gui gui, NodeType type)
    {
        var id = View.AddNode(type, View.Canvas.VisibleBounds.Center);
        if (!id.IsEmpty)
        {
            View.Canvas.Navigation.Select(id.ToString());
            gui.Focus.RequestFocus("fsm-canvas");
        }
        _paletteOpen = false;
    }
}
