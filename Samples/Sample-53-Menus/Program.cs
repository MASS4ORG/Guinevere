using System.Numerics;
using Guinevere;
using Guinevere.OpenGL.SilkNET;

namespace Sample_53_Menus;

public abstract class Program
{
    private static string _statusMessage = "Ready";
    private static DateTime _lastActionTime = DateTime.Now;
    private static bool _showGrid = true;
    private static bool _showRuler;
    private static bool _darkMode;
    private static float _zoom = 1.0f;
    private static readonly string CurrentDocument = "Untitled Document";
    private static bool _documentModified;

    private static readonly List<string> RecentFiles =
        ["Document1.txt", "Project.guinevere", "Layout.xml", "Config.json"];

    private static bool _contextMenuOpen;

    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui, 1200, 800, "Flyout Menu Demo");

        win.RunGui(() => Draw(gui));
    }

    private static void Draw(Gui gui)
    {
        var backgroundColor = _darkMode ? Color.FromArgb(255, 45, 45, 45) : Color.FromArgb(255, 248, 249, 250);
        gui.DrawRect(gui.ScreenRect, backgroundColor);

        using (gui.Node().Expand().Direction(Axis.Vertical).Enter())
        {
            // Menu Bar
            RenderMenuBar(gui);

            // Main Content Area
            using (gui.Node().Expand().Direction(Axis.Horizontal).Enter())
            {
                // Sidebar
                RenderSidebar(gui);

                // Content Area
                RenderContentArea(gui);
            }

            // Status Bar
            RenderStatusBar(gui);
        }

        // Handle flyout rendering (must be after main UI)
        HandleContextMenus(gui);
    }

    private static void RenderMenuBar(Gui gui)
    {
        var textColor = _darkMode ? Color.White : Color.Black;
        var bgColor = _darkMode ? Color.FromArgb(255, 60, 60, 60) : Color.FromArgb(255, 248, 249, 250);
        var hoverColor = _darkMode ? Color.FromArgb(255, 80, 80, 80) : Color.FromArgb(255, 230, 230, 230);

        gui.MenuBar(menuBar =>
            {
                menuBar.Menu("File", fileMenu =>
                {
                    fileMenu.Item("New", () => ExecuteAction("Created new document"), "Ctrl+N");
                    fileMenu.Item("Open...", () => ExecuteAction("Opened file dialog"), "Ctrl+O");

                    fileMenu.Submenu("Open Recent", recentMenu =>
                    {
                        foreach (var file in RecentFiles)
                        {
                            recentMenu.Item(file, () => ExecuteAction($"Opened {file}"));
                        }

                        recentMenu.Separator();
                        recentMenu.Item("Clear Recent Files", () => ExecuteAction("Cleared recent files"));
                    });

                    fileMenu.Separator();
                    fileMenu.Item("Save", () => ExecuteAction("Saved document"), "Ctrl+S", _documentModified);
                    fileMenu.Item("Save As...", () => ExecuteAction("Opened save dialog"), "Ctrl+Shift+S");

                    fileMenu.Submenu("Export", exportMenu =>
                    {
                        exportMenu.Item("Export as PDF", () => ExecuteAction("Exported as PDF"));
                        exportMenu.Item("Export as Image", () => ExecuteAction("Exported as Image"));
                        exportMenu.Item("Export as HTML", () => ExecuteAction("Exported as HTML"));
                    });

                    fileMenu.Separator();
                    fileMenu.Item("Print...", () => ExecuteAction("Opened print dialog"), "Ctrl+P");
                    fileMenu.Item("Print Preview", () => ExecuteAction("Opened print preview"));
                    fileMenu.Separator();
                    fileMenu.Item("Exit", () => ExecuteAction("Application exit requested"), "Alt+F4");
                });

                menuBar.Menu("Edit", editMenu =>
                {
                    editMenu.Item("Undo", () => ExecuteAction("Undo performed"), "Ctrl+Z");
                    editMenu.Item("Redo", () => ExecuteAction("Redo performed"), "Ctrl+Y");
                    editMenu.Separator();
                    editMenu.Item("Cut", () => ExecuteAction("Cut to clipboard"), "Ctrl+X");
                    editMenu.Item("Copy", () => ExecuteAction("Copied to clipboard"), "Ctrl+C");
                    editMenu.Item("Paste", () => ExecuteAction("Pasted from clipboard"), "Ctrl+V");
                    editMenu.Separator();
                    editMenu.Item("Select All", () => ExecuteAction("Selected all"), "Ctrl+A");
                    editMenu.Item("Find...", () => ExecuteAction("Opened find dialog"), "Ctrl+F");
                    editMenu.Item("Replace...", () => ExecuteAction("Opened replace dialog"), "Ctrl+H");
                });

                menuBar.Menu("View", viewMenu =>
                {
                    viewMenu.Item($"{(_showGrid ? "Hide" : "Show")} Grid", () =>
                    {
                        _showGrid = !_showGrid;
                        ExecuteAction($"Grid {(_showGrid ? "shown" : "hidden")}");
                    });

                    viewMenu.Item($"{(_showRuler ? "Hide" : "Show")} Ruler", () =>
                    {
                        _showRuler = !_showRuler;
                        ExecuteAction($"Ruler {(_showRuler ? "shown" : "hidden")}");
                    });

                    viewMenu.Separator();

                    viewMenu.Submenu("Zoom", zoomMenu =>
                    {
                        zoomMenu.Item("Zoom In", () =>
                        {
                            _zoom = Math.Min(_zoom * 1.2f, 5.0f);
                            ExecuteAction($"Zoomed to {_zoom:P0}");
                        }, "Ctrl++");

                        zoomMenu.Item("Zoom Out", () =>
                        {
                            _zoom = Math.Max(_zoom / 1.2f, 0.1f);
                            ExecuteAction($"Zoomed to {_zoom:P0}");
                        }, "Ctrl+-");

                        zoomMenu.Item("Reset Zoom", () =>
                        {
                            _zoom = 1.0f;
                            ExecuteAction("Zoom reset to 100%");
                        }, "Ctrl+0");

                        zoomMenu.Separator();
                        zoomMenu.Item("Fit to Window", () => ExecuteAction("Fit to window"));
                        zoomMenu.Item("Fit Width", () => ExecuteAction("Fit width"));
                    });

                    viewMenu.Submenu("Theme", themeMenu =>
                    {
                        themeMenu.Item("Light Mode", () =>
                        {
                            _darkMode = false;
                            ExecuteAction("Switched to light mode");
                        });

                        themeMenu.Item("Dark Mode", () =>
                        {
                            _darkMode = true;
                            ExecuteAction("Switched to dark mode");
                        });
                    });

                    viewMenu.Separator();
                    viewMenu.Item("Full Screen", () => ExecuteAction("Toggled full screen"), "F11");
                });

                menuBar.Menu("Tools", toolsMenu =>
                {
                    toolsMenu.Item("Preferences...", () => ExecuteAction("Opened preferences"));
                    toolsMenu.Item("Customize Toolbar...", () => ExecuteAction("Opened toolbar customization"));
                    toolsMenu.Separator();

                    toolsMenu.Submenu("Language", langMenu =>
                    {
                        langMenu.Item("English", () => ExecuteAction("Language: English"));
                        langMenu.Item("Spanish", () => ExecuteAction("Language: Spanish"));
                        langMenu.Item("French", () => ExecuteAction("Language: French"));
                        langMenu.Item("German", () => ExecuteAction("Language: German"));
                    });

                    toolsMenu.Separator();
                    toolsMenu.Item("Run Diagnostic", () => ExecuteAction("Diagnostic completed"));
                    toolsMenu.Item("Check for Updates", () => ExecuteAction("Checked for updates"));
                });

                menuBar.Menu("Help", helpMenu =>
                {
                    helpMenu.Item("User Guide", () => ExecuteAction("Opened user guide"), "F1");
                    helpMenu.Item("Keyboard Shortcuts", () => ExecuteAction("Showed shortcuts"));
                    helpMenu.Item("Video Tutorials", () => ExecuteAction("Opened tutorials"));
                    helpMenu.Separator();
                    helpMenu.Item("Report Bug", () => ExecuteAction("Opened bug report"));
                    helpMenu.Item("Feature Request", () => ExecuteAction("Opened feature request"));
                    helpMenu.Separator();
                    helpMenu.Item("About", () => ExecuteAction("Showed about dialog"));
                });
            },
            backgroundColor: bgColor,
            textColor: textColor,
            hoverColor: hoverColor);
    }

    private static void RenderSidebar(Gui gui)
    {
        var bgColor = _darkMode ? Color.FromArgb(255, 55, 55, 55) : Color.White;
        var textColor = _darkMode ? Color.White : Color.Black;

        using (gui.Node().Width(200).Enter())
        {
            gui.DrawBackgroundRect(bgColor);
            gui.DrawRectBorder(gui.CurrentNode.Rect, Color.FromArgb(255, 200, 200, 200));

            using (gui.Node().Margin(15).Direction(Axis.Vertical).Gap(15).Enter())
            {
                gui.DrawText("Project Explorer", size: 14, color: textColor);

                var items = new[] { "📁 Documents", "📁 Images", "📁 Templates", "📄 " + CurrentDocument };

                foreach (var item in items)
                {
                    using (gui.Node().Height(25).Enter())
                    {
                        var interactable = gui.GetInteractable();

                        var isHovered = interactable.OnHover();

                        if (isHovered)
                        {
                            var hoverColor = _darkMode
                                ? Color.FromArgb(255, 75, 75, 75)
                                : Color.FromArgb(255, 240, 240, 240);
                            gui.DrawBackgroundRect(hoverColor, 4);
                        }

                        using (gui.Node().Padding(8, 4).Enter())
                        {
                            gui.DrawText(item, size: 12, color: textColor, centerInRect: false);
                        }
                    }
                }

                gui.DrawText("Properties", size: 14, color: textColor);

                using (gui.Node().Direction(Axis.Vertical).Gap(8).Enter())
                {
                    gui.DrawText($"Zoom: {_zoom:P0}", size: 11, color: textColor);
                    gui.DrawText($"Grid: {(_showGrid ? "On" : "Off")}", size: 11, color: textColor);
                    gui.DrawText($"Ruler: {(_showRuler ? "On" : "Off")}", size: 11, color: textColor);
                    gui.DrawText($"Theme: {(_darkMode ? "Dark" : "Light")}", size: 11, color: textColor);
                }
            }
        }
    }

    private static void RenderContentArea(Gui gui)
    {
        var bgColor = _darkMode ? Color.FromArgb(255, 40, 40, 40) : Color.White;
        var textColor = _darkMode ? Color.FromArgb(255, 220, 220, 220) : Color.FromArgb(255, 60, 60, 60);

        using (gui.Node().Expand().Enter())
        {
            gui.DrawBackgroundRect(bgColor);

            using (gui.Node().Margin(20).Direction(Axis.Vertical).Gap(20).Enter())
            {
                // Document title
                var titleText = CurrentDocument + (_documentModified ? " *" : "");
                gui.DrawText(titleText, size: 18, color: textColor);

                // Content description
                gui.DrawText("This is the main content area where your document would be edited.",
                    size: 14, color: textColor, wrapWidth: 600);

                gui.DrawText("Right-click anywhere in this area to see a context menu with additional options.",
                    size: 12, color: Color.Gray, wrapWidth: 600);

                // Sample grid if enabled
                if (_showGrid)
                {
                    DrawGrid(gui);
                }

                // Sample ruler if enabled
                if (_showRuler)
                {
                    DrawRuler(gui);
                }

                // Zoom indicator
                using (gui.Node().Height(40).Direction(Axis.Horizontal).ContentAlignY(.5f).Gap(10).Enter())
                {
                    gui.DrawText($"Current Zoom: {_zoom:P0}", size: 12, color: textColor);

                    if (gui.Button("-", width: 30, height: 25))
                    {
                        _zoom = Math.Max(_zoom / 1.2f, 0.1f);
                        ExecuteAction($"Zoomed to {_zoom:P0}");
                    }

                    if (gui.Button("+", width: 30, height: 25))
                    {
                        _zoom = Math.Min(_zoom * 1.2f, 5.0f);
                        ExecuteAction($"Zoomed to {_zoom:P0}");
                    }
                }
            }
        }
    }

    private static void RenderStatusBar(Gui gui)
    {
        var bgColor = _darkMode ? Color.FromArgb(255, 50, 50, 50) : Color.FromArgb(255, 240, 240, 240);
        var textColor = _darkMode ? Color.White : Color.Black;

        using (gui.Node().Height(25).Direction(Axis.Horizontal).ContentAlignY(.5f).Enter())
        {
            // if (gui.Pass == Pass.Pass2Render)
            {
                gui.DrawBackgroundRect(bgColor);
                gui.DrawRectBorder(gui.CurrentNode.Rect, Color.FromArgb(255, 200, 200, 200));
            }

            using (gui.Node().Margin(10, 0).Direction(Axis.Horizontal).ContentAlignY(.5f).Gap(20).Enter())
            {
                gui.DrawText(_statusMessage, size: 11, color: textColor, centerInRect: false);

                using (gui.Node().Width(0).Expand().Enter())
                {
                } // Spacer

                gui.DrawText($"Last Action: {_lastActionTime:HH:mm:ss}", size: 11, color: textColor,
                    centerInRect: false);
            }
        }
    }

    private static void HandleContextMenus(Gui gui)
    {
        if (gui.Input.IsMouseButtonPressed(MouseButton.Right))
        {
            _contextMenuOpen = true;
        }

        if (_contextMenuOpen)
        {
            var mousePos = gui.Input.MousePosition;

            gui.Flyout(ref _contextMenuOpen, mousePos, contextMenu =>
            {
                contextMenu.Item("Cut", () => ExecuteAction("Cut from context menu"), "Ctrl+X");
                contextMenu.Item("Copy", () => ExecuteAction("Copied from context menu"), "Ctrl+C");
                contextMenu.Item("Paste", () => ExecuteAction("Pasted from context menu"), "Ctrl+V");
                contextMenu.Separator();

                contextMenu.Submenu("Insert", insertMenu =>
                {
                    insertMenu.Item("Text Box", () => ExecuteAction("Inserted text box"));
                    insertMenu.Item("Image", () => ExecuteAction("Inserted image"));
                    insertMenu.Item("Table", () => ExecuteAction("Inserted table"));
                    insertMenu.Separator();
                    insertMenu.Item("Shape", () => ExecuteAction("Inserted shape"));
                    insertMenu.Item("Chart", () => ExecuteAction("Inserted chart"));
                });

                contextMenu.Submenu("Format", formatMenu =>
                {
                    formatMenu.Item("Bold", () => ExecuteAction("Applied bold formatting"));
                    formatMenu.Item("Italic", () => ExecuteAction("Applied italic formatting"));
                    formatMenu.Item("Underline", () => ExecuteAction("Applied underline formatting"));
                    formatMenu.Separator();

                    formatMenu.Submenu("Text Color", colorMenu =>
                    {
                        colorMenu.Item("Black", () => ExecuteAction("Set text color: Black"));
                        colorMenu.Item("Red", () => ExecuteAction("Set text color: Red"));
                        colorMenu.Item("Blue", () => ExecuteAction("Set text color: Blue"));
                        colorMenu.Item("Green", () => ExecuteAction("Set text color: Green"));
                    });
                });

                contextMenu.Separator();
                contextMenu.Item("Properties", () => ExecuteAction("Opened properties dialog"));
            });
        }
    }

    private static void DrawGrid(Gui gui)
    {
        if (gui.Pass != Pass.Pass2Render) return;

        var rect = gui.CurrentNode.Rect;
        var gridColor = _darkMode ? Color.FromArgb(100, 80, 80, 80) : Color.FromArgb(100, 220, 220, 220);
        var gridSize = 20 * _zoom;

        for (var x = rect.X; x < rect.X + rect.W; x += gridSize)
        {
            gui.DrawLine(new Vector2(x, rect.Y), new Vector2(x, rect.Y + rect.H), gridColor);
        }

        for (var y = rect.Y; y < rect.Y + rect.H; y += gridSize)
        {
            gui.DrawLine(new Vector2(rect.X, y), new Vector2(rect.X + rect.W, y), gridColor);
        }
    }

    private static void DrawRuler(Gui gui)
    {
        if (gui.Pass != Pass.Pass2Render) return;

        var rect = gui.CurrentNode.Rect;
        var rulerColor = _darkMode ? Color.FromArgb(255, 100, 100, 100) : Color.FromArgb(255, 180, 180, 180);
        var rulerHeight = 20;

        // Horizontal ruler
        gui.DrawRect(new Rect(rect.X, rect.Y, rect.W, rulerHeight), rulerColor);

        // Vertical ruler
        gui.DrawRect(new Rect(rect.X, rect.Y, rulerHeight, rect.H), rulerColor);
    }

    private static void ExecuteAction(string action)
    {
        _statusMessage = action;
        _lastActionTime = DateTime.Now;
        _documentModified = true;
        Console.WriteLine($"[{_lastActionTime:HH:mm:ss}] {action}");
    }
}
