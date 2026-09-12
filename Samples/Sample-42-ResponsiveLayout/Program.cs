using Guinevere;
using Guinevere.OpenGL.SilkNET;

namespace Sample_42_ResponsiveLayoutDemo;

public abstract class Program
{
    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui, 1024, 768, "Responsive Layout Demo");

        win.RunGui(() => Draw(gui));
    }

    private static void Draw(Gui gui)
    {
        var screenWidth = gui.ScreenRect.W;
        var isMobile = screenWidth < 600;
        var isTablet = screenWidth is >= 600 and < 900;

        gui.DrawRect(gui.ScreenRect, Color.AntiqueWhite);

        using (gui.Node().Expand().Margin(isMobile ? 10 : 20).Enter())
        {
            if (isMobile)
            {
                // Mobile layout - single column
                DrawMobileLayout(gui);
            }
            else if (isTablet)
            {
                // Tablet layout - flexible columns
                DrawTabletLayout(gui);
            }
            else
            {
                // Desktop layout - full three-column
                DrawDesktopLayout(gui);
            }
        }
    }

    private static void DrawMobileLayout(Gui gui)
    {
        // Header
        using (gui.Node().Height(60).ExpandWidth().Enter())
        {
            gui.DrawBackgroundRect(Color.FromArgb(52, 73, 94), 4);
            gui.DrawText("Mobile App", 18, Color.Beige);
        }

        // Content stack
        using (gui.Node().Expand().Gap(15).Enter())
        {
            DrawContentCard(gui, "Main Content", Color.FromArgb(46, 204, 113));
            DrawContentCard(gui, "Secondary", Color.FromArgb(52, 152, 219));
            DrawContentCard(gui, "Sidebar Content", Color.FromArgb(155, 89, 182));
        }

        // Bottom navigation
        using (gui.Node().Height(60).Direction(Axis.Horizontal).Gap(1).Enter())
        {
            DrawNavItem(gui, "Home", Color.FromArgb(46, 204, 113));
            DrawNavItem(gui, "Search", Color.FromArgb(52, 152, 219));
            DrawNavItem(gui, "Profile", Color.FromArgb(155, 89, 182));
            DrawNavItem(gui, "Settings", Color.FromArgb(241, 196, 15));
        }
    }

    private static void DrawTabletLayout(Gui gui)
    {
        // Header
        using (gui.Node().Height(60).ExpandWidth().Enter())
        {
            gui.DrawBackgroundRect(Color.FromArgb(52, 73, 94), 6);
            gui.DrawText("Tablet App", 20, Color.White);
        }

        // Two-column content
        using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(20).Enter())
        {
            // Main content (2/3)
            using (gui.Node().Expand().Enter())
            {
                DrawContentCard(gui, "Main Content Area", Color.FromArgb(46, 204, 113));
            }

            // Sidebar (1/3)
            using (gui.Node(250).Enter())
            {
                DrawContentCard(gui, "Sidebar", Color.FromArgb(155, 89, 182));
            }
        }
    }

    private static void DrawDesktopLayout(Gui gui)
    {
        // Header with navigation
        using (gui.Node().Height(60).ExpandWidth().Direction(Axis.Horizontal).Enter())
        {
            gui.DrawBackgroundRect(Color.FromArgb(52, 73, 94), 8);

            using (gui.Node(200).AlignSelf(0.5f).Margin(20).Enter())
            {
                gui.DrawText("Desktop App", 24, Color.White);
            }

            using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(10).AlignSelf(0.5f).Enter())
            {
                DrawNavItem(gui, "Dashboard", Color.FromArgb(46, 204, 113));
                DrawNavItem(gui, "Projects", Color.FromArgb(52, 152, 219));
                DrawNavItem(gui, "Team", Color.FromArgb(155, 89, 182));
                DrawNavItem(gui, "Settings", Color.FromArgb(241, 196, 15));
            }
        }

        // Three-column layout
        using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(20).Enter())
        {
            // Left sidebar
            using (gui.Node(250).ExpandHeight().Enter())
            {
                DrawContentCard(gui, "Left Sidebar", Color.FromArgb(230, 126, 34));
            }

            // Main content
            using (gui.Node().Expand().Enter())
            {
                DrawContentCard(gui, "Main Content Area", Color.FromArgb(46, 204, 113));
            }

            // Right sidebar
            using (gui.Node(250).ExpandHeight().Enter())
            {
                DrawContentCard(gui, "Right Sidebar", Color.FromArgb(155, 89, 182));
            }
        }
    }

    private static void DrawContentCard(Gui gui, string title, Color color)
    {
        using (gui.Node().Expand().Enter())
        {
            gui.DrawBackgroundRect(Color.White, 8);

            using (gui.Node().Expand().Margin(20).Enter())
            {
                gui.DrawBackgroundRect(color, 4);
                gui.DrawText(title, 16, Color.Black);
            }
        }
    }

    private static void DrawNavItem(Gui gui, string text, Color color)
    {
        using (gui.Node().Direction(Axis.Horizontal).Margin(5).Enter())
        {
            gui.DrawBackgroundRect(color, 4);
            gui.DrawText(text, 12, Color.White);
        }
    }
}
