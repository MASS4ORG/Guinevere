using System.Numerics;
using Guinevere;

namespace Controls_01;

public abstract partial class Program
{
    static int _nestedTab;
    static int _pillTab;
    static int _verticalTab;
    static int _activeTab;

    static readonly TreeViewState FileTreeState = new() { DefaultExpandedDepth = 1 };

    static string _treeStatus = "Click a row — double-click a folder or use the arrow keys to open it";

    static string _treeCrumbStatus = "Select a row; the trail then shows its path. Click a crumb to jump there.";

    static readonly string[] NestedTabTitles = ["Overview", "Details", "History"];

    static string _menuStatus = "Choose a menu — hover a title to switch, submenus cascade on hover";

    static bool _showStatusBar = true;
    static bool _showToolbar = true;
    static bool _focusScopeOpen;

    const string HorizontalTabsId = "navigation-horizontal-tabs";

    const string FolderGlyph = "📁";
    const string FileGlyph = "📄";

    static void NavigationContent(Gui gui)
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(16).Padding(10).Enter())
        {
            gui.SetTextColor(Color.Green);
            gui.ScrollY();

            Section(gui, "Horizontal Tabs", () => HorizontalTabsContent(gui));

            Section(gui, "Pill Tabs", () => gui.PillTabs(ref _pillTab, tabs =>
                {
                    tabs.Tab("Overview", () => gui.DrawText("Overview content", size: 12,
                        color: Color.FromArgb(255, 102, 102, 102)));
                    tabs.Tab("Details", () => gui.DrawText("Details content", size: 12,
                        color: Color.FromArgb(255, 102, 102, 102)));
                    tabs.Tab("History", () => gui.DrawText("History content", size: 12,
                        color: Color.FromArgb(255, 102, 102, 102)));
                }, activeTabColor: Color.FromArgb(255, 76, 175, 80)));

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

            Section(gui, "Menu Bar", () => MenuBarContent(gui));

            Section(gui, "Tree View & Breadcrumb", () => TreeViewBreadcrumb(gui));
        }
    }

    static void FocusNavigationContent(Gui gui)
    {
        Section(gui, "Focus navigation", () =>
        {
            gui.DrawText("Tab focuses controls in build order. Arrow keys choose the nearest control in that direction.",
                size: 12, color: Color.FromArgb(255, 102, 102, 102));

            if (gui.Button("Open focus scope", width: 150)) _focusScopeOpen = true;

            if (!_focusScopeOpen) return;

            using var scope = gui.EnterFocusNavigationScope("example-focus-scope");
            scope.SetActive();

            gui.DrawText("This temporary scope traps Tab until it closes, then restores focus to its opener.",
                size: 12, color: Color.FromArgb(255, 51, 51, 51));
            using (gui.Node().Direction(Axis.Horizontal).Gap(8).Enter())
            {
                gui.Button("First", width: 90);
                gui.Button("Second", width: 90);
                if (gui.Button("Close scope", width: 110)) _focusScopeOpen = false;
            }
        });
    }

    static void MenuBarContent(Gui gui)
    {
        gui.MenuBar(menu =>
        {
            menu.Menu("File", file =>
            {
                file.Item("New", () => _menuStatus = "File > New", "Ctrl+N");
                file.Item("Open…", () => _menuStatus = "File > Open", "Ctrl+O");
                file.Separator();
                file.Item("Save", () => _menuStatus = "File > Save", "Ctrl+S");
                file.Item("Save As…", () => _menuStatus = "File > Save As", "Ctrl+Shift+S");
                file.Separator();
                file.Item("Exit", () => _menuStatus = "File > Exit");
            });

            menu.Menu("Edit", edit =>
            {
                edit.Item("Undo", () => _menuStatus = "Edit > Undo", "Ctrl+Z");
                edit.Item("Redo", () => _menuStatus = "Edit > Redo", "Ctrl+Y");
                edit.Separator();
                edit.Item("Cut", () => _menuStatus = "Edit > Cut", "Ctrl+X");
                edit.Item("Copy", () => _menuStatus = "Edit > Copy", "Ctrl+C");
                edit.Item("Paste", () => _menuStatus = "Edit > Paste", "Ctrl+V");
            });

            menu.Menu("View", view =>
            {
                view.CheckItem("Toolbar", () => _showToolbar, value => _showToolbar = value);
                view.CheckItem("Status Bar", () => _showStatusBar, value => _showStatusBar = value);
                view.Separator();
                view.Submenu("Zoom", zoom =>
                {
                    zoom.Item("Zoom In", () => _menuStatus = "View > Zoom In", "Ctrl+=");
                    zoom.Item("Zoom Out", () => _menuStatus = "View > Zoom Out", "Ctrl+-");
                    zoom.Item("Reset Zoom", () => _menuStatus = "View > Reset Zoom", "Ctrl+0");
                });
                view.Separator();
                view.Item("Full Screen", () => _menuStatus = "View > Full Screen", "F11", enabled: false);
            });

            menu.Menu("Help", help =>
            {
                help.Item("Documentation", () => _menuStatus = "Help > Documentation", "F1");
                help.Item("Check for Updates…", () => _menuStatus = "Help > Updates");
                help.Separator();
                help.Item("About", () => _menuStatus = "Help > About");
            });
        });

        gui.DrawText(_menuStatus, size: 12, color: Color.FromArgb(255, 102, 102, 102), wrapWidth: 560);
    }

    static void TreeViewBreadcrumb(Gui gui)
    {
        using (gui.Node().Height(250).Enter())
        {
            gui.SetTextColor(Color.Blue);
            gui.Breadcrumb(FileTrail(), height: 32);

            using (gui.Node().Margin(0, 10, 0, 0).Height(190).Direction(Axis.Horizontal).Gap(12).Enter())
            {
                using (gui.Node(270).Enter())
                {
                    gui.TreeView(FileTreeState, FileTree(), onClick: OnTreeClick);
                }

                using (gui.Node().Expand().Padding(8).Direction(Axis.Vertical).Gap(10).Enter())
                {
                    gui.DrawText("Project Explorer", size: 14, color: Color.FromArgb(255, 51, 51, 51));
                    gui.DrawText(_treeStatus, size: 12, color: Color.FromArgb(255, 102, 102, 102),
                        wrapWidth: 420);
                    gui.DrawText(_treeCrumbStatus, size: 12, color: Color.FromArgb(255, 102, 102, 102),
                        wrapWidth: 420);

                    using (gui.Node().Margin(0, 10, 0, 0).Direction(Axis.Vertical).Gap(4).Enter())
                    {
                        gui.DrawText("Arrow keys navigate, Enter activates", size: 11,
                            color: Color.FromArgb(255, 153, 153, 153));
                        gui.DrawText("Click a folder's arrow, double-click the row", size: 11,
                            color: Color.FromArgb(255, 153, 153, 153));
                        gui.DrawText("The breadcrumb above follows the selection", size: 11,
                            color: Color.FromArgb(255, 153, 153, 153));
                        gui.DrawText("Right / middle click are reported too", size: 11,
                            color: Color.FromArgb(255, 153, 153, 153));
                    }
                }
            }
        }
    }

    static IReadOnlyList<TreeItem> FileTree()
    {
        return
        [
            new TreeItem("proj", "Guinevere", 0, true, FolderIcon),
            new TreeItem("proj/src", "src", 1, true, FolderIcon),
            new TreeItem("proj/src/core", "Core", 2, true, FolderIcon),
            new TreeItem("proj/src/core/engine", "Engine.cs", 3, false, FileIcon),
            new TreeItem("proj/src/core/renderer", "Renderer.cs", 3, false, FileIcon),
            new TreeItem("proj/src/examples", "Examples", 2, true, FolderIcon),
            new TreeItem("proj/src/examples/progress", "ProgressDemo.cs", 3, false, FileIcon),
            new TreeItem("proj/src/examples/tree", "TreeDemo.cs", 3, false, FileIcon),
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

    static void HorizontalTabsContent(Gui gui)
    {
        gui.Tabs(ref _nestedTab, tabs =>
        {
            foreach (var title in NestedTabTitles)
                tabs.Tab(title, () => gui.DrawText($"{title} content", size: 12,
                    color: Color.FromArgb(255, 102, 102, 102)), closable: true);
        }, id: HorizontalTabsId);

        using (gui.Node().Margin(0, 6, 0, 0).Direction(Axis.Horizontal).Gap(8).Enter())
        {
            gui.DrawText("Middle-click a tab to close it", size: 11,
                color: Color.FromArgb(255, 153, 153, 153));

            if (gui.Button("Restore", width: 72, height: 24, fontSize: 12))
            {
                gui.RestoreTabs(HorizontalTabsId);
                _nestedTab = 0;
            }
        }
    }

    static IReadOnlyList<BreadcrumbItem> FileTrail()
    {
        var selectedId = FileTreeState.SelectedId;
        if (string.IsNullOrEmpty(selectedId))
        {
            return
            [
                new BreadcrumbItem("Guinevere", () => NavigateTo("proj"), Icon: FolderGlyph,
                    Children: ChildrenOf("proj")),
                new BreadcrumbItem("Nothing selected", IsCurrent: true)
            ];
        }

        var segments = selectedId.Split('/');
        var crumbs = new List<BreadcrumbItem>();
        for (var i = 0; i < segments.Length; i++)
        {
            var path = string.Join('/', segments.Take(i + 1));
            var label = i == 0 ? "Guinevere" : segments[i];
            var isLeaf = i == segments.Length - 1;

            crumbs.Add(new BreadcrumbItem(label,
                isLeaf ? null : () => NavigateTo(path),
                IsCurrent: isLeaf,
                Icon: IsFolder(path) ? FolderGlyph : FileGlyph,
                Children: ChildrenOf(path)));
        }
        return crumbs;
    }

    /// <summary>Direct children of <paramref name="path"/>, as crumbs that jump to the child's folder.</summary>
    static IReadOnlyList<BreadcrumbItem> ChildrenOf(string path)
    {
        var childDepth = path.Split('/').Length;
        var prefix = $"{path}/";

        return FileTree()
            .Where(row => row.Depth == childDepth && row.Id.StartsWith(prefix))
            .Select(row => new BreadcrumbItem(row.Label, () => NavigateTo(row.Id),
                Icon: row.HasChildren ? FolderGlyph : FileGlyph))
            .ToList();
    }

    static bool IsFolder(string path)
    {
        var depth = path.Split('/').Length - 1;
        return FileTree().Any(row => row.Depth == depth && row.Id == path && row.HasChildren);
    }

    static void NavigateTo(string path)
    {
        var segments = path.Split('/');
        var prefix = "";
        foreach (var segment in segments)
        {
            prefix = prefix.Length == 0 ? segment : $"{prefix}/{segment}";
            FileTreeState.SetExpanded(prefix, true);
        }

        FileTreeState.SelectedId = path;
        _treeCrumbStatus = $"Jumped to {segments[^1]} — the tree follows the trail.";
    }

    static void OnTreeClick(TreeViewEvent evt)
    {
        _treeStatus = evt.Button switch
        {
            MouseButton.Right => $"Right-clicked {evt.Item.Label}",
            MouseButton.Middle => $"Middle-clicked {evt.Item.Label}",
            _ => evt.ClickCount >= 2 ? $"Double-clicked {evt.Item.Label}" : $"Selected {evt.Item.Label}"
        };

        if (evt.Button == MouseButton.Left && !string.IsNullOrEmpty(evt.Item.Id))
            _treeCrumbStatus = $"The trail now shows the path to {evt.Item.Label}.";
    }

    static void FolderIcon(Gui gui)
    {
        if (gui.Pass != Pass.Pass2Render) return;

        var rect = gui.CurrentNode.Rect;
        var center = new Vector2(rect.X + rect.W / 2f, rect.Y + rect.H / 2f);
        gui.DrawCircleFilled(center, MathF.Min(rect.W, rect.H) * 0.32f, Color.FromArgb(255, 235, 179, 63));
    }

    static void FileIcon(Gui gui)
    {
        if (gui.Pass != Pass.Pass2Render) return;

        var rect = gui.CurrentNode.Rect;
        var center = new Vector2(rect.X + rect.W / 2f, rect.Y + rect.H / 2f);
        gui.DrawCircleFilled(center, MathF.Min(rect.W, rect.H) * 0.26f, Color.FromArgb(255, 150, 170, 190));
    }
}
