using System.Numerics;
using Guinevere;
using Guinevere.OpenGL.SilkNET;

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

    private static DockLayout _layout = SeedLayout();
    private static string? _saved;

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
        layout.DockAtEdge("console", DockZone.Bottom, 0.25f);
        return layout;
    }

    private static void Draw(Gui gui)
    {
        gui.DrawRect(gui.ScreenRect, Color.FromArgb(255, 18, 20, 25));

        using (gui.Node().Expand().Direction(Axis.Vertical).Enter())
        {
            Toolbar(gui);

            using (gui.Node().Expand().Enter())
            {
                gui.DockSpace(_layout, PanelInfo, RenderPanel, Theme);
            }
        }
    }

    private static DockPanelInfo? PanelInfo(string panelId) =>
        Titles.TryGetValue(panelId, out var title) ? new DockPanelInfo(title) : null;

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
                    _layout.EnsurePanel(panelId, DockZone.Center);
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

            switch (panelId)
            {
                case "scene":
                case "game":
                    Viewport(gui, panelId == "scene" ? Color.FromArgb(255, 40, 52, 66) : Color.FromArgb(255, 30, 46, 40));
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
