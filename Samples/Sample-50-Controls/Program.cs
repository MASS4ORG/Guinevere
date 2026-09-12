using Guinevere;
using Guinevere.OpenGL.SilkNET;

namespace Controls_01;

/// <summary>
/// Core widgets: buttons (plain, colored, styled, icon, edge cases), the input family
/// (checkbox, toggle, text input, password, text area, dropdown), the tab variants
/// (horizontal, pill, vertical), scroll containers and a USS-styled widget panel.
/// </summary>
public abstract partial class Program
{
    public static void Main()
    {
        var gui = new Gui();
        gui.StyleSheets.Add(StyleSheet.Parse(Style));

        using var win = new GuiWindow(gui, 1150, 860, "Controls");
        win.RunGui(() => Draw(gui));
    }

    private static void Draw(Gui gui)
    {
        gui.DrawRect(gui.ScreenRect, Color.FromArgb(255, 245, 245, 245));

        using (gui.Node().Expand().Margin(20).Direction(Axis.Vertical).Gap(15).Enter())
        {
            using (gui.Node().Expand().Margin(15, 0).Enter())
            {
                gui.Tabs(ref _activeTab, tabs =>
                {
                    tabs.Tab("Buttons", () => ButtonsContent(gui));
                    tabs.Tab("Selection", () => SelectionContent(gui));
                    tabs.Tab("Text Inputs", () => TextInputsContent(gui));
                    tabs.Tab("Navigation", () => NavigationContent(gui));
                    tabs.Tab("Scrolling", () => ScrollingContent(gui));
                    tabs.Tab("Focus", () => FocusContent(gui));
                    tabs.Tab("Styling", () => StylingContent(gui));
                });
            }

            using (gui.Node().Height(40).Padding(15, 0).Enter())
            {
                gui.DrawText($"Frame: {gui.Time.Frames} | FPS: {gui.Time.SmoothFps:N1}", size: 12,
                    color: Color.FromArgb(255, 153, 153, 153));
            }
        }
    }

    private static Color Hsb(float hueDegrees)
    {
        var hue = hueDegrees * Math.PI / 180;
        return Color.FromArgb(255,
            (byte)(128 + 127 * Math.Sin(hue)),
            (byte)(128 + 127 * Math.Sin(hue + 120 * Math.PI / 180)),
            (byte)(128 + 127 * Math.Sin(hue + 240 * Math.PI / 180)));
    }

    private static void Section(Gui gui, string title, Action body)
    {
        gui.DrawText(title, size: 18, color: Color.FromArgb(255, 51, 51, 51));
        using (gui.Node(0, 4).Enter()) ;

        body();
        gui.Node(0, 12);
    }
}
