using System.Numerics;
using Guinevere;
using GuinevereDemos;

namespace Controls_01;

/// <summary>
/// The Excalibur control gallery hung on the docking system: every widget group is a dock panel that
/// can be split, tabbed, reordered, floated or closed. The toolbar saves, restores and resets layouts.
/// </summary>
public abstract partial class Program
{
    static bool _nativeTitlebar;
    static bool _followSystem = true;

    // Layout edits are deferred to the next frame's build pass so both passes of a frame see one tree.
    static Action? _pendingLayout;

    static readonly DockTheme Theme = new();
    static DockLayout _layout = SeedLayout();
    static string? _saved;
    static string? _menuPanelId;

    static readonly string[] PanelIds =
    [
        "buttons", "selection", "text-inputs", "navigation", "focus", "feedback", "scrolling", "styling",
        "colors"
    ];

    static readonly Dictionary<string, string> PanelTitles = new()
    {
        ["buttons"] = "Buttons",
        ["selection"] = "Selection",
        ["text-inputs"] = "Text Inputs",
        ["navigation"] = "Navigation",
        ["focus"] = "Focus",
        ["feedback"] = "Feedback",
        ["scrolling"] = "Scrolling",
        ["styling"] = "Styling",
        ["colors"] = "Colors"
    };

    static readonly Dictionary<string, Action<Gui>> Panels = new()
    {
        ["buttons"] = ButtonsContent,
        ["selection"] = SelectionContent,
        ["text-inputs"] = TextInputsContent,
        ["navigation"] = NavigationContent,
        ["focus"] = FocusNavigationContent,
        ["feedback"] = FeedbackContent,
        ["scrolling"] = ScrollingContent,
        ["styling"] = StylingContent,
        ["colors"] = ColorsContent
    };

    // Bodies without their own scroll container get one; the rest manage scrolling themselves.
    static readonly HashSet<string> ScrollPanels = ["buttons", "selection", "text-inputs", "feedback", "focus"];

    // Cached: the tab resolver runs once per tab per pass, so build the icon delegates once.
    static readonly Dictionary<string, Action<Gui>> PanelIcons = new()
    {
        ["buttons"] = g => Dot(g, Color.FromArgb(255, 120, 170, 255)),
        ["selection"] = g => Dot(g, Color.FromArgb(255, 120, 210, 150)),
        ["text-inputs"] = g => Dot(g, Color.FromArgb(255, 235, 190, 110)),
        ["navigation"] = g => Dot(g, Color.FromArgb(255, 220, 130, 200)),
        ["focus"] = g => Dot(g, Color.FromArgb(255, 200, 200, 200)),
        ["feedback"] = g => Dot(g, Color.FromArgb(255, 100, 200, 180)),
        ["scrolling"] = g => Dot(g, Color.FromArgb(255, 180, 160, 255)),
        ["styling"] = g => Dot(g, Color.FromArgb(255, 255, 160, 112)),
        ["colors"] = g => Dot(g, Color.FromArgb(255, 160, 224, 255))
    };

    /// <summary>Runs the control gallery.</summary>
    public static void Main()
    {
        var gui = new Gui();
        gui.StyleSheets.Add(StylingSheet);

        Environment.SetEnvironmentVariable("OPENTK_4_USE_WAYLAND", "0");
        Environment.SetEnvironmentVariable("SILKNET_USE_WAYLAND", "0");
        using var win = new GuiWindow(gui, 1360, 900, "Guinevere Excalibur — Controls & Docking");
        win.RunGui(() => Draw(gui));
    }

    static DockLayout SeedLayout()
    {
        var layout = new DockLayout();
        foreach (var panelId in new[] { "buttons", "selection", "text-inputs", "colors", "focus" })
            layout.DockAtEdge(panelId, DockZone.Center);
        layout.DockAtEdge("navigation", DockZone.Left, 0.2f);
        layout.DockAtEdge("styling", DockZone.Right, 0.24f);
        layout.DockAtEdge("scrolling", DockZone.Bottom, 0.3f);
        layout.DockAtEdge("feedback", DockZone.Bottom, 0.3f);
        return layout;
    }

    static void Draw(Gui gui)
    {
        if (gui.Pass == Pass.Pass1Build && _pendingLayout is { } pending)
        {
            _pendingLayout = null;
            pending();
        }

        if (_followSystem) ExcaliburStyles.FollowSystemAppearance(gui);
        else ExcaliburStyles.SetTheme(gui, SelectedTheme());

        var chrome = gui.Platform.Require<IWindowChromeCapability>();
        var nativeTitlebar = _nativeTitlebar || !chrome.CanMove;
        using (gui.AppBar(windowControls: !_nativeTitlebar, nativeTitlebar: _nativeTitlebar, resizable: true))
        {
            gui.MenuBar(menu => menu.Collapsible()
                .Menu("File", file => file.Item("Close", () => gui.Platform.Require<IWindowChromeCapability>().RequestClose()))
                .Menu("View", view => view
                    .Item("Toggle theme", ToggleTheme)
                    .CheckItem("Follow system theme", () => _followSystem, value => _followSystem = value)
                    .CheckItem("Native title bar", () => nativeTitlebar, value => _nativeTitlebar = value,
                        enabled: chrome.CanMove)));
            DemoHeader.BadgeMini(gui);
            gui.DrawText("Guinevere Excalibur");
            gui.Node().ExpandWidth();
            gui.DrawText($"FPS: {gui.Time.SmoothFps:N1}", 12, Token(gui, "text"));
            _nativeTitlebar = gui.Checkbox(_nativeTitlebar, "Native title bar");
            if (gui.Button("◐", 36, 36)) ToggleTheme();
        }

        Toolbar(gui);
        gui.DockSpace(_layout, PanelInfo, RenderPanel, Theme, TabStripActions);
        gui.Toasts();
    }

    /// <summary>Switches between the light and dark themes, leaving follow-system mode.</summary>
    static void ToggleTheme()
    {
        _followSystem = false;
        _radioChoice = _radioChoice == 0 ? 1 : 0;
    }

    static StyleSheet SelectedTheme() => _radioChoice switch
    {
        0 => ExcaliburStyles.Light,
        1 => ExcaliburStyles.Dark,
        2 => ExcaliburStyles.MonoLight,
        3 => ExcaliburStyles.MonoDark,
        _ => ExcaliburStyles.Dark
    };

    /// <summary>Queues a layout edit to run at the start of the next frame's build pass.</summary>
    static void DeferLayout(Action action) => _pendingLayout = action;

    static void Toolbar(Gui gui)
    {
        using (gui.Node(-1, 32).ExpandWidth().Direction(Axis.Horizontal).Padding(0, 6).Gap(6).Enter())
        {
            gui.DrawBackgroundRect(Token(gui, "base-background"));

            if (ToolbarButton(gui, "Save layout")) _saved = _layout.ToJson();
            if (ToolbarButton(gui, "Load layout") && _saved is not null)
                DeferLayout(() => _layout = DockLayout.FromJson(_saved) ?? SeedLayout());
            if (ToolbarButton(gui, "Reset")) DeferLayout(() => _layout = SeedLayout());

            foreach (var panelId in PanelIds.Where(id => !_layout.Contains(id)))
                if (ToolbarButton(gui, $"Reopen {PanelTitles[panelId]}"))
                    DeferLayout(() => _layout.EnsurePanel(panelId));
        }
    }

    static bool ToolbarButton(Gui gui, string label)
    {
        var activated = false;

        using (gui.Node(110, 22).Enter())
        {
            var result = gui.Pressable(new ControlBehaviorOptions(Role: ControlRole.Button, Label: label),
                state =>
                {
                    if (gui.Pass != Pass.Pass2Render) return;
                    gui.DrawBackgroundRect(
                        Token(gui, state.HasFlag(ControlVisualState.Hovered) ? "surface-hover" : "surface"), 3);
                    gui.DrawText(label, 10, Token(gui, "text"));
                });
            activated = result.Activated;
        }

        return activated;
    }

    static DockPanelInfo? PanelInfo(string panelId) =>
        PanelTitles.TryGetValue(panelId, out var title)
            ? new DockPanelInfo(title, Icon: PanelIcons.GetValueOrDefault(panelId))
            : null;

    static void RenderPanel(string panelId, Gui gui)
    {
        if (!ScrollPanels.Contains(panelId))
        {
            Panels[panelId](gui);
            return;
        }

        using (gui.Node().Expand().Direction(Axis.Vertical).Enter())
        {
            gui.ScrollY();
            Panels[panelId](gui);
        }
    }

    /// <summary>
    /// Fills the space a group's tabs leave over: an overflow button that opens the panel's actions.
    /// Children flow from the right, so this is where Unity puts its lock and overflow buttons.
    /// </summary>
    static void TabStripActions(DockTabStrip strip, Gui gui)
    {
        if (strip.ActivePanelId is not { } panelId) return;

        using (gui.Node(18, Theme.TabHeight, $"dock-actions/{panelId}").Enter())
        {
            var result = gui.Pressable(new ControlBehaviorOptions(Role: ControlRole.Button, Label: "Panel actions"),
                state =>
                {
                    if (gui.Pass != Pass.Pass2Render) return;
                    if (state.HasFlag(ControlVisualState.Hovered))
                        gui.DrawBackgroundRect(Token(gui, "surface-hover"), 2);
                    DrawOverflowDots(gui, Token(gui, _menuPanelId == panelId ? "text" : "text-dim"));
                });
            if (result.Activated) _menuPanelId = _menuPanelId == panelId ? null : panelId;
        }

        if (_menuPanelId == panelId) PanelActions(gui, strip, panelId);
    }

    static void PanelActions(Gui gui, DockTabStrip strip, string panelId)
    {
        const float width = 180;
        var free = strip.FreeArea;

        using (gui.Node(width, id: $"dock-actions-menu/{panelId}")
                   .AbsoluteScreen(free.X + free.W - width, free.Y + free.H)
                   .BlockInput().Padding(8).Gap(6).Enter())
        {
            gui.SetZIndex(4000);
            gui.DrawBackgroundRect(Token(gui, "surface"), 6);
            gui.DrawText($"Panel: {PanelTitles.GetValueOrDefault(panelId, panelId)}", 12,
                Token(gui, "text-dim"), centerInRect: false);

            if (ToolbarButton(gui, "Close panel"))
                DeferLayout(() =>
                {
                    _layout.Remove(panelId);
                    _menuPanelId = null;
                });
        }
    }

    /// <summary>A stand-in for a real panel icon — any drawing works here.</summary>
    static void Dot(Gui gui, Color color)
    {
        if (gui.Pass != Pass.Pass2Render) return;

        var rect = gui.CurrentNode.Rect;
        gui.DrawCircleFilled(new Vector2(rect.X + rect.W / 2f, rect.Y + rect.H / 2f), rect.W * 0.32f, color);
    }

    /// <summary>Three stacked dots, drawn rather than typed: most system fonts have no U+22EE.</summary>
    static void DrawOverflowDots(Gui gui, Color color)
    {
        var rect = gui.CurrentNode.Rect;
        var center = new Vector2(rect.X + rect.W / 2f, rect.Y + rect.H / 2f);

        for (var i = -1; i <= 1; i++)
            gui.DrawCircleFilled(center with { Y = center.Y + i * 4f }, 1.4f, color);
    }

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
