using Guinevere;

namespace Controls_01;

public abstract partial class Program
{
    private static int _nestedTab;
    private static int _pillTab;
    private static int _verticalTab;
    private static int _activeTab;

    private static void NavigationContent(Gui gui)
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(16).Padding(10).Enter())
        {
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
        }
    }
}
