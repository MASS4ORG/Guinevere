using System.Numerics;
using Guinevere;

namespace Controls_01;

/// <summary>
/// The Excalibur control gallery hung on the docking system: every widget group is a dock panel that
/// can be split, tabbed, reordered, floated or closed. The toolbar saves, restores and resets layouts.
/// </summary>
public abstract partial class Program
{
    static bool _nativeTitlebar;
    static readonly Lazy<Bitmap> Badge = new(DecodeBadge);

    // Layout edits are deferred to the next frame's build pass so both passes of a frame see one tree.
    static Action? _pendingLayout;

    static readonly DockTheme DockTheme = new();
    static readonly string[] CenterPanels = ["navigation", "feedback", "styling", "nine-slice", "dialog", "selection"];
    static DockLayout _layout = SeedLayout();
    static string? _saved;
    static string? _menuPanelId;

    static readonly Dictionary<string, string> PanelTitles = new()
    {
        ["buttons"] = "Buttons",
        ["selection"] = "Selection",
        ["text-inputs"] = "Text Inputs",
        ["navigation"] = "Navigation",
        ["feedback"] = "Feedback",
        ["scrolling"] = "Scrolling",
        ["styling"] = "Styling",
        ["nine-slice"] = "9-slice",
        ["dialog"] = "Dialog"
    };

    static readonly Dictionary<string, Action<Gui>> Panels = new()
    {
        ["buttons"] = ButtonsContent,
        ["selection"] = SelectionContent,
        ["text-inputs"] = TextInputsContent,
        ["navigation"] = NavigationContent,
        ["feedback"] = FeedbackContent,
        ["scrolling"] = ScrollingContent,
        ["styling"] = StylingContent,
        ["nine-slice"] = NineSliceContent,
        ["dialog"] = DialogContent
    };

    // Cached: the tab resolver runs once per tab per pass, so build the icon delegates once.
    static readonly Dictionary<string, Action<Gui>> PanelIcons = new()
    {
        ["buttons"] = g => TabGlyph(g, TabGlyphKind.Button, Color.FromArgb(255, 120, 170, 255)),
        ["selection"] = g => TabGlyph(g, TabGlyphKind.Check, Color.FromArgb(255, 120, 210, 150)),
        ["text-inputs"] = g => TabGlyph(g, TabGlyphKind.Cursor, Color.FromArgb(255, 235, 190, 110)),
        ["navigation"] = g => TabGlyph(g, TabGlyphKind.Menu, Color.FromArgb(255, 220, 130, 200)),
        ["feedback"] = g => TabGlyph(g, TabGlyphKind.Toast, Color.FromArgb(255, 100, 200, 180)),
        ["scrolling"] = g => TabGlyph(g, TabGlyphKind.Scroll, Color.FromArgb(255, 180, 160, 255)),
        ["styling"] = g => TabGlyph(g, TabGlyphKind.Swatch, Color.FromArgb(255, 255, 160, 112)),
        ["dialog"] = g => TabGlyph(g, TabGlyphKind.Button, Color.FromArgb(255, 255, 160, 112))
    };

    /// <summary>Adds the gallery's styling sheet; hosts call this before the first frame.</summary>
    public static void ApplyDefaultSheet(Gui gui) => gui.StyleSheets.Add(StylingSheet);

    static DockLayout SeedLayout()
    {
        var layout = new DockLayout();
        foreach (var panelId in CenterPanels)
            layout.DockAtEdge(panelId, DockZone.Center);
        layout.DockAtEdge("text-inputs", DockZone.Right, 0.24f);
        layout.DockAtEdge("scrolling", DockZone.Bottom, 0.3f);
        layout.DockAtEdge("buttons", DockZone.Left, 0.2f);
        return layout;
    }

    /// <summary>Returns the shared badge decoded once from the gallery's embedded resource.</summary>
    internal static Bitmap LoadBadge() => Badge.Value;

    static Bitmap DecodeBadge()
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("Controls_01.guinevere-badge.png")
                           ?? throw new InvalidOperationException("The embedded gallery badge is missing.");
        return Bitmap.FromStream(stream);
    }

    /// <summary>Draws the control gallery into the host's Gui.</summary>
    public static void Draw(Gui gui)
    {
        if (gui.Pass == Pass.Pass1Build && _pendingLayout is { } pending)
        {
            _pendingLayout = null;
            pending();
        }

        ExcaliburStyles.SetTheme(gui, SelectedTheme());

        using (gui.AppBar(windowControls: !_nativeTitlebar, nativeTitlebar: _nativeTitlebar, resizable: true))
        {
            gui.MenuBar(menu => menu.Collapsible()
                .Menu("File", file => file
                    .Item("Close", () => gui.Platform.Require<IWindowChromeCapability>().RequestClose())
                )
                .Menu("Layout", layout => layout
                    .Item("Save", () => _saved = _layout.ToJson())
                    .Item("Load", () => DeferLayout(() =>
                    {
                        if (_saved is not null) _layout = DockLayout.FromJson(_saved) ?? SeedLayout();
                    }))
                    .Item("Reset", () => DeferLayout(() => _layout = SeedLayout()))
                )
            );

            gui.Image(LoadBadge(), width: 26, height: 26);
            gui.DrawText("Guinevere Excalibur", 16, Token(gui, "text"));
            gui.Node().ExpandWidth();
            gui.DrawText($"FPS: {gui.Time.SmoothFps:N1}", 12, Token(gui, "text"));
            _nativeTitlebar = gui.Checkbox(_nativeTitlebar, "Native title bar");
            if (gui.Button("◐", width: 62, height: 28, fontSize: 12)) ToggleTheme();
        }

        gui.DockSpace(_layout, PanelInfo, RenderPanel, DockTheme, TabStripActions);
        gui.Toasts();
    }

    /// <summary>Switches between the light and dark themes, leaving follow-system mode.</summary>
    static void ToggleTheme()
    {
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

    static bool ToolbarButton(Gui gui, string label)
    {
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
            return result.Activated;
        }
    }

    static DockPanelInfo? PanelInfo(string panelId) =>
        PanelTitles.TryGetValue(panelId, out var title)
            ? new DockPanelInfo(title, Icon: PanelIcons.GetValueOrDefault(panelId))
            : null;

    static void RenderPanel(string panelId, Gui gui)
    {
        if (!CenterPanels.Contains(panelId))
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

        using (gui.Node(18, DockTheme.TabHeight, $"dock-actions/{panelId}").Enter())
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
    static void TabGlyph(Gui gui, TabGlyphKind kind, Color color)
    {
        if (gui.Pass != Pass.Pass2Render) return;

        var rect = gui.CurrentNode.Rect;
        var center = new Vector2(rect.X + rect.W / 2, rect.Y + rect.H / 2);
        var size = MathF.Min(rect.W, rect.H) * 0.40f;
        switch (kind)
        {
            case TabGlyphKind.Button:
                gui.DrawRectBorder(new Rect(center.X - size, center.Y - size, size * 2f, size * 2f), color, 1.5f);
                break;
            case TabGlyphKind.Check:
                gui.DrawLine(new Vector2(center.X - size, center.Y - size * 0.1f),
                    new Vector2(center.X - 0.1f * size, center.Y + size), color);
                gui.DrawLine(new Vector2(center.X + size, center.Y - size),
                    new Vector2(center.X - size, center.Y + 0.9f * size), color);
                break;
            case TabGlyphKind.Cursor:
                gui.DrawLine(new Vector2(center.X, center.Y - size), new Vector2(center.X, center.Y + size), color);
                gui.DrawLine(new Vector2(center.X - size, center.Y + size),
                    new Vector2(center.X + size, center.Y + size), color);
                break;
            case TabGlyphKind.Menu:
                for (var i = -1; i <= 1; i++)
                    gui.DrawLine(new Vector2(center.X - size, center.Y + i * size),
                        new Vector2(center.X + size, center.Y + i * size), color);
                break;
            case TabGlyphKind.Toast:
                gui.DrawCircleBorder(center, size, color, 1.5f);
                gui.DrawCircleFilled(new Vector2(center.X + 0.6f * size, center.Y - 0.6f * size), 0.3f * size, color);
                break;
            case TabGlyphKind.Scroll:
                gui.DrawLine(new Vector2(center.X, center.Y - size), new Vector2(center.X, center.Y + 0.4f * size),
                    color);
                gui.DrawLine(new Vector2(center.X - size, center.Y - 0.4f * size),
                    new Vector2(center.X, center.Y + 0.4f * size), color);
                gui.DrawLine(new Vector2(center.X + size, center.Y - 0.4f * size),
                    new Vector2(center.X, center.Y + 0.4f * size), color);
                break;
            case TabGlyphKind.Swatch:
                gui.DrawCircleFilled(center, 0.45f * size, color);
                gui.DrawCircleBorder(center, size, color, 1.5f);
                break;
        }
    }

    /// <summary>The tab glyph shapes; keep them font-free so no glyph face is required on the tab bar.</summary>
    enum TabGlyphKind
    {
        Button,
        Check,
        Cursor,
        Menu,
        Toast,
        Scroll,
        Swatch
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
            gui.DrawBackgroundRect(Token(gui, "md-surface-container-low"), 12);
            gui.DrawText(title, size: 18, color: Token(gui, "text")).MarginBottom(5);
            body();
        }
    }
}
