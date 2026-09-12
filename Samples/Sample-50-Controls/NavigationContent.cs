using System.Numerics;
using Guinevere;

namespace Controls_01;

public abstract partial class Program
{
    private static int _nestedTab;
    private static int _pillTab;
    private static int _verticalTab;
    private static int _activeTab;

    private static readonly TreeViewState FileTreeState = new() { DefaultExpandedDepth = 1 };

    private static readonly TreeViewTheme LightTreeTheme = new()
    {
        RowHeight = 22f,
        Ink = Color.FromArgb(255, 51, 51, 51),
        InkDim = Color.FromArgb(255, 102, 102, 102),
        Hover = Color.FromArgb(255, 238, 242, 250),
        Selected = Color.FromArgb(255, 203, 219, 252)
    };

    private static string _treeStatus = "Click a row — double-click a folder or use the arrow keys to open it";

    private static string _breadcrumbStatus = "You are on Home. Click a crumb to walk back up the trail.";

    private static void NavigationContent(Gui gui)
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(16).Padding(10).Enter())
        {
            Section(gui, "Breadcrumb", () =>
            {
                gui.Breadcrumb(
                [
                    new BreadcrumbItem("Home",
                        () => _breadcrumbStatus = "You are on Home. Click a crumb to walk back up the trail."),
                    new BreadcrumbItem("Documentation",
                        () => _breadcrumbStatus = "Navigated to Documentation — two levels below Home."),
                    new BreadcrumbItem("Controls", IsCurrent: true)
                ], height: 32);

                using (gui.Node().Margin(2, 8, 0, 0).Padding(10, 6).Enter())
                {
                    gui.DrawBackgroundRect(Color.FromArgb(255, 247, 249, 252), 6);
                    gui.DrawText(_breadcrumbStatus, size: 12, color: Color.FromArgb(255, 102, 102, 102),
                        centerInRect: false);
                }
            });

            Section(gui, "Horizontal Tabs", () =>
            {
                gui.Tabs(ref _nestedTab, tabs =>
                {
                    tabs.Tab("Overview", () => gui.DrawText("Overview content", size: 12,
                        color: Color.FromArgb(255, 102, 102, 102)));
                    tabs.Tab("Details", () => gui.DrawText("Details content", size: 12,
                        color: Color.FromArgb(255, 102, 102, 102)));
                    tabs.Tab("History", () => gui.DrawText("History content", size: 12,
                        color: Color.FromArgb(255, 102, 102, 102)));
                });
            });

            Section(gui, "Pill Tabs", () =>
            {
                gui.PillTabs(ref _pillTab, tabs =>
                {
                    tabs.Tab("Overview", () => gui.DrawText("Overview content", size: 12,
                        color: Color.FromArgb(255, 102, 102, 102)));
                    tabs.Tab("Details", () => gui.DrawText("Details content", size: 12,
                        color: Color.FromArgb(255, 102, 102, 102)));
                    tabs.Tab("History", () => gui.DrawText("History content", size: 12,
                        color: Color.FromArgb(255, 102, 102, 102)));
                }, activeTabColor: Color.FromArgb(255, 76, 175, 80));
            });

            Section(gui, "Vertical Tabs", () =>
            {
                using (gui.Node().Height(140).Direction(Axis.Horizontal).Enter())
                {
                    gui.VerticalTabs(ref _verticalTab, tabs =>
                    {
                        tabs.Tab("Overview", () => gui.DrawText("Overview content", size: 12,
                            color: Color.FromArgb(255, 102, 102, 102)));
                        tabs.Tab("Details", () => gui.DrawText("Details content", size: 12,
                            color: Color.FromArgb(255, 102, 102, 102)));
                        tabs.Tab("History", () => gui.DrawText("History content", size: 12,
                            color: Color.FromArgb(255, 102, 102, 102)));
                    });
                }
            });

            Section(gui, "Tree View", () =>
            {
                using (gui.Node().Height(190).Direction(Axis.Horizontal).Gap(12).Enter())
                {
                    using (gui.Node(270).Enter())
                    {
                        gui.DrawBackgroundRect(Color.FromArgb(255, 248, 249, 250), radius: 8);
                        gui.TreeView(FileTreeState, FileTree(), LightTreeTheme, OnTreeClick);
                    }

                    using (gui.Node().Expand().Padding(8).Direction(Axis.Vertical).Gap(10).Enter())
                    {
                        gui.DrawText("Project Explorer", size: 14, color: Color.FromArgb(255, 51, 51, 51));
                        gui.DrawText(_treeStatus, size: 12, color: Color.FromArgb(255, 102, 102, 102),
                            wrapWidth: 420);

                        using (gui.Node().Margin(0, 10, 0, 0).Direction(Axis.Vertical).Gap(4).Enter())
                        {
                            gui.DrawText("Arrow keys navigate, Enter activates", size: 11,
                                color: Color.FromArgb(255, 153, 153, 153));
                            gui.DrawText("Click a folder's arrow, double-click the row", size: 11,
                                color: Color.FromArgb(255, 153, 153, 153));
                            gui.DrawText("Right / middle click are reported too", size: 11,
                                color: Color.FromArgb(255, 153, 153, 153));
                        }
                    }
                }
            });
        }
    }

    private static IReadOnlyList<TreeItem> FileTree()
    {
        return
        [
            new TreeItem("proj", "Guinevere", 0, true, FolderIcon),
            new TreeItem("proj/src", "src", 1, true, FolderIcon),
            new TreeItem("proj/src/core", "Core", 2, true, FolderIcon),
            new TreeItem("proj/src/core/engine", "Engine.cs", 3, false, FileIcon),
            new TreeItem("proj/src/core/renderer", "Renderer.cs", 3, false, FileIcon),
            new TreeItem("proj/src/samples", "Samples", 2, true, FolderIcon),
            new TreeItem("proj/src/samples/progress", "ProgressDemo.cs", 3, false, FileIcon),
            new TreeItem("proj/src/samples/tree", "TreeDemo.cs", 3, false, FileIcon),
            new TreeItem("proj/tests", "Tests", 1, true, FolderIcon),
            new TreeItem("proj/tests/unit", "LayoutTests.cs", 2, false, FileIcon),
            new TreeItem("proj/tests/unit/tree", "TreeViewTests.cs", 2, false, FileIcon),
            new TreeItem("proj/README.md", "README.md", 1, false, FileIcon),
            new TreeItem("assets", "Assets", 0, true, FolderIcon),
            new TreeItem("assets/textures", "Textures", 1, true, FolderIcon),
            new TreeItem("assets/textures/player", "player.png", 2, false, FileIcon),
            new TreeItem("assets/textures/tile", "tile.png", 2, false, FileIcon),
            new TreeItem("assets/audio", "Audio", 1, true, FolderIcon),
            new TreeItem("assets/audio/bgm", "bgm.ogg", 2, false, FileIcon)
        ];
    }

    private static void OnTreeClick(TreeViewEvent evt)
    {
        _treeStatus = evt.Button switch
        {
            MouseButton.Right => $"Right-clicked {evt.Item.Label}",
            MouseButton.Middle => $"Middle-clicked {evt.Item.Label}",
            _ => evt.ClickCount >= 2 ? $"Double-clicked {evt.Item.Label}" : $"Selected {evt.Item.Label}"
        };
    }

    private static void FolderIcon(Gui gui)
    {
        if (gui.Pass != Pass.Pass2Render) return;

        var rect = gui.CurrentNode.Rect;
        var centre = new Vector2(rect.X + rect.W / 2f, rect.Y + rect.H / 2f);
        gui.DrawCircleFilled(centre, MathF.Min(rect.W, rect.H) * 0.32f, Color.FromArgb(255, 235, 179, 63));
    }

    private static void FileIcon(Gui gui)
    {
        if (gui.Pass != Pass.Pass2Render) return;

        var rect = gui.CurrentNode.Rect;
        var centre = new Vector2(rect.X + rect.W / 2f, rect.Y + rect.H / 2f);
        gui.DrawCircleFilled(centre, MathF.Min(rect.W, rect.H) * 0.26f, Color.FromArgb(255, 150, 170, 190));
    }
}
