using Guinevere;

namespace Example01;

public partial class Shared
{
    static readonly string[] ResponsiveNames = ["Mobile", "Tablet", "Desktop"];
    static readonly string[] ResponsiveTabs = ["Home", "Search", "Profile", "Settings"];
    static int _responsiveTab;

    /// <summary>The layout depends on the width the stage has: a column, two columns or three.</summary>
    static void Responsive(Gui gui, ColorPalette p)
    {
        var screenWidth = gui.ScreenRect.W;
        var width = screenWidth - (screenWidth < NarrowWidth ? 64f : 270f);
        var layout = width < 480f ? 0 : width < 720f ? 1 : 2;

        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(10).Enter())
        {
            Label(gui, p, $"Stage about {width:0}px wide: {ResponsiveNames[layout]} layout. Resize across 480 and 720.");
            using (gui.Node().Expand().Direction(Axis.Vertical).Gap(10).Enter())
            {
                switch (layout)
                {
                    case 0:
                        ResponsiveHeader(gui, p, "Mobile app", showTabs: false);
                        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(8).Enter())
                        {
                            ResponsiveBlock(gui, p, "Main content", 0);
                            ResponsiveBlock(gui, p, "Secondary", 1);
                        }

                        ResponsiveTabBar(gui, p);
                        break;
                    case 1:
                        ResponsiveHeader(gui, p, "Tablet app", showTabs: false);
                        using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(10).Enter())
                        {
                            ResponsiveBlock(gui, p, "Main content", 0);
                            using (gui.Node(170).ExpandHeight().Enter())
                            {
                                ResponsiveBlock(gui, p, "Sidebar", 4);
                            }
                        }

                        break;
                    default:
                        ResponsiveHeader(gui, p, "Desktop app", showTabs: true);
                        using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(10).Enter())
                        {
                            using (gui.Node(170).ExpandHeight().Enter())
                            {
                                ResponsiveBlock(gui, p, "Left sidebar", 2);
                            }

                            ResponsiveBlock(gui, p, "Main content", 0);
                            using (gui.Node(170).ExpandHeight().Enter())
                            {
                                ResponsiveBlock(gui, p, "Right sidebar", 4);
                            }
                        }

                        break;
                }
            }
        }
    }

    static void ResponsiveHeader(Gui gui, ColorPalette p, string title, bool showTabs)
    {
        using (gui.Node().ExpandWidth().Height(48).Direction(Axis.Horizontal).Gap(8).Padding(14, 0).ContentAlignY(0.5f)
                   .Enter())
        {
            gui.DrawBackgroundRect(p.SurfaceRaised, 10);
            gui.DrawText(title, 16, p.Text);
            gui.Node().Expand();
            if (showTabs)
            {
                ResponsiveTabButtons(gui, p);
            }
        }
    }

    static void ResponsiveTabBar(Gui gui, ColorPalette p)
    {
        using (gui.Node().ExpandWidth().Height(48).Direction(Axis.Horizontal).Gap(6).Padding(8, 0).ContentAlignX(0.5f)
                   .ContentAlignY(0.5f).Enter())
        {
            gui.DrawBackgroundRect(p.SurfaceRaised, 10);
            ResponsiveTabButtons(gui, p);
        }
    }

    static void ResponsiveTabButtons(Gui gui, ColorPalette p)
    {
        for (var i = 0; i < ResponsiveTabs.Length; i++)
        {
            var index = i;
            if (Button(gui, p, ResponsiveTabs[i], i == _responsiveTab, $"responsive/{i}"))
            {
                Later(() => _responsiveTab = index);
            }
        }
    }

    static void ResponsiveBlock(Gui gui, ColorPalette p, string title, int color)
    {
        using (gui.Node().Expand().AlignContent(0.5f, 0.5f).Direction(Axis.Vertical).Gap(4).Enter())
        {
            gui.DrawBackgroundRect(p.Pick(color), 12);
            gui.DrawText(title, 15, p.AccentText);
            gui.DrawText(ResponsiveTabs[_responsiveTab], 12, p.AccentText);
        }
    }
}
