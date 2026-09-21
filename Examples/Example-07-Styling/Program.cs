using Guinevere;

namespace Example_76_Styling;

public static class Program
{
    static bool _selected = true;
    static bool _lightTheme;
    static float _progress = 0.65f;

    public static void Main()
    {
        var gui = new Gui();
        var source = StyleSheetSource.FromProvider(Theme);
        gui.AddStyleSheet(source);
        using var window = new GuiWindow(gui, 900, 620, "Guinevere styling showcase");
        window.RunGui(() => Draw(gui, source));
    }

    static void Draw(Gui gui, StyleSheetSource source)
    {
        var primaryText = _lightTheme ? Color.FromArgb(255, 30, 38, 52) : Color.White;
        var secondaryText = _lightTheme
            ? Color.FromArgb(255, 82, 92, 110)
            : Color.FromArgb(255, 180, 190, 205);
        using (gui.StyledNode("screen").Enter())
        {
            gui.DrawText("Styling system", 30, primaryText);
            gui.DrawText("Nested selectors, inheritance, modifiers, variables and live theme reload", 16,
                secondaryText);

            using (gui.StyledNode("toolbar").Enter())
            {
                StyledButton(gui, _lightTheme ? "Use dark theme" : "Use light theme", () =>
                {
                    _lightTheme = !_lightTheme;
                    source.TryReload();
                });
                StyledButton(gui, _selected ? "Selected" : "Select me", () => _selected = !_selected,
                    _selected ? ["selected"] : []);
            }

            using (gui.StyledNode("feature-card").Enter())
            {
                gui.DrawText("Inherited card", 21, primaryText);
                gui.DrawText("feature-card #inherit(card) receives the card layout and participates in card selectors.",
                    14, secondaryText);

                using (gui.StyledNode("progress-track").Enter())
                using (gui.StyledNode("progress-fill", variables: [new StyleVariable("progress", $"{_progress * 100}%")])
                           .Enter()) { }

                using (gui.StyledNode("toolbar").Enter())
                {
                    StyledButton(gui, "Less", () => _progress = Math.Max(0.1f, _progress - 0.1f));
                    StyledButton(gui, "More", () => _progress = Math.Min(1f, _progress + 0.1f));
                }
            }
        }
    }

    static void StyledButton(Gui gui, string label, Action clicked, IReadOnlyList<string>? modifiers = null)
    {
        var node = gui.StyledNode("button", modifiers ?? []);
        if (node.OnClick()) clicked();
        using (node.Enter()) gui.DrawText(label, 15, Color.White);
    }

    static string Theme() => $$"""
        @const radius = 10;
        $accent = {{(_lightTheme ? "#7157d9" : "#3d8bfd")}};

        screen {
            width = expand;
            height = expand;
            padding = 32;
            gap = 16;
            bg-color = {{(_lightTheme ? "#e9edf5" : "#111722")}};

            toolbar {
                flow-dir = x;
                gap = 10;

                > button { min-width = 120; }
            }
        }

        card {
            padding = 20;
            gap = 14;
            border-radius = @radius;
            bg-color = {{(_lightTheme ? "#ffffff" : "#202a3a")}};
            border-color = {{(_lightTheme ? "#ccd2dd" : "#354158")}};
            border-width = 1;
        }

        feature-card #inherit(card) { width = expand; }

        button {
            padding = 10 16;
            border-radius = 8;
            bg-color = #354158;
            border-color = #53627a;
            border-width = 1;

            :hover(0.15 ease-out) { bg-color = #465875; }
            :selected { bg-color = $accent; border-color = $accent; }
        }

        progress-track {
            width = expand;
            height = 24;
            border-radius = 12;
            bg-color = #151c28;

            > progress-fill {
                width = $progress;
                height = expand;
                border-radius = 12;
                bg-color = $accent;
            }
        }
        """;
}
