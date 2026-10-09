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
    static bool _nativeTitlebar;

    /// <summary>Runs the control gallery.</summary>
    public static void Main()
    {
        var gui = new Gui();
        gui.StyleSheets.Add(StylingSheet);

        Environment.SetEnvironmentVariable("OPENTK_4_USE_WAYLAND", "0");
        Environment.SetEnvironmentVariable("SILKNET_USE_WAYLAND", "0");
        using var win = new GuiWindow(gui, 1150, 860, "Controls");
        win.RunGui(() => Draw(gui));
    }

    static void Draw(Gui gui)
    {
        ExcaliburStyles.SetTheme(gui, SelectedTheme());
        var chrome = gui.Platform.Require<IWindowChromeCapability>();
        var nativeTitlebar = _nativeTitlebar || !chrome.CanMove;
        using (gui.AppBar(windowControls: !_nativeTitlebar, nativeTitlebar: _nativeTitlebar, resizable: true))
        {
            gui.MenuBar(menu => menu.Collapsible()
                .Menu("File", file => file.Item("Close", () => gui.Platform.Require<IWindowChromeCapability>().RequestClose()))
                .Menu("View", view => view
                    .Item("Toggle theme", () => _radioChoice = _radioChoice == 0 ? 1 : 0)
                    .CheckItem("Native title bar", () => nativeTitlebar, value => _nativeTitlebar = value,
                        enabled: chrome.CanMove)));
            DemoHeader.BadgeMini(gui);
            gui.DrawText("Guinevere Excalibur");
            gui.Node().ExpandWidth();
            gui.DrawText($"FPS: {gui.Time.SmoothFps:N1}", 12, Token(gui, "text"));
            _nativeTitlebar = gui.Checkbox(_nativeTitlebar, "Native title bar");
            if (gui.Button("◐", 36, 36)) _radioChoice = _radioChoice == 0 ? 1 : 0;
        }

        using (gui.Node().Expand().Enter())
        {
            gui.Tabs(ref _activeTab, tabs =>
            {
                tabs.Tab("Buttons", () => ButtonsContent(gui), closable: false);
                tabs.Tab("Selection", () => SelectionContent(gui));
                tabs.Tab("Text Inputs", () => TextInputsContent(gui));
                tabs.Tab("Navigation", () => NavigationContent(gui));
                tabs.Tab("Focus", () => FocusNavigationContent(gui));
                tabs.Tab("Feedback", () => FeedbackContent(gui));
                tabs.Tab("Scrolling", () => ScrollingContent(gui));
                tabs.Tab("Styling", () => StylingContent(gui));
            });
        }
    }

    static StyleSheet SelectedTheme() => _radioChoice switch
    {
        0 => ExcaliburStyles.Light,
        1 => ExcaliburStyles.Dark,
        2 => ExcaliburStyles.MonoLight,
        3 => ExcaliburStyles.MonoDark,
        _ => ExcaliburStyles.Dark
    };

    /// <summary>A color token of the active theme, for the gallery's own drawing.</summary>
    static Color Token(Gui gui, string name) => ExcaliburStyles.TokenColor(gui, name);

    static Color Hsb(float hueDegrees)
    {
        var hue = hueDegrees * Math.PI / 180;
        return Color.FromArgb(255,
            (byte)(128 + 127 * Math.Sin(hue)),
            (byte)(128 + 127 * Math.Sin(hue + 120 * Math.PI / 180)),
            (byte)(128 + 127 * Math.Sin(hue + 240 * Math.PI / 180)));
    }

    static void Section(Gui gui, string title, Action body)
    {
        using (gui.Node().ExpandWidth().Margin(5).Padding(5).Enter())
        {
            gui.DrawText(title, size: 18, color: Token(gui, "text")).MarginBottom(5);
            gui.DrawBackgroundRect(Token(gui, "surface-hover"), 5);
            body();
        }
    }
}
