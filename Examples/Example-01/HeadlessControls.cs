using Guinevere;

namespace Example01;

public partial class Shared
{
    static readonly string[] SkinNames = ["Arcade", "Outline", "Pill"];
    static int _skin;
    static bool _armed;
    static int _launches;
    static ControlVisualState _lastState;

    /// <summary>The same Pressable and Toggleable behavior drawn by three unrelated skins.</summary>
    static void HeadlessControls(Gui gui, ColorPalette p)
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(18).Enter())
        {
            using (gui.Node().Direction(Axis.Horizontal).Gap(14).ContentAlignY(0.5f).Enter())
            {
                Label(gui, p, "Skin");
                Choice(gui, p, "skin", SkinNames, _skin, index => _skin = index);
            }

            using (gui.Node().ExpandWidth().Direction(Axis.Vertical).Padding(20).Gap(16).Enter())
            {
                gui.DrawBackgroundRect(p.SurfaceRaised, 12);
                CustomToggle(gui, p, "Arm launch", ref _armed);
                if (CustomButton(gui, p, "Launch", _armed))
                {
                    Later(() => _launches++);
                }

                gui.DrawText($"Launches: {_launches}", 16, p.TextDim);
            }

            Label(gui, p, "Behavior state, as the skins see it (hover, press and Tab to the controls)");
            gui.DrawText(_lastState == ControlVisualState.None ? "None" : _lastState.ToString(), 15, p.Accent);
        }
    }

    static bool CustomButton(Gui gui, ColorPalette p, string label, bool enabled)
    {
        using (gui.Node(190, 46, $"button/{label}").AlignContent(0.5f, 0.5f).Enter())
        {
            var result = gui.Pressable(
                new ControlBehaviorOptions(enabled, Role: ControlRole.Button, Label: label),
                state =>
                {
                    if (gui.Pass != Pass.Pass2Render)
                    {
                        return;
                    }

                    _lastState = state;
                    var pressed = state.HasFlag(ControlVisualState.Pressed);
                    var hovered = state.HasFlag(ControlVisualState.Hovered);
                    var accent = state.HasFlag(ControlVisualState.Disabled) ? p.Border
                        : pressed ? Color.Lerp(p.Warning, p.Negative, 0.5f) : hovered ? p.Warning : p.Series[2];
                    switch (_skin)
                    {
                        case 0:
                            gui.DrawBackgroundRect(accent, 12);
                            break;
                        case 1:
                            gui.DrawBackgroundRect(pressed ? new Color(accent, 0.3f) : new Color(accent, 0.08f), 6);
                            gui.DrawRectBorder(gui.CurrentNode.Rect, accent, 2f);
                            break;
                        default:
                            gui.DrawBackgroundRect(accent, 23);
                            break;
                    }

                    if (state.HasFlag(ControlVisualState.Focused))
                    {
                        gui.DrawRectBorder(gui.CurrentNode.Rect, p.Text, 2f);
                    }
                });
            gui.DrawText(label, 18, enabled ? _skin == 1 ? p.Text : Color.White : p.TextDim);
            return result.Activated;
        }
    }

    static void CustomToggle(Gui gui, ColorPalette p, string label, ref bool value)
    {
        using (gui.Node(280, 38, $"toggle/{label}").Direction(Axis.Horizontal).Gap(12).ContentAlignY(0.5f).Enter())
        {
            var result = gui.Toggleable(value,
                new ControlBehaviorOptions(Role: ControlRole.Switch, Label: label));
            if (result.Activated)
            {
                value = !value;
            }

            var knob = gui.AnimateFloat(value ? 1f : 0f, 0.15f);
            using (gui.Node(64, 32).Enter())
            {
                var track = gui.CurrentNode.Rect;
                var radius = _skin == 1 ? 6f : 16f;
                gui.DrawBackgroundRect(Color.Lerp(p.Border, p.Positive, knob), radius);
                var side = _skin == 1 ? 22f : 24f;
                gui.DrawRect(new Rect(track.X + 4 + knob * (track.W - side - 8), track.Y + (track.H - side) / 2, side, side),
                    Color.White, _skin == 1 ? 4f : side / 2);
            }

            gui.DrawText(label, 17, p.Text);
        }
    }
}
