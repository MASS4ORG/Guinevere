namespace Example02;

/// <summary>An application command shared by menus and compact toolbars.</summary>
/// <param name="Menu">The main menu containing the command.</param>
/// <param name="Label">The readable action name.</param>
/// <param name="Id">The stable command identity.</param>
/// <param name="Run">The action committed before the next build.</param>
public sealed record DemoCommand(string Menu, string Label, string Id, Action Run);

/// <summary>Excalibur presentation shared by the playground's scenarios.</summary>
public static class DemoControls
{
    /// <summary>Draws a labeled Excalibur action, using an icon and tooltip for common commands.</summary>
    public static bool Button(Gui gui, string label, string id)
    {
        using var host = gui.Node().Width(UnitValue.Fit).Height(28).Enter();
        var clicked = DemoIcons.Actions.TryGetValue(label, out var icon)
            ? gui.IconButton(icon, label, size: 28, id: id)
            : gui.Button(label, height: 28, id: id);
        if (icon is null)
        {
            Publish(gui, id, label, null, ControlRole.Button);
        }
        gui.Tooltip(gui.CurrentNode, label);
        return clicked;
    }

    /// <summary>Draws an Excalibur text field with a stable readable label and optional application keys.</summary>
    public static void TextInput(Gui gui, ref string text, string label, string id, float width = 360,
        Action<KeyDownEvent>? onKey = null)
    {
        using var host = gui.Node().Width(width).Height(28).Enter();
        if (onKey is not null)
        {
            gui.CurrentNode.On(onKey);
        }
        gui.TextInput(ref text, width: width, height: 28, placeholder: label, id: id);
        Publish(gui, id, label, text, ControlRole.None);
    }

    static void Publish(Gui gui, string id, string label, string? value, ControlRole role)
    {
        if (gui.Pass != Pass.Pass2Render)
        {
            return;
        }
        var state = gui.HasFocus(id) ? ControlVisualState.Focused : ControlVisualState.None;
        var sink = gui.ControlSemantics;
        if (sink is null && gui.Platform.TryGet<IAccessibilityCapability>(out var bridge))
        {
            sink = bridge;
        }
        sink?.Publish(new ControlSemantics(id, role, label, value, gui.CurrentNode.Rect, state));
    }
}
