using Guinevere;
using GuinevereDemos;

namespace Controls_01;

/// <summary>
/// Core widgets: buttons (plain, colored, styled, icon, edge cases), the input family
/// (checkbox, toggle, text input, password, text area, dropdown), the tab variants
/// (horizontal, pill, vertical), scroll containers and a USS-styled widget panel.
/// </summary>
public abstract partial class Program
{
    private static readonly Color SectionColor = Color.DarkGray;

    public static void Main()
    {
        var gui = new Gui();
        gui.StyleSheets.Add(StyleSheet.Parse(Style));

        using var win = new GuiWindow(gui, 1150, 860, "Controls");
        win.RunGui(() => Draw(gui));
    }

    private static void Draw(Gui gui)
    {
        DemoHeader.Header(gui, "Guinevere Excalibur");

        using (gui.Node().Expand().Enter())
        {
            gui.Tabs(ref _activeTab, tabs =>
            {
                tabs.Tab("Buttons", () => ButtonsContent(gui), closable: false);
                tabs.Tab("Selection", () => SelectionContent(gui));
                tabs.Tab("Text Inputs", () => TextInputsContent(gui));
                tabs.Tab("Navigation", () => NavigationContent(gui));
                tabs.Tab("Feedback", () => FeedbackContent(gui));
                tabs.Tab("Scrolling", () => ScrollingContent(gui));
                tabs.Tab("Styling", () => StylingContent(gui));
            });
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
        using (gui.Node().ExpandWidth().Margin(5).Padding(5).Enter())
        {
            gui.DrawText(title, size: 18, color: Color.FromArgb(255, 51, 51, 51)).MarginBottom(5);
            gui.DrawBackgroundRect(SectionColor, 5);
            body();
        }
    }
}
