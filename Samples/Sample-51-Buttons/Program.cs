using Guinevere;
using Guinevere.Vulkan.SilkNET;

namespace Sample_51_Buttons;

public abstract class Program
{
    private static int _buttonClickCount;
    private static int _iconButtonClickCount;
    private static string _lastClickedButton = "None";

    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui, 800, 800, "Button Controls Test");

        win.RunGui(() => Draw(gui));
    }

    private static void Draw(Gui gui)
    {
        // Background
        gui.DrawRect(gui.ScreenRect, Color.FromArgb(255, 240, 240, 240));

        // Main container
        using (gui.Node().Expand().Margin(20).Direction(Axis.Vertical).Gap(20).Enter())
        {
            gui.DrawBackgroundRect(Color.White, radius: 8);

            // Title
            using (gui.Node().Height(50).Enter())
            {
                gui.DrawText("Button Controls Test", size: 24, color: Color.FromArgb(255, 51, 51, 51));
            }

            // Content area
            using (gui.Node().Expand().Direction(Axis.Vertical).Gap(15).Margin(20).Enter())
            {
                // Basic Buttons Section
                using (gui.Node().Height(100).Direction(Axis.Vertical).Gap(10).Enter())
                {
                    gui.DrawText("Basic Buttons", size: 18, color: Color.FromArgb(255, 51, 51, 51));

                    using (gui.Node().Height(40).Direction(Axis.Horizontal).Gap(10).Enter())
                    {
                        // Default button (auto-sized)
                        if (gui.Button("Auto-sized Button"))
                        {
                            _buttonClickCount++;
                            _lastClickedButton = "Auto-sized";
                            Console.WriteLine("Auto-sized button clicked!");
                        }

                        // Fixed size button
                        if (gui.Button("Fixed Size", width: 120, height: 40))
                        {
                            _buttonClickCount++;
                            _lastClickedButton = "Fixed Size";
                            Console.WriteLine("Fixed size button clicked!");
                        }

                        // Small button
                        if (gui.Button("Small", width: 60, height: 24, fontSize: 12))
                        {
                            _buttonClickCount++;
                            _lastClickedButton = "Small";
                            Console.WriteLine("Small button clicked!");
                        }

                        // Large button
                        if (gui.Button("Large Button", width: 150, height: 50, fontSize: 18))
                        {
                            _buttonClickCount++;
                            _lastClickedButton = "Large";
                            Console.WriteLine("Large button clicked!");
                        }
                    }
                }

                // Colored Buttons Section
                using (gui.Node().Height(100).Direction(Axis.Vertical).Gap(10).Enter())
                {
                    gui.DrawText("Colored Buttons", size: 18, color: Color.FromArgb(255, 51, 51, 51));

                    using (gui.Node().Height(40).Direction(Axis.Horizontal).Gap(10).Enter())
                    {
                        // Primary button
                        if (gui.Button("Primary",
                                backgroundColor: Color.FromArgb(255, 0, 123, 255),
                                hoverColor: Color.FromArgb(255, 0, 86, 179),
                                pressedColor: Color.FromArgb(255, 0, 61, 128)))
                        {
                            _buttonClickCount++;
                            _lastClickedButton = "Primary";
                            Console.WriteLine("Primary button clicked!");
                        }

                        // Success button
                        if (gui.Button("Success",
                                backgroundColor: Color.FromArgb(255, 40, 167, 69),
                                hoverColor: Color.FromArgb(255, 34, 142, 58),
                                pressedColor: Color.FromArgb(255, 28, 117, 48)))
                        {
                            _buttonClickCount++;
                            _lastClickedButton = "Success";
                            Console.WriteLine("Success button clicked!");
                        }

                        // Warning button
                        if (gui.Button("Warning",
                                backgroundColor: Color.FromArgb(255, 255, 193, 7),
                                hoverColor: Color.FromArgb(255, 217, 164, 6),
                                pressedColor: Color.FromArgb(255, 180, 136, 5),
                                color: Color.Black))
                        {
                            _buttonClickCount++;
                            _lastClickedButton = "Warning";
                            Console.WriteLine("Warning button clicked!");
                        }

                        // Danger button
                        if (gui.Button("Danger",
                                backgroundColor: Color.FromArgb(255, 220, 53, 69),
                                hoverColor: Color.FromArgb(255, 187, 45, 59),
                                pressedColor: Color.FromArgb(255, 154, 37, 48)))
                        {
                            _buttonClickCount++;
                            _lastClickedButton = "Danger";
                            Console.WriteLine("Danger button clicked!");
                        }
                    }
                }

                // Styled Buttons Section
                using (gui.Node().Height(100).Direction(Axis.Vertical).Gap(10).Enter())
                {
                    gui.DrawText("Styled Buttons", size: 18, color: Color.FromArgb(255, 51, 51, 51));

                    using (gui.Node().Height(40).Direction(Axis.Horizontal).Gap(10).Enter())
                    {
                        // Rounded button
                        if (gui.Button("Rounded", radius: 20, width: 100))
                        {
                            _buttonClickCount++;
                            _lastClickedButton = "Rounded";
                            Console.WriteLine("Rounded button clicked!");
                        }

                        // Square button
                        if (gui.Button("Square", radius: 0, width: 80))
                        {
                            _buttonClickCount++;
                            _lastClickedButton = "Square";
                            Console.WriteLine("Square button clicked!");
                        }

                        // Custom font size
                        if (gui.Button("Big Text", fontSize: 20, width: 120, height: 45))
                        {
                            _buttonClickCount++;
                            _lastClickedButton = "Big Text";
                            Console.WriteLine("Big text button clicked!");
                        }

                        // Custom colors
                        if (gui.Button("Custom",
                                backgroundColor: Color.FromArgb(255, 106, 90, 205),
                                color: Color.White,
                                hoverColor: Color.FromArgb(255, 123, 104, 238),
                                width: 80))
                        {
                            _buttonClickCount++;
                            _lastClickedButton = "Custom";
                            Console.WriteLine("Custom button clicked!");
                        }
                    }
                }

                // Icon Buttons Section
                using (gui.Node().Height(100).Direction(Axis.Vertical).Gap(10).Enter())
                {
                    gui.DrawText("Icon Buttons", size: 18, color: Color.FromArgb(255, 51, 51, 51));

                    using (gui.Node().Height(40).Direction(Axis.Horizontal).Gap(10).Enter())
                    {
                        // Search icon
                        if (gui.IconButton("🔍", size: 40))
                        {
                            _iconButtonClickCount++;
                            _lastClickedButton = "Search Icon";
                            Console.WriteLine("Search icon clicked!");
                        }

                        // Settings icon
                        if (gui.IconButton("⚙️", size: 40,
                                backgroundColor: Color.FromArgb(100, 128, 128, 128)))
                        {
                            _iconButtonClickCount++;
                            _lastClickedButton = "Settings Icon";
                            Console.WriteLine("Settings icon clicked!");
                        }

                        // Heart icon
                        if (gui.IconButton("❤️", size: 40,
                                hoverColor: Color.FromArgb(255, 255, 182, 193)))
                        {
                            _iconButtonClickCount++;
                            _lastClickedButton = "Heart Icon";
                            Console.WriteLine("Heart icon clicked!");
                        }

                        // Star icon
                        if (gui.IconButton("⭐", size: 40,
                                backgroundColor: Color.FromArgb(255, 255, 215, 0),
                                color: Color.Black))
                        {
                            _iconButtonClickCount++;
                            _lastClickedButton = "Star Icon";
                            Console.WriteLine("Star icon clicked!");
                        }

                        // Large icon button
                        if (gui.IconButton("🚀", size: 60, fontSize: 24))
                        {
                            _iconButtonClickCount++;
                            _lastClickedButton = "Rocket Icon";
                            Console.WriteLine("Rocket icon clicked!");
                        }
                    }
                }

                // Edge Cases Section
                using (gui.Node().Height(100).Direction(Axis.Vertical).Gap(10).Enter())
                {
                    gui.DrawText("Edge Cases", size: 18, color: Color.FromArgb(255, 51, 51, 51));

                    using (gui.Node().Height(40).Direction(Axis.Horizontal).Gap(10).Enter())
                    {
                        // Empty text button
                        if (gui.Button("", width: 50, height: 30))
                        {
                            _buttonClickCount++;
                            _lastClickedButton = "Empty";
                            Console.WriteLine("Empty button clicked!");
                        }

                        // Very long text button
                        if (gui.Button("This is a very long button text that should auto-size properly"))
                        {
                            _buttonClickCount++;
                            _lastClickedButton = "Long Text";
                            Console.WriteLine("Long text button clicked!");
                        }

                        // Tiny button
                        if (gui.Button("T", width: 20, height: 20, fontSize: 10))
                        {
                            _buttonClickCount++;
                            _lastClickedButton = "Tiny";
                            Console.WriteLine("Tiny button clicked!");
                        }
                    }
                }

                // Statistics Section
                using (gui.Node().Height(120).Direction(Axis.Vertical).Gap(5).Enter())
                {
                    gui.DrawText("Click Statistics", size: 18, color: Color.FromArgb(255, 51, 51, 51));

                    gui.DrawText($"Button clicks: {_buttonClickCount}", size: 14, color: Color.FromArgb(255, 102, 102, 102));
                    gui.DrawText($"Icon button clicks: {_iconButtonClickCount}", size: 14, color: Color.FromArgb(255, 102, 102, 102));
                    gui.DrawText($"Total clicks: {_buttonClickCount + _iconButtonClickCount}", size: 14, color: Color.FromArgb(255, 102, 102, 102));
                    gui.DrawText($"Last clicked: {_lastClickedButton}", size: 14, color: Color.FromArgb(255, 102, 102, 102));
                    gui.DrawText($"FPS: {gui.Time.Fps:F1} | Frame: {gui.Time.Frames}",
                        size: 12, color: Color.FromArgb(255, 153, 153, 153));
                }
            }
        }
    }
}
