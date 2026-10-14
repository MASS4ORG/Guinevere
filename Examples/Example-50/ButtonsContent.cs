using Guinevere;

namespace Controls01;

public abstract partial class Program
{
    static int _buttonClickCount;
    static string _lastClickedButton = "None";

    // Buttons take no colors or radii: these classes restyle them from a sheet layered over the default one.
    static readonly StyleSheet ButtonsSheet = StyleSheet.Parse("""
                                                               button.success { background-color = #28a745; color = #ffffff; :hover { background-color = #148f31; } }
                                                               button.warning { background-color = #ffc107; color = #000000; :hover { background-color = #ebad00; } }
                                                               button.danger  { background-color = #dc3545; color = #ffffff; :hover { background-color = #c82131; } }
                                                               button.rounded { border-radius = 20; }
                                                               button.shadow  { border-radius = 0; box-shadow = 0 6px 14px #000000aa, inset 0 1px 0 #ffffff33; }
                                                               button.outline { background-color = #00000000; border-width = 2px; border-color = #6a5acd; :hover { background-color = #7b68ee; } }
                                                               button.muted   { background-color = #80808064; }
                                                               """);

    static void ButtonsContent(Gui gui)
    {
        gui.ScrollY();
        if (!gui.StyleSheets.Contains(ButtonsSheet))
        {
            gui.StyleSheets.Add(ButtonsSheet);
        }

        Section(gui, "Basic Buttons", () =>
        {
            using (gui.Node().Gap(10).Direction(Axis.Horizontal).Wrap(1).Enter())
            {
                if (gui.Button("Auto-sized"))
                {
                    Clicked("Auto-sized");
                }

                if (gui.Button("120x40", 120, 40))
                {
                    Clicked("120x40");
                }

                if (gui.Button("font 12", fontSize: 12))
                {
                    Clicked("60x40 font 12");
                }

                if (gui.Button("*x50 font 18", height: 50, fontSize: 18))
                {
                    Clicked("120x40 font 18");
                }

                if (gui.Button("Disabled", enabled: false))
                {
                    Clicked("Disabled");
                }
            }
        });

        Section(gui, "Semantic", () =>
        {
            using (gui.Node().Direction(Axis.Horizontal).Wrap(1).Gap(10).Enter())
            {
                if (gui.Button("primary", classes: ["primary"]))
                {
                    Clicked("primary");
                }

                if (gui.Button("success", classes: ["success"]))
                {
                    Clicked("success");
                }

                if (gui.Button("warning", classes: ["warning"]))
                {
                    Clicked("warning");
                }

                if (gui.Button("danger", classes: ["danger"]))
                {
                    Clicked("danger");
                }
            }
        });

        Section(gui, "Style Class", () =>
        {
            using (gui.Node().Direction(Axis.Horizontal).Wrap(1).Gap(10).Enter())
            {
                if (gui.Button("rounded", classes: ["rounded"]))
                {
                    Clicked("rounded");
                }

                if (gui.Button("shadow", classes: ["shadow"]))
                {
                    Clicked("shadow");
                }

                if (gui.Button("outline", classes: ["outline"]))
                {
                    Clicked("outline");
                }

                if (gui.Button("muted", classes: ["muted"]))
                {
                    Clicked("muted");
                }
            }
        });

        Section(gui, "Icon Buttons", () =>
        {
            using (gui.Node().Direction(Axis.Horizontal).Wrap(1).Gap(10).Enter())
            {
                if (gui.Button("⚙️"))
                {
                    Clicked("Settings Icon");
                }

                if (gui.Button("➕ Create"))
                {
                    Clicked("➕ Create");
                }
            }
        });

        Section(gui, "Edge Cases", () =>
        {
            using (gui.Node().Direction(Axis.Horizontal).Wrap(1).Gap(10).Enter())
            {
                Button(gui, "", width: 50, height: 30, label: "Empty");
                Button(gui, "This is a very long button text that should auto-size properly",
                    label: "Long Text");
                Button(gui, "T", width: 20, height: 20, fontSize: 10, label: "Tiny");
            }
        });

        Section(gui, "Click Statistics", () =>
        {
            gui.SetTextColor(Token(gui, "text"));
            gui.SetTextSize(14);
            gui.DrawText($"Total clicks: {_buttonClickCount}");
            gui.DrawText($"Last clicked: {_lastClickedButton}");
        });
    }

    static void Button(Gui gui, string text, float width = 0, float height = 0, float? fontSize = null,
        string? label = null, bool enabled = true, string[]? classes = null)
    {
        var showLabel = label ?? text;
        if (gui.Button(text, width: width, height: height, fontSize: fontSize, enabled: enabled, classes: classes))
        {
            Clicked(showLabel);
        }
    }

    static void Clicked(string name)
    {
        _buttonClickCount++;
        _lastClickedButton = name;
    }
}
