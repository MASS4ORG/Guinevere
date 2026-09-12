using System.Numerics;
using Guinevere;
using Guinevere.OpenGL.SilkNET;

namespace Sample_07_Scroll;

public static class Program
{
    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui, 1200, 800, "Guinevere Advanced Scroll Demo");
        var demo = new ScrollDemo(gui);
        win.RunGui(demo.Draw);
    }
}

public class ScrollDemo(Gui gui)
{
    private int _selectedDemo;

    private readonly string[] _demoNames =
    [
        "Basic Scroll", "Nested Scroll", "Mixed Content", "Programmatic Scroll", "Performance Test"
    ];

    private float _scrollPercentage = 0.5f;
    private string _scrollNodeId = "";

    public void Draw()
    {
        gui.DrawRect(gui.ScreenRect, Color.FromArgb(255, 35, 35, 35));
        gui.SetTextColor(Color.White);

        using (gui
                   .Node(800,600)
                   // .Node().Expand()
                   .Padding(20).Direction(Axis.Horizontal).Enter())
        {
            // Left sidebar for demo selection
            using (gui.Node(250).Margin(0, 0, 20, 0).Padding(10).Enter())
            {
                gui.DrawBackgroundRect(Color.FromArgb(255, 40, 40, 40), 5);
                gui.DrawText($"{gui.Time.SmoothFps:N0} FPS");
                gui.DrawText("Scroll Demos", size: 18);

                gui.Node(0, 20); // Spacing

                for (var i = 0; i < _demoNames.Length; i++)
                {
                    var isSelected = i == _selectedDemo;
                    var bgColor = isSelected ? Color.FromArgb(255, 60, 120, 180) : Color.Transparent;
                    var textColor = isSelected ? Color.White : Color.Gray;

                    using (gui.Node(220, 30).Margin(0, 0, 0, 5).Padding(8, 5).Enter())
                    {
                        gui.DrawBackgroundRect(bgColor, 3);
                        gui.DrawText(_demoNames[i], color: textColor);

                        if (gui.GetInteractable().OnClick())
                        {
                            _selectedDemo = i;
                        }
                    }
                }

                // Add programmatic scroll controls for demo 3
                if (_selectedDemo == 3)
                {
                    gui.Node(0, 30); // Spacing
                    gui.DrawText("Scroll Controls", size: 16);

                    gui.Node(0, 10); // Spacing

                    // Scroll percentage slider
                    using (gui.Node(200, 25).Padding(5).Enter())
                    {
                        gui.DrawText($"Scroll: {_scrollPercentage:F2}", size: 12, color: Color.Gray);

                        using (gui.Node(180, 15).Margin(0, 5, 0, 0).Enter())
                        {
                            gui.DrawBackgroundRect(Color.FromArgb(255, 60, 60, 60), 2);

                            var handleX = _scrollPercentage * 160;
                            using (gui.Node(20, 15).Left(handleX).Top(0).Enter())
                            {
                                gui.DrawBackgroundRect(Color.FromArgb(255, 100, 150, 200), 2);

                                if (gui.GetInteractable().OnClick())
                                {
                                    var mouseX = gui.Input.MousePosition.X - gui.CurrentNode.Rect.X;
                                    _scrollPercentage = Math.Clamp(mouseX / 160f, 0f, 1f);
                                    if (!string.IsNullOrEmpty(_scrollNodeId))
                                    {
                                        gui.SetScrollPercentage(_scrollNodeId, Axis.Vertical, _scrollPercentage);
                                    }
                                }
                            }
                        }
                    }

                    // Control buttons
                    using (gui.Node(200, 25).Margin(0, 5, 0, 0).Direction(Axis.Horizontal).Gap(5).Enter())
                    {
                        using (gui.Node(45, 25).Padding(5).Enter())
                        {
                            gui.DrawBackgroundRect(Color.FromArgb(255, 80, 80, 80), 3);
                            gui.DrawText("Top", size: 10);

                            if (gui.GetInteractable().OnClick() && !string.IsNullOrEmpty(_scrollNodeId))
                            {
                                gui.ScrollToTop(_scrollNodeId);
                                _scrollPercentage = 0f;
                            }
                        }

                        using (gui.Node(45, 25).Padding(5).Enter())
                        {
                            gui.DrawBackgroundRect(Color.FromArgb(255, 80, 80, 80), 3);
                            gui.DrawText("Mid", size: 10);

                            if (gui.GetInteractable().OnClick() && !string.IsNullOrEmpty(_scrollNodeId))
                            {
                                gui.SetScrollPercentage(_scrollNodeId, Axis.Vertical, 0.5f);
                                _scrollPercentage = 0.5f;
                            }
                        }

                        using (gui.Node(45, 25).Padding(5).Enter())
                        {
                            gui.DrawBackgroundRect(Color.FromArgb(255, 80, 80, 80), 3);
                            gui.DrawText("Bot", size: 10);

                            if (gui.GetInteractable().OnClick() && !string.IsNullOrEmpty(_scrollNodeId))
                            {
                                gui.ScrollToBottom(_scrollNodeId);
                                _scrollPercentage = 1f;
                            }
                        }
                    }
                }
            }

            // Main content area
            using (gui.Node().Expand().Padding(10).Enter())
            {
                gui.DrawBackgroundRect(Color.FromArgb(255, 45, 45, 45), 5);

                switch (_selectedDemo)
                {
                    case 0:
                        BuildBasicScrollDemo();
                        break;
                    case 1:
                        BuildNestedScrollDemo();
                        break;
                    case 2:
                        BuildMixedContentDemo();
                        break;
                    case 3:
                        BuildProgrammaticScrollDemo();
                        break;
                    case 4:
                        BuildPerformanceTestDemo();
                        break;
                }
            }
        }
    }

    private void BuildBasicScrollDemo()
    {
        using (gui.Node().Expand().Padding(20).Enter())
        {
            gui.DrawText("Basic Vertical Scrolling", size: 20);
            gui.DrawText("Mouse wheel to scroll, Shift+wheel for horizontal", size: 14, color: Color.Gray);

            gui.Node(0, 20); // Spacing

            using (gui.Node().Expand().Padding(10).Direction(Axis.Vertical).Gap(5).Enter())
            {
                gui.DrawBackgroundRect(Color.FromArgb(255, 30, 30, 30), 3);
                gui.ScrollContainer(scrollY: true);
                // Create a long list of items to scroll through
                for (var i = 0; i < 50; i++)
                {
                    using (gui.Node(0, 40).Margin(0, 0, 0, 5).Padding(10).Enter())
                    {
                        var color = i % 2 == 0 ? Color.FromArgb(255, 50, 50, 50) : Color.FromArgb(255, 60, 60, 60);
                        gui.DrawBackgroundRect(color, 2);
                        gui.DrawText($"Item {i + 1} - This is a scrollable item with some content");

                        if (gui.GetInteractable().OnClick())
                        {
                            gui.DrawText($" [CLICKED]", color: Color.Yellow);
                        }
                    }
                }
            }
        }
    }

    private void BuildNestedScrollDemo()
    {
        using (gui.Node().Expand().Padding(20).Enter())
        {
            gui.DrawText("Nested Scrolling Areas", size: 20);
            gui.DrawText("Independent scroll areas within each other", size: 14, color: Color.Gray);

            gui.Node(0, 20); // Spacing

            using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(10).Enter())
            {
                // Left scroll area
                using (gui.Node().Expand().Padding(10).Enter())
                {
                    gui.DrawBackgroundRect(Color.FromArgb(255, 30, 30, 30), 3);
                    gui.DrawText("Left Panel", size: 16);
                    gui.ScrollContainer(scrollY: true);
                    for (var i = 0; i < 30; i++)
                    {
                        using (gui.Node(0, 35).Margin(0, 0, 0, 3).Padding(8).Enter())
                        {
                            gui.DrawBackgroundRect(Color.FromArgb(255, 80, 50, 50), 2);
                            gui.DrawText($"Left {i + 1}");

                            if (gui.GetInteractable().OnClick())
                            {Console.WriteLine("asdfasdfasdf");}
                    }
                    }
                }

                // Right scroll area with horizontal scrolling
                using (gui.Node().Expand().Padding(10).Enter())
                {
                    gui.DrawBackgroundRect(Color.FromArgb(255, 30, 30, 30), 3);
                    gui.DrawText("Right Panel (H+V Scroll)", size: 16);
                    gui.ScrollContainer(scrollX: true, scrollY: true);
                    for (var i = 0; i < 20; i++)
                    {
                        using (gui.Node(0, 40).Margin(0, 0, 0, 3).Direction(Axis.Horizontal).Gap(5).Enter())
                        {
                            for (var j = 0; j < 10; j++)
                            {
                                using (gui.Node(120, 35).Padding(5).Enter())
                                {
                                    var hue = (i * 10 + j) * 15f % 360f;
                                    var color = Color.FromArgb(255,
                                        (byte)(128 + 127 * Math.Sin(hue * Math.PI / 180)),
                                        (byte)(128 + 127 * Math.Sin((hue + 120) * Math.PI / 180)),
                                        (byte)(128 + 127 * Math.Sin((hue + 240) * Math.PI / 180)));
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

    private void BuildMixedContentDemo()
    {
        using (gui.Node().Expand().Padding(20).Enter())
        {
            gui.DrawText("Mixed Content Scrolling", size: 20);
            gui.DrawText("Shapes, text, and interactive elements", size: 14, color: Color.Gray);

            gui.Node(0, 20); // Spacing

            using (gui.Node().Expand().Padding(10).Enter())
            {
                gui.DrawBackgroundRect(Color.FromArgb(255, 30, 30, 30), 3);
                gui.ScrollContainer(scrollY: true);
                // Mixed content with various elements
                for (var i = 0; i < 25; i++)
                {
                    using (gui.Node(0, 80).Margin(0, 0, 0, 10).Padding(15).Enter())
                    {
                        gui.DrawBackgroundRect(Color.FromArgb(255, 40, 40, 40), 5);

                        // Header
                        gui.DrawText($"Section {i + 1}", size: 16);

                        // Shape and content
                        using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(10).Margin(0, 10, 0, 0).Enter())
                        {
                            // Shape
                            using (gui.Node(50, 50).Enter())
                            {
                                var hue = (i * 30f % 360f);
                                var shapeColor = Color.FromArgb(255,
                                    (byte)(128 + 127 * Math.Sin(hue * Math.PI / 180)),
                                    (byte)(128 + 127 * Math.Sin((hue + 120) * Math.PI / 180)),
                                    (byte)(128 + 127 * Math.Sin((hue + 240) * Math.PI / 180)));

                                switch (i % 4)
                                {
                                    case 0:
                                        gui.DrawRect(gui.CurrentNode.InnerRect, shapeColor);
                                        break;
                                    case 1:
                                        gui.DrawCircle(gui.CurrentNode.Center, 25, shapeColor);
                                        break;
                                    case 2:
                                        gui.DrawTriangle(
                                            new Vector2(gui.CurrentNode.InnerRect.X + 25,
                                                gui.CurrentNode.InnerRect.Y),
                                            new Vector2(gui.CurrentNode.InnerRect.X,
                                                gui.CurrentNode.InnerRect.Y + 50),
                                            new Vector2(gui.CurrentNode.InnerRect.X + 50,
                                                gui.CurrentNode.InnerRect.Y + 50),
                                            shapeColor
                                        );
                                        break;
                                    case 3:
                                        gui.DrawRect(gui.CurrentNode.InnerRect, shapeColor);
                                        break;
                                }
                            }

                            // Text content
                            using (gui.Node().Expand().Enter())
                            {
                                gui.DrawText($"This is content for section {i + 1}. ", color: Color.LightGray);
                                gui.DrawText("It contains various types of content including shapes and text.",
                                    color: Color.LightGray);
                            }
                        }
                    }
                }
            }
        }
    }

    private void BuildProgrammaticScrollDemo()
    {
        using (gui.Node().Expand().Padding(20).Enter())
        {
            gui.DrawText("Programmatic Scroll Control", size: 20);
            gui.DrawText("Use the controls on the left to manipulate scrolling via API", size: 14, color: Color.Gray);

            gui.Node(0, 20); // Spacing

            using (gui.Node().Expand().Padding(10).Enter())
            {
                gui.DrawBackgroundRect(Color.FromArgb(255, 30, 30, 30), 3);

                using (var scrollNode = gui.ScrollContainer(scrollY: true).Enter())
                {
                    _scrollNodeId = scrollNode.Node.Id;

                    // Update scroll percentage from current state
                    _scrollPercentage = gui.GetScrollPercentage(_scrollNodeId, Axis.Vertical);

                    // Generate content to scroll through
                    for (var i = 0; i < 100; i++)
                    {
                        using (gui.Node(0, 50).Margin(0, 0, 0, 5).Padding(10).Enter())
                        {
                            var progress = i / 99f;
                            var hue = progress * 240f;
                            var color = Color.FromArgb(255,
                                (byte)(128 + 127 * Math.Sin(hue * Math.PI / 180)),
                                (byte)(128 + 127 * Math.Sin((hue + 120) * Math.PI / 180)),
                                (byte)(128 + 127 * Math.Sin((hue + 240) * Math.PI / 180)));
                            gui.DrawBackgroundRect(color, 3);

                            gui.DrawText($"Programmable Item {i + 1}", color: Color.Black, size: 14);
                            gui.DrawText($"Position: {progress:P1}", color: Color.Black, size: 12);

                            if (gui.GetInteractable().OnClick())
                            {
                                // Scroll to clicked item
                                var targetPercentage = i / 99f;
                                gui.SetScrollPercentage(_scrollNodeId, Axis.Vertical, targetPercentage);
                                _scrollPercentage = targetPercentage;
                            }
                        }
                    }
                }
            }
        }
    }

    private void BuildPerformanceTestDemo()
    {
        using (gui.Node().Expand().Padding(20).Enter())
        {
            gui.DrawText("Performance Test", size: 20);
            gui.DrawText("Large number of items to test scroll performance", size: 14, color: Color.Gray);

            gui.Node(0, 20); // Spacing

            using (gui.Node().Expand().Padding(10).Enter())
            {
                gui.DrawBackgroundRect(Color.FromArgb(255, 30, 30, 30), 3);
                gui.ScrollContainer(scrollX: true, scrollY: true);
                // Large grid of items
                for (var row = 0; row < 200; row++)
                {
                    using (gui.Node(0, 30).Margin(0, 0, 0, 2).Direction(Axis.Horizontal).Gap(2).Enter())
                    {
                        for (var col = 0; col < 50; col++)
                        {
                            using (gui.Node(60, 25).Padding(3).Enter())
                            {
                                var index = row * 50 + col;
                                var hue = (index * 7) % 360f;
                                var color = Color.FromArgb(255,
                                    (byte)(128 + 127 * Math.Sin(hue * Math.PI / 180)),
                                    (byte)(128 + 127 * Math.Sin((hue + 120) * Math.PI / 180)),
                                    (byte)(128 + 127 * Math.Sin((hue + 240) * Math.PI / 180)));
                                gui.DrawBackgroundRect(color, 1);
                                gui.DrawText($"{index}", size: 8);

                                if (gui.GetInteractable().OnClick())
                                {
                                    gui.DrawBackgroundRect(Color.Yellow, 1);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
