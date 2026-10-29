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
        using (var search = gui.StyledNode("graph-search", id: "fsm-node-search").Width(360).Height(32).Padding(8).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                gui.RegisterFocusable();
                gui.ControlSemantics?.Publish(new ControlSemantics("fsm-node-search", ControlRole.None,
                    "Search registered FSM node types", _query, gui.CurrentNode.Rect, ControlVisualState.None));
            }
            gui.CurrentNode.On<PointerDownEvent>(_ => gui.Focus.RequestFocus("fsm-node-search"));
            gui.CurrentNode.On<TextInputEvent>(e => { _query += e.Text; e.PreventDefault(); });
            gui.CurrentNode.On<KeyDownEvent>(e => SearchKey(gui, e));
            gui.DrawText(_query.Length == 0 ? "Search node types…" : _query);
        }
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
            case KeyboardKey.Backspace when _query.Length > 0:
                _query = _query[..StringInfo.ParseCombiningCharacters(_query)[^1]];
                break;
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
