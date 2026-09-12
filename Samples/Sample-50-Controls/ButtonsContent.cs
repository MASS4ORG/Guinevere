using Guinevere;

namespace Controls_01;

public abstract partial class Program
{
    private static int _buttonClickCount;
    private static int _iconButtonClickCount;
    private static string _lastClickedButton = "None";

    private static void ButtonsContent(Gui gui)
    {
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
                ColoredButton(gui, "Primary", 0, 123, 255);
                ColoredButton(gui, "Success", 40, 167, 69);
                ColoredButton(gui, "Warning", 255, 193, 7, Color.Black);
                ColoredButton(gui, "Danger", 220, 53, 69);
            }
        });

        Section(gui, "Styled Buttons", () =>
        {
            using (gui.Node().Height(40).Direction(Axis.Horizontal).Gap(10).Enter())
            {
                Button(gui, "Rounded", radius: 20, width: 100);
                Button(gui, "Square", radius: 0, width: 80);
                Button(gui, "Big", fontSize: 20, width: 120, height: 45);

                if (gui.Button("Custom", width: 80,
                        backgroundColor: Color.FromArgb(255, 106, 90, 205),
                        color: Color.White,
                        hoverColor: Color.FromArgb(255, 123, 104, 238)))
                    Clicked("Custom");
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

                if (gui.Button("⚙️", 40, backgroundColor: Color.FromArgb(100, 128, 128, 128)))
                {
                    _iconButtonClickCount++;
                    Clicked("Settings Icon");
                }

                if (gui.Button("❤️", 40, hoverColor: Color.FromArgb(255, 255, 182, 193)))
                {
                    _iconButtonClickCount++;
                    Clicked("Heart Icon");
                }

                if (gui.Button("⭐", 40, backgroundColor: Color.Black,
                        color: Color.Black))
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
            gui.DrawText($"Button clicks: {_buttonClickCount}", size: 14, color: Color.FromArgb(255, 102, 102, 102));
            gui.DrawText($"Icon button clicks: {_iconButtonClickCount}", size: 14,
                color: Color.FromArgb(255, 102, 102, 102));
            gui.DrawText($"Total clicks: {_buttonClickCount + _iconButtonClickCount}", size: 14,
                color: Color.FromArgb(255, 102, 102, 102));
            gui.DrawText($"Last clicked: {_lastClickedButton}", size: 14, color: Color.FromArgb(255, 102, 102, 102));
        });
    }

    private static void Button(Gui gui, string text, float width = 0, float height = 0, float? fontSize = null,
        float radius = 4, string? label = null)
    {
        var showLabel = label ?? text;
        if (gui.Button(text, width: width, height: height, fontSize: fontSize, radius: radius))
            Clicked(showLabel);
    }

    private static void ColoredButton(Gui gui, string text, int r, int g, int b, Color? textColor = null)
    {
        if (gui.Button(text,
                backgroundColor: Color.FromArgb(255, r, g, b),
                hoverColor: Color.FromArgb(255, Math.Max(0, r - 20), Math.Max(0, g - 20), Math.Max(0, b - 20)),
                color: textColor ?? Color.White))
            Clicked(text);
    }

    private static void Clicked(string name)
    {
        _buttonClickCount++;
        _lastClickedButton = name;
    }

}
