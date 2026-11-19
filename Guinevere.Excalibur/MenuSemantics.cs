namespace Guinevere;

public static partial class ControlsExtensions
{
    static void PublishMenuSemantics(Gui gui, string label, bool enabled, bool isChecked,
        ControlRole? role = null)
    {
        if (gui.Pass != Pass.Pass2Render)
        {
            return;
        }
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
        if (isChecked)
        {
            state |= ControlVisualState.Checked;
        }
        sink?.Publish(new ControlSemantics(gui.CurrentNode.Id, role ?? ControlRole.MenuItem, label, null,
            gui.CurrentNode.Rect, state));
    }
}
