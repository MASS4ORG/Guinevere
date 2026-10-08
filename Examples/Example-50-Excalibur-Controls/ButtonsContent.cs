using Guinevere;

namespace Controls_01;

public abstract partial class Program
{
    static int _buttonClickCount;
    static int _iconButtonClickCount;
    static string _lastClickedButton = "None";

    // Buttons take no colors or radii: these classes restyle them from a sheet layered over the default one.
    static readonly StyleSheet ButtonsSheet = StyleSheet.Parse("""
        button.success { background-color = #28a745; color = #ffffff; :hover { background-color = #148f31; } }
        button.warning { background-color = #ffc107; color = #000000; :hover { background-color = #ebad00; } }
        button.danger  { background-color = #dc3545; color = #ffffff; :hover { background-color = #c82131; } }
        button.rounded { border-radius = 20; }
        button.square  { border-radius = 0; }
        button.custom  { background-color = #6a5acd; color = #ffffff; :hover { background-color = #7b68ee; } }
        button.muted   { background-color = #80808064; }
        button.love    { :hover { background-color = #ffb6c1; } }
        button.ink     { color = #000000; }
        """);

    static void ButtonsContent(Gui gui)
    {
        if (!gui.StyleSheets.Contains(ButtonsSheet)) gui.StyleSheets.Add(ButtonsSheet);

        Section(gui, "Basic Buttons", () =>
        {
            using (gui.Node().Height(40).Direction(Axis.Horizontal).Gap(10).Enter())
            {
                Button(gui, "Auto-sized");
                Button(gui, "Fixed Size", width: 120, height: 40);
                Button(gui, "Small", width: 60, height: 24, fontSize: 12);
                Button(gui, "Large Button", width: 150, height: 50, fontSize: 18);
            }
        });

        Section(gui, "Colored Buttons", () =>
        {
            using (gui.Node().Height(40).Direction(Axis.Horizontal).Gap(10).Enter())
            {
                Button(gui, "Primary", classes: ["primary"]);
                Button(gui, "Success", classes: ["success"]);
                Button(gui, "Warning", classes: ["warning"]);
                Button(gui, "Danger", classes: ["danger"]);
            }
        });

        Section(gui, "Styled Buttons", () =>
        {
            using (gui.Node().Height(40).Direction(Axis.Horizontal).Gap(10).Enter())
            {
                Button(gui, "Rounded", width: 100, classes: ["rounded"]);
                Button(gui, "Square", width: 80, classes: ["square"]);
                Button(gui, "Big", fontSize: 20, width: 120, height: 45);
                Button(gui, "Custom", width: 80, classes: ["custom"]);
            }
        });

        Section(gui, "Icon Buttons", () =>
        {
            using (gui.Node().Height(40).Direction(Axis.Horizontal).Gap(10).Enter())
            {
                if (gui.Button("🔍", 40))
                {
                    _iconButtonClickCount++;
                    Clicked("Search Icon");
                }

                if (gui.Button("⚙️", 40, classes: ["muted"]))
                {
                    _iconButtonClickCount++;
                    Clicked("Settings Icon");
                }

                if (gui.Button("❤️", 40, classes: ["love"]))
                {
                    _iconButtonClickCount++;
                    Clicked("Heart Icon");
                }

                if (gui.Button("⭐", 40, classes: ["ink"]))
                {
                    _iconButtonClickCount++;
                    Clicked("Star Icon");
                }

                if (gui.Button("🚀", 60, fontSize: 24))
                {
                    _iconButtonClickCount++;
                    Clicked("Rocket Icon");
                }
            }
        });

        Section(gui, "Disabled Buttons", () =>
        {
            using (gui.Node().Height(40).Direction(Axis.Horizontal).Gap(10).Enter())
            {
                Button(gui, "Disabled", enabled: false);
                Button(gui, "Fixed Size", width: 120, enabled: false);
                Button(gui, "Disabled Primary", enabled: false, classes: ["primary"]);
                Button(gui, "Rounded", width: 100, enabled: false, classes: ["rounded"]);

                if (gui.Button("⚙️", 40, enabled: false))
                {
                    _iconButtonClickCount++;
                    Clicked("Disabled Icon");
                }
            }
        });

        Section(gui, "Edge Cases", () =>
        {
            using (gui.Node().Height(40).Direction(Axis.Horizontal).Gap(10).Enter())
            {
                Button(gui, "", width: 50, height: 30, label: "Empty");
                Button(gui, "This is a very long button text that should auto-size properly",
                    label: "Long Text");
                Button(gui, "T", width: 20, height: 20, fontSize: 10, label: "Tiny");
            }
        });

        Section(gui, "Click Statistics", () =>
        {
            gui.SetTextColor(gui.ControlPalette.Text);
            gui.SetTextSize(14);
            gui.DrawText($"Button clicks: {_buttonClickCount}");
            gui.DrawText($"Icon button clicks: {_iconButtonClickCount}");
            gui.DrawText($"Total clicks: {_buttonClickCount + _iconButtonClickCount}");
            gui.DrawText($"Last clicked: {_lastClickedButton}");
        });
    }

    static void Button(Gui gui, string text, float width = 0, float height = 0, float? fontSize = null,
        string? label = null, bool enabled = true, string[]? classes = null)
    {
        var showLabel = label ?? text;
        if (gui.Button(text, width: width, height: height, fontSize: fontSize, enabled: enabled, classes: classes))
            Clicked(showLabel);
    }

    static void Clicked(string name)
    {
        _buttonClickCount++;
        _lastClickedButton = name;
    }

}
