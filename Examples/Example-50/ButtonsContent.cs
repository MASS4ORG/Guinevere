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

    static readonly Icon SettingsIcon = Icon.FromGlyph("⚙");
    static readonly Icon OpenIcon = Icon.FromGlyph(WidgetIcons.Folder);
    static bool _german;

    /// <summary>A localization key: any type can become button content by declaring a conversion.</summary>
    sealed record Localized(string English, string German) : IContent
    {
        public string? Label => _german ? German : English;

        public void Build(Gui gui) => gui.DrawText(Label!, centerInRect: false);

        public static implicit operator Content(Localized text) => Content.From(text);
    }

    static readonly Localized SaveText = new("Save", "Speichern");

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

                if (gui.Button("120x40").Width(120).Height(40).Clicked)
                {
                    Clicked("120x40");
                }

                if (gui.Button("font 12", classes: ["small"]))
                {
                    Clicked("font 12");
                }

                if (gui.Button("*x50 font 18", classes: ["large"]).Height(50).Clicked)
                {
                    Clicked("*x50 font 18");
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

        Section(gui, "Any Content", () =>
        {
            using (gui.Node().Direction(Axis.Horizontal).Wrap(1).Gap(10).AlignContentY(0.5f).Enter())
            {
                if (gui.Button(SettingsIcon.Labeled("Settings")))
                {
                    Clicked("Settings icon");
                }

                if (gui.Button(SettingsIcon.Labeled("Settings (large)")).Width(48).Height(48).Clicked)
                {
                    Clicked("Settings icon 48");
                }

                if (gui.Button("➕ Create (emoji text)"))
                {
                    Clicked("➕ Create");
                }

                if (gui.Button(Content.Row(OpenIcon, "Open")))
                {
                    Clicked("Icon + text");
                }

                if (gui.Button(SaveText, classes: ["primary"]))
                {
                    Clicked(SaveText.Label!);
                }

                if (gui.Button(Content.Text(() => _german ? "Deutsch" : "English")).Clicked)
                {
                    _german = !_german;
                    Clicked("Language");
                }

                var nested = gui.Button(Content.Build(() =>
                {
                    gui.DrawText("Widgets inside:", centerInRect: false);
                    gui.ProgressBar(_buttonClickCount % 10 / 10f).Width(60);
                }).Labeled("Progress"));
                if (nested.Clicked)
                {
                    Clicked("Nested widgets");
                }
            }
        });

        Section(gui, "Edge Cases", () =>
        {
            using (gui.Node().Direction(Axis.Horizontal).Wrap(1).Gap(10).Enter())
            {
                if (gui.Button("").Width(50).Height(30).Clicked)
                {
                    Clicked("Empty");
                }

                if (gui.Button("This is a very long button text that should auto-size properly"))
                {
                    Clicked("Long Text");
                }

                if (gui.Button("T", classes: ["small"]).Width(20).Height(20).Clicked)
                {
                    Clicked("Tiny");
                }
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

    static void Clicked(string name)
    {
        _buttonClickCount++;
        _lastClickedButton = name;
    }
}
