using Guinevere;

namespace Controls_01;

public abstract partial class Program
{
    private static int _scrollDemo;
    private static string _scrollNodeId = "";
    private static readonly string[] ScrollDemoNames = ["List", "Nested", "Programmatic"];

    private static void ScrollingContent(Gui gui)
    {
        using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(15).Enter())
        {
            using (gui.Node(220).Enter())
            {
                gui.DrawBackgroundRect(Color.FromArgb(255, 248, 249, 250), radius: 8);

                using (gui.Node().Padding(10).Direction(Axis.Vertical).Gap(6).Enter())
                {
                    gui.DrawText("Scroll Demos", size: 16, color: Color.FromArgb(255, 51, 51, 51));

                    for (var i = 0; i < ScrollDemoNames.Length; i++)
                    {
                        var isSelected = i == _scrollDemo;
                        var bgColor = isSelected ? Color.FromArgb(255, 60, 120, 180) : Color.Transparent;
                        var textColor = isSelected ? Color.White : Color.Gray;

                        using (gui.Node(190, 28).Margin(0, 0, 0, 4).Padding(8, 5).Enter())
                        {
                            gui.DrawBackgroundRect(bgColor, 3);
                            gui.DrawText(ScrollDemoNames[i], color: textColor);

                            if (gui.GetInteractable().OnClick())
                            {
                                _scrollDemo = i;
                            }
                        }
                    }

                    if (_scrollDemo == 2)
                    {
                        gui.Node(0, 10);
                        gui.DrawText("Jump to position:", size: 12, color: Color.Gray);
                        if (ProgrammaticButton(gui, "Top"))
                        {
                            gui.ScrollToTop(_scrollNodeId);
                        }

                        if (ProgrammaticButton(gui, "Mid"))
                        {
                            gui.SetScrollPercentage(_scrollNodeId, Axis.Vertical, 0.5f);
                        }

                        if (ProgrammaticButton(gui, "Bottom"))
                        {
                            gui.ScrollToBottom(_scrollNodeId);
                        }
                    }
                }
            }

            using (gui.Node().Expand().Padding(10).Enter())
            {
                gui.DrawBackgroundRect(Color.FromArgb(255, 240, 242, 245), radius: 5);

                switch (_scrollDemo)
                {
                    case 0:
                        BasicScroll(gui);
                        break;
                    case 1:
                        NestedScroll(gui);
                        break;
                    default:
                        ProgrammaticScroll(gui);
                        break;
                }
            }
        }
    }

    private static bool ProgrammaticButton(Gui gui, string label)
    {
        var clicked = false;
        using (gui.Node(190, 26).Margin(0, 3, 0, 0).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                var interactable = gui.GetInteractable();
                gui.DrawBackgroundRect(interactable.OnHover()
                    ? Color.FromArgb(255, 90, 150, 210)
                    : Color.FromArgb(255, 225, 228, 233), 3);
                clicked = interactable.OnClick();
            }

            gui.DrawText(label, size: 12, color: clicked ? Color.White : Color.FromArgb(255, 70, 70, 70));
        }

        return clicked;
    }

    private static void BasicScroll(Gui gui)
    {
        using (gui.Node().Expand().Padding(10).Enter())
        {
            gui.DrawText("Basic Vertical Scrolling", size: 18);
            gui.DrawText("Mouse wheel to scroll, Shift+wheel for horizontal", size: 13, Color.Gray);

            using (gui.Node().Expand().Padding(10).Direction(Axis.Vertical).Gap(5).Margin(0, 15, 0, 0).Enter())
            {
                gui.DrawBackgroundRect(Color.FromArgb(255, 30, 30, 30), 3);
                gui.ScrollContainer(scrollY: true);

                for (var i = 0; i < 40; i++)
                {
                    using (gui.Node(0, 40).Margin(0, 0, 0, 5).Padding(10).Enter())
                    {
                        var color = i % 2 == 0 ? Color.FromArgb(255, 60, 60, 60) : Color.FromArgb(255, 70, 70, 70);
                        gui.DrawBackgroundRect(color, 2);
                        gui.DrawText($"Item {i + 1} - a scrollable row; click me", color: Color.White);

                        if (gui.GetInteractable().OnClick())
                            gui.DrawText($"[CLICKED]", color: Color.Yellow);
                    }
                }
            }
        }
    }

    private static void NestedScroll(Gui gui)
    {
        using (gui.Node().Expand().Padding(10).Enter())
        {
            gui.DrawText("Independent Scrolling Areas", size: 18);
            gui.DrawText("Each panel scrolls separately; the right one scrolls in both axes", size: 13, Color.Gray);

            using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(10).Margin(0, 15, 0, 0).Enter())
            {
                using (gui.Node().Expand().Padding(10).Enter())
                {
                    gui.DrawBackgroundRect(Color.FromArgb(255, 30, 30, 30), 3);
                    gui.DrawText("Left", size: 16);
                    gui.ScrollContainer(scrollY: true);

                    for (var i = 0; i < 25; i++)
                    {
                        using (gui.Node(0, 34).Margin(0, 0, 0, 3).Padding(8).Enter())
                        {
                            gui.DrawBackgroundRect(Color.FromArgb(255, 90, 60, 60), 2);
                            gui.DrawText($"Left {i + 1}", color: Color.White);
                        }
                    }
                }

                using (gui.Node().Expand().Padding(10).Enter())
                {
                    gui.DrawBackgroundRect(Color.FromArgb(255, 30, 30, 30), 3);
                    gui.DrawText("Right (H+V)", size: 16);
                    gui.ScrollContainer(scrollX: true, scrollY: true);

                    for (var i = 0; i < 15; i++)
                    {
                        using (gui.Node(0, 40).Margin(0, 0, 0, 3).Direction(Axis.Horizontal).Gap(5).Enter())
                        {
                            for (var j = 0; j < 12; j++)
                            {
                                using (gui.Node(110, 34).Padding(5).Enter())
                                {
                                    var hue = (i * 10 + j) * 15f % 360f;
                                    var color = Hsb(hue);
                                    gui.DrawBackgroundRect(color, 2);
                                    gui.DrawText($"R{i + 1}C{j + 1}", color: Color.Black, size: 12);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private static void ProgrammaticScroll(Gui gui)
    {
        using (gui.Node().Expand().Padding(10).Enter())
        {
            gui.DrawText("Programmatic Scroll Control", size: 18);
            gui.DrawText("Use the jump buttons on the left; clicking an item scrolls to it", size: 13, Color.Gray);

            using (gui.Node().Expand().Padding(10).Margin(0, 15, 0, 0).Enter())
            {
                gui.DrawBackgroundRect(Color.FromArgb(255, 30, 30, 30), 3);

                gui.ScrollContainer(scrollY: true);
                _scrollNodeId = gui.CurrentNode.Id;

                for (var i = 0; i < 80; i++)
                {
                    using (gui.Node(0, 40).Margin(0, 0, 0, 4).Padding(10).Enter())
                    {
                        var progress = i / 79f;
                        gui.DrawBackgroundRect(Hsb(progress * 240f), 3);
                        gui.DrawText($"Item {(i + 1):00} — position {progress:P1}", color: Color.Black, size: 13);

                        if (gui.GetInteractable().OnClick())
                        {
                            var target = i / 79f;
                            gui.SetScrollPercentage(_scrollNodeId, Axis.Vertical, target);
                        }
                    }
                }
            }
        }
    }
}
