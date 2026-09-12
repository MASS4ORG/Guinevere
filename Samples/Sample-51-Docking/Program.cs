using System.Numerics;
using Guinevere;
using Guinevere.OpenGL.SilkNET;
using GuinevereDemos;

namespace Sample_60_Docking;

/// <summary>
/// Exercises the docking system: drag a tab onto another group's edge to split, onto its centre to
/// join its tabs, along a tab strip to reorder, or onto empty space to tear it into a floating
/// window. Drag a splitter to resize, and use the ✕ on a tab to close a panel.
/// </summary>
public abstract class Program
{
    private static readonly string[] PanelIds = ["scene", "game", "hierarchy", "inspector", "console"];

    private static readonly Dictionary<string, string> Titles = new()
    {
        ["scene"] = "Scene",
        ["game"] = "Game",
        ["hierarchy"] = "Hierarchy",
        ["inspector"] = "Inspector",
        ["console"] = "Console"
    };

    private static readonly DockTheme Theme = new();

    // Cached: the resolver runs once per tab per pass, so building these each call would allocate
    // two delegates per tab per frame.
    private static readonly Dictionary<string, Action<Gui>> Icons = new()
    {
        ["scene"] = g => Dot(g, Color.FromArgb(255, 120, 170, 255)),
        ["game"] = g => Dot(g, Color.FromArgb(255, 120, 210, 150)),
        ["hierarchy"] = g => Dot(g, Color.FromArgb(255, 235, 190, 110)),
        ["inspector"] = g => Dot(g, Color.FromArgb(255, 220, 130, 200)),
        ["console"] = g => Dot(g, Color.FromArgb(255, 200, 200, 200))
    };

    private static DockLayout _layout = SeedLayout();
    private static string? _saved;
    private static string? _menuPanelId;

    public static void Main()
    {
        var gui = new Gui();
        using var window = new GuiWindow(gui, 1200, 800, "Guinevere — Docking");
        window.RunGui(() => Draw(gui));
    }

    private static DockLayout SeedLayout()
    {
        var layout = new DockLayout();
        layout.DockAtEdge("scene", DockZone.Center);
        layout.DockAtEdge("game", DockZone.Center);
        layout.DockAtEdge("hierarchy", DockZone.Left, 0.2f);
        layout.DockAtEdge("inspector", DockZone.Right, 0.22f);
        layout.DockAtEdge("console", DockZone.Bottom);
        return layout;
    }

    private static void Draw(Gui gui)
    {
        DemoHeader.Header(gui, "Guinevere Excalibur - Docking");

        Toolbar(gui);
        gui.DockSpace(_layout, PanelInfo, RenderPanel, Theme, TabStripActions);
    }

    private static DockPanelInfo? PanelInfo(string panelId) =>
        Titles.TryGetValue(panelId, out var title)
            ? new DockPanelInfo(title, Icon: Icons.GetValueOrDefault(panelId))
            : null;

    /// <summary>A stand-in for a real panel icon — any drawing works here.</summary>
    private static void Dot(Gui gui, Color color)
    {
        if (gui.Pass != Pass.Pass2Render) return;

        var rect = gui.CurrentNode.Rect;
        gui.DrawCircleFilled(new Vector2(rect.X + rect.W / 2f, rect.Y + rect.H / 2f), rect.W * 0.32f, color);
    }

    /// <summary>
    /// Fills the space a group's tabs leave over. Children flow from the right, so this is where
    /// Unity would put its lock and overflow buttons.
    /// </summary>
    private static void TabStripActions(DockTabStrip strip, Gui gui)
    {
        if (strip.ActivePanelId is not { } panelId) return;

        using (gui.Node(18, Theme.TabHeight).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                var interactable = gui.GetInteractable();
                if (interactable.OnHover()) gui.DrawBackgroundRect(Theme.Hover, 2);
                if (interactable.OnClick()) _menuPanelId = _menuPanelId == panelId ? null : panelId;
            }

            if (gui.Pass == Pass.Pass2Render)
                DrawOverflowDots(gui, _menuPanelId == panelId ? Theme.Ink : Theme.InkDim);
        }
    }

    /// <summary>Three stacked dots, drawn rather than typed: most system fonts have no U+22EE.</summary>
    private static void DrawOverflowDots(Gui gui, Color color)
    {
        var rect = gui.CurrentNode.Rect;
        var centre = new Vector2(rect.X + rect.W / 2f, rect.Y + rect.H / 2f);

        for (var i = -1; i <= 1; i++)
            gui.DrawCircleFilled(centre with { Y = centre.Y + i * 4f }, 1.4f, color);
    }

    private static void Toolbar(Gui gui)
    {
        using (gui.Node(-1, 32).ExpandWidth().Direction(Axis.Horizontal).Padding(0, 6).Gap(6).Enter())
        {
            gui.DrawBackgroundRect(Theme.TabStrip);

            if (ToolbarButton(gui, "Save layout")) _saved = _layout.ToJson();
            if (ToolbarButton(gui, "Load layout") && _saved is not null)
                _layout = DockLayout.FromJson(_saved) ?? SeedLayout();
            if (ToolbarButton(gui, "Reset")) _layout = SeedLayout();

            foreach (var panelId in PanelIds.Where(id => !_layout.Contains(id)))
                if (ToolbarButton(gui, $"Reopen {Titles[panelId]}"))
                    _layout.EnsurePanel(panelId);
        }
    }

    private static bool ToolbarButton(Gui gui, string label)
    {
        var clicked = false;

        using (gui.Node(110, 22).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                var interactable = gui.GetInteractable();
                gui.DrawBackgroundRect(interactable.OnHover() ? Theme.Hover : Theme.Tab, 3);
                clicked = interactable.OnClick();
            }

            gui.DrawText(label, 11, Theme.Ink);
        }

        return clicked;
    }

    private static void RenderPanel(string panelId, Gui gui)
    {
        using (gui.Node().Expand().Padding(10).Gap(6).Enter())
        {
            gui.DrawText(Titles.GetValueOrDefault(panelId, panelId), 15, Theme.Ink, centerInRect: false);

            if (_menuPanelId == panelId)
                gui.DrawText("(the overflow menu for this panel is open)", 11, Theme.Accent, centerInRect: false);

            switch (panelId)
            {
                case "scene":
                case "game":
                    Viewport(gui,
                        panelId == "scene" ? Color.FromArgb(255, 40, 52, 66) : Color.FromArgb(255, 30, 46, 40));
                    break;

                case "hierarchy":
                    foreach (var name in new[] { "Root", "  Camera", "  Light", "  Player", "    Mesh" })
                        gui.DrawText(name, 12, Theme.InkDim, centerInRect: false);
                    break;

                case "inspector":
                    foreach (var field in new[] { "Position", "Rotation", "Scale" })
                        using (gui.Node(-1, 20).ExpandWidth().Direction(Axis.Horizontal).Gap(8).Enter())
                        {
                            gui.DrawText(field, 12, Theme.InkDim, centerInRect: false);
                            gui.DrawText("0.0, 0.0, 0.0", 12, Theme.Ink, centerInRect: false);
                        }

                    break;

                default:
                    using (gui.Node().Expand().Enter())
                    {
                        gui.ScrollY();
                        for (var i = 0; i < 40; i++)
                            gui.DrawText($"[{i:00}] log line", 11, Theme.InkDim, centerInRect: false);
                    }

                    break;
            }
        }
    }

    private static void Viewport(Gui gui, Color color)
    {
        using (gui.Node().Expand().Enter())
        {
            gui.DrawBackgroundRect(color, 3);

            if (gui.Pass != Pass.Pass2Render) return;

            var rect = gui.CurrentNode.Rect;
            var centre = new Vector2(rect.X + rect.W / 2f, rect.Y + rect.H / 2f);
            gui.DrawCircleFilled(centre, MathF.Min(rect.W, rect.H) * 0.2f, Color.FromArgb(90, Theme.Accent));
        }
    }
}
