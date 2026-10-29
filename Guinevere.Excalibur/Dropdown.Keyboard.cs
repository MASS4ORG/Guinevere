namespace Guinevere;

public static partial class ControlsExtensions
{
    static void DropdownKey(Gui gui, KeyDownEvent e, string id, int count, int selected,
        DropdownState state, bool enabled)
    {
        if (!enabled || count == 0 || !OpensDropdown(e.Key))
        {
            return;
        }
        state.RequestedOpen = true;
        var next = selected >= 0 ? selected : e.Key == KeyboardKey.Up ? count - 1 : 0;
        gui.Focus.RequestFocus($"{id}/list/{next}", FocusReason.Keyboard);
        gui.RequestFrame();
        e.PreventDefault();
        e.StopPropagation();
    }

    static bool OpensDropdown(KeyboardKey key) => key is KeyboardKey.Enter or KeyboardKey.Space
        or KeyboardKey.Down or KeyboardKey.Up;

    static void DropdownOptionKey(Gui gui, KeyDownEvent e, string id, int count, int option, DropdownState state)
    {
        var next = DropdownNextOption(e.Key, count, option);
        if (next >= 0)
        {
            gui.Focus.RequestFocus($"{id}/list/{next}", FocusReason.Keyboard);
        }
        else if (e.Key is KeyboardKey.Enter or KeyboardKey.Space)
        {
            state.RequestedSelection = option;
            CloseDropdown(gui, id, state);
        }
        else if (e.Key == KeyboardKey.Escape)
        {
            CloseDropdown(gui, id, state);
        }
        else
        {
            return;
        }
        gui.RequestFrame();
        e.PreventDefault();
        e.StopPropagation();
    }

    static int DropdownNextOption(KeyboardKey key, int count, int option) => key switch
    {
        KeyboardKey.Down => (option + 1) % count,
        KeyboardKey.Up => (option + count - 1) % count,
        KeyboardKey.Home => 0,
        KeyboardKey.End => count - 1,
        _ => -1
    };

    static void CloseDropdown(Gui gui, string id, DropdownState state)
    {
        state.RequestedOpen = false;
        gui.Focus.RequestFocus($"{id}/button", FocusReason.Keyboard);
    }

    static void PublishDropdown(Gui gui, string label, string value, bool enabled, bool open)
    {
        var sink = gui.ControlSemantics;
        if (sink is null && gui.Platform.TryGet<IAccessibilityCapability>(out var bridge))
        {
            sink = bridge;
        }
        var state = enabled ? ControlVisualState.None : ControlVisualState.Disabled;
        if (gui.HasFocus())
        {
            state |= ControlVisualState.Focused;
        }
        sink?.Publish(new ControlSemantics(gui.CurrentNode.Id, ControlRole.Button, label,
            $"{value}; {(open ? "expanded" : "collapsed")}", gui.CurrentNode.Rect, state));
    }
}
