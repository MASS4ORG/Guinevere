using Guinevere;
using Guinevere.OpenGL.SilkNET;

namespace Sample_41_AdvancedLayoutDemo;

public abstract class Program
{
    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui, 1200, 900, "Advanced Layout Demo - Dashboard");

        win.RunGui(() => Draw(gui));
    }

    private static void Draw(Gui gui)
    {
        // Fullscreen background
        gui.DrawRect(gui.ScreenRect, Color.DarkGray);

        // Main application layout
        using (gui.Node().Expand().Margin(20).Gap(10).Enter())
        {
            // Header bar
            using (gui.Node().Height(60).Direction(Axis.Horizontal).Enter())
            {
                gui.DrawRect(gui.CurrentNode.Rect, Color.FromArgb(52, 73, 94));

                // Logo area
                using (gui.Node(200, 40).AlignSelf(0.5f).Margin(10).Enter())
                {
                    gui.DrawText("MyApp", 24, Color.White);
                }

                // Navigation buttons
                using (gui.Node().AlignSelf(0.5f).Gap(15).Direction(Axis.Horizontal)
                           .Enter())
                {
                    DrawNavButton(gui, "Home", Color.FromArgb(46, 204, 113));
                    DrawNavButton(gui, "Projects", Color.FromArgb(52, 152, 219));
                    DrawNavButton(gui, "Settings", Color.FromArgb(155, 89, 182));
                }

                // User profile area
                using (gui.Node(150, 40).AlignSelf(0.5f).Margin(10).Enter())
                {
                    gui.DrawRect(gui.CurrentNode.Rect, Color.FromArgb(44, 62, 80), 20);
                    gui.DrawText("User Profile", 12, Color.White);
                }
            }

            // Main content area
            using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(15).Enter())
            {
                // Sidebar
                using (gui.Node(250).Enter())
                {
                    gui.DrawRect(gui.CurrentNode.Rect, Color.White, 8);

                    // Sidebar content
                    using (gui.Node().Expand().Margin(15).Gap(10).Enter())
                    {
                        gui.DrawText("Quick Actions", 16, Color.FromArgb(52, 73, 94));

                        DrawSidebarItem(gui, "📊 Dashboard", Color.FromArgb(241, 196, 15));
                        DrawSidebarItem(gui, "📁 Files", Color.FromArgb(230, 126, 34));
                        DrawSidebarItem(gui, "👥 Team", Color.FromArgb(26, 188, 156));
                        DrawSidebarItem(gui, "⚙️ Settings", Color.FromArgb(149, 165, 166));
                    }
                }

                // Main content panel
                using (gui.Node().Expand().Enter())
                {
                    gui.DrawRect(gui.CurrentNode.Rect, Color.White, 8);

                    // Content header
                    using (gui.Node().Height(80).Margin(20).AlignContent(0.0f).Enter())
                    {
                        gui.DrawText("Project Dashboard", 24, Color.FromArgb(52, 73, 94));
                        gui.DrawText("Overview of your current projects and tasks", 14,
                            Color.FromArgb(127, 140, 141));
                    }

                    // Cards grid
                    using (gui.Node().Expand().Margin(20).Gap(20).Enter())
                    {
                        DrawProjectCards(gui);
                    }
                }

                // Right panel
                using (gui.Node(300).Enter())
                {
                    gui.DrawRect(gui.CurrentNode.Rect, Color.White, 8);

                    using (gui.Node().Expand().Margin(15).Gap(15).Enter())
                    {
                        // Activity feed
                        gui.DrawText("Recent Activity", 16, Color.FromArgb(52, 73, 94));

                        DrawActivityItem(gui, "John updated Project Alpha", "2 min ago",
                            Color.FromArgb(46, 204, 113));
                        DrawActivityItem(gui, "New comment on Beta", "5 min ago", Color.FromArgb(52, 152, 219));
                        DrawActivityItem(gui, "Task completed", "10 min ago", Color.FromArgb(241, 196, 15));

                        // Quick stats
                        using (gui.Node().Height(200).Margin(0, 20).Enter())
                        {
                            gui.DrawRect(gui.CurrentNode.Rect, Color.FromArgb(236, 240, 241), 6);
                            gui.DrawText("Quick Stats", 14, Color.FromArgb(52, 73, 94));

                            DrawStatItem(gui, "Active Projects", "12", Color.FromArgb(46, 204, 113));
                            DrawStatItem(gui, "Pending Tasks", "24", Color.FromArgb(230, 126, 34));
                            DrawStatItem(gui, "Team Members", "8", Color.FromArgb(155, 89, 182));
                        }
                    }
                }
            }

            // Footer
            using (gui.Node().Height(40).Direction(Axis.Horizontal).AlignContent(0.5f).Enter())
            {
                gui.DrawRect(gui.CurrentNode.Rect, Color.FromArgb(52, 73, 94));

                using (gui.Node().AlignSelf(0.5f).Margin(20).Enter())
                {
                    gui.DrawText("© Gaya.", 12, Color.White);
                }

                using (gui.Node().AlignSelf(0.5f).Margin(20).Enter())
                {
                    gui.DrawText("v1.0.0", 12, Color.FromArgb(149, 165, 166));
                }
            }
        }
    }

    private static void DrawNavButton(Gui gui, string text, Color color)
    {
        using (gui.Node(80, 35).Enter())
        {
            gui.DrawRect(gui.CurrentNode.Rect, color, 4);
            gui.DrawText(text, 12, Color.White);
        }
    }

    private static void DrawSidebarItem(Gui gui, string text, Color accent)
    {
        using (gui.Node().Height(40).Direction(Axis.Horizontal).AlignContent(0.0f).Enter())
        {
            // Accent bar
            using (gui.Node(4, 40).Enter())
            {
                gui.DrawRect(gui.CurrentNode.Rect, accent);
            }

            // Text area
            using (gui.Node().Expand().Margin(10, 0).AlignContent(0.0f).Enter())
            {
                gui.DrawText(text, 14, Color.FromArgb(52, 73, 94));
            }
        }
    }

    private static void DrawProjectCards(Gui gui)
    {
        var projects = new[]
        {
            ("Project Alpha", "Backend Development", 75, Color.FromArgb(46, 204, 113)),
            ("Project Beta", "Frontend Design", 45, Color.FromArgb(52, 152, 219)),
            ("Project Gamma", "Data Analysis", 90, Color.FromArgb(155, 89, 182)),
            ("Project Delta", "Mobile App", 30, Color.FromArgb(241, 196, 15))
        };

        // Simple grid layout simulation
        foreach (var (name, description, progress, color) in projects)
        {
            using (gui.Node(250, 120).Enter())
            {
                gui.DrawRect(gui.CurrentNode.Rect, Color.White, 6);

                // Card header
                using (gui.Node().Height(30).Margin(15, 10).AlignContent(0.0f).Enter())
                {
                    gui.DrawText(name, 16, Color.FromArgb(52, 73, 94));
                }

                // Description
                using (gui.Node().Height(20).Margin(15, 5).AlignContent(0.0f).Enter())
                {
                    gui.DrawText(description, 12, Color.FromArgb(127, 140, 141));
                }

                // Progress bar
                using (gui.Node().Height(8).Margin(15, 10).Enter())
                {
                    gui.DrawRect(gui.CurrentNode.Rect, Color.FromArgb(236, 240, 241), 4);

                    var progressWidth = gui.CurrentNode.Rect.W * (progress / 100f);
                    var progressRect = new Rect(
                        gui.CurrentNode.Rect.X,
                        gui.CurrentNode.Rect.Y,
                        progressWidth,
                        gui.CurrentNode.Rect.H
                    );
                    gui.DrawRect(progressRect, color, 4);
                }

                // Progress text
                using (gui.Node().Height(20).Margin(15, 5).AlignContent(1.0f).Enter())
                {
                    gui.DrawText($"{progress}% Complete", 10, Color.FromArgb(127, 140, 141));
                }
            }
        }
    }

    private static void DrawActivityItem(Gui gui, string text, string time, Color accent)
    {
        using (gui.Node().Height(50).Direction(Axis.Horizontal).Enter())
        {
            // Accent dot
            using (gui.Node(8, 8).AlignSelf(0.2f).Enter())
            {
                gui.DrawRect(gui.CurrentNode.Rect, accent, 4);
            }

            // Content
            using (gui.Node().Expand().Margin(10, 0).Enter())
            {
                gui.DrawText(text, 12, Color.FromArgb(52, 73, 94));
                gui.DrawText(time, 10, Color.FromArgb(149, 165, 166));
            }
        }
    }

    private static void DrawStatItem(Gui gui, string label, string value, Color color)
    {
        using (gui.Node().Height(40).Direction(Axis.Horizontal).Margin(10).Enter())
        {
            using (gui.Node().Expand().AlignContent(0.0f).Enter())
            {
                gui.DrawText(label, 11, Color.FromArgb(127, 140, 141));
            }

            using (gui.Node().Enter())
            {
                gui.DrawText(value, 16, color);
            }
        }
    }
}
