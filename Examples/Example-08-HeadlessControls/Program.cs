using Guinevere;

namespace Example_08_HeadlessControls;

public static class Program
{
    static bool _armed;
    static int _launches;

    public static void Main()
    {
        var gui = new Gui();
        using var window = new GuiWindow(gui, 520, 280, "Headless controls");
        window.RunGui(() => Draw(gui));
    }

    static void Draw(Gui gui)
    {
        using (gui.Node().Expand().Padding(32).Gap(18).Enter())
        {
            gui.DrawText("Custom game skin", 26, Color.White);
            CustomToggle(gui, "Arm launch", ref _armed);
            if (CustomButton(gui, "Launch", enabled: _armed)) _launches++;
            gui.DrawText($"Launches: {_launches}", 16, Color.FromArgb(255, 170, 190, 220));
        }
    }

    static bool CustomButton(Gui gui, string label, bool enabled)
    {
        using (gui.Node(180, 46, $"button/{label}").Enter())
        {
            var result = gui.Pressable(
                new ControlBehaviorOptions(enabled, Role: ControlRole.Button, Label: label),
                state =>
                {
                    if (gui.Pass != Pass.Pass2Render) return;
                    var color = state.HasFlag(ControlVisualState.Disabled)
                        ? Color.FromArgb(255, 45, 50, 62)
                        : state.HasFlag(ControlVisualState.Pressed)
                            ? Color.FromArgb(255, 240, 95, 65)
                            : state.HasFlag(ControlVisualState.Hovered)
                                ? Color.FromArgb(255, 255, 145, 75)
                                : Color.FromArgb(255, 235, 115, 60);
                    gui.DrawBackgroundRect(color, 12);
                });
            gui.DrawText(label, 18, enabled ? Color.White : Color.Gray, centerInRect: true);
            return result.Activated;
        }
    }

    static void CustomToggle(Gui gui, string label, ref bool value)
    {
        using (gui.Node(260, 38, $"toggle/{label}").Direction(Axis.Horizontal).Gap(12).Enter())
        {
            var result = gui.Toggleable(value,
                new ControlBehaviorOptions(Role: ControlRole.Switch, Label: label));
            if (result.Activated) value = !value;

            using (gui.Node(64, 32).Enter())
            {
                if (gui.Pass == Pass.Pass2Render)
                    gui.DrawBackgroundRect(value
                        ? Color.FromArgb(255, 50, 190, 135)
                        : Color.FromArgb(255, 70, 78, 94), 16);
            }
            gui.DrawText(label, 17, Color.White);
        }
    }
}
