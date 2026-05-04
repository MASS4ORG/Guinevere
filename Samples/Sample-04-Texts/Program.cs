using System.Text;
using Guinevere;
using Guinevere.OpenGL.SilkNET;

namespace Sample_04_Texts;

public abstract class Program
{
    private static string _textInput = "Type here...";
    private static string _passwordInput = "";
    private static string _textArea =
        "Multi-line text area\nLine 2\nLine 3\nLine 4\nLine 5\nLine 6\nLine 7\nLine 8\nLine 9\nLine 10\nLine 11\nLine 12\nLine 13\nLine 14\nLine 15\nLine 16\nLine 17\nLine 18\nLine 19\nLine 20";
    private static int _activeTabIndex = 0;

    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui, 1000, 800, "Text & Scroll Showcase");

        win.RunGui(() => Draw(gui));
    }

    private static void Draw(Gui gui)
    {
        gui.DrawRect(gui.ScreenRect, Color.DarkGray);
        gui.DrawWindowTitlebar();

        using (gui.Node().Expand().Direction(Axis.Vertical).Padding(20).Enter())
        {
            // Title
            gui.DrawText("Guinevere Text & Scroll Showcase", 24, Color.White);

            gui.Node(10, 10); // Spacer

            // Create a scrollable container for the main content
            using (gui.Node(700, 500).Enter())
            {
                gui.DrawRectBorder(gui.CurrentNode.Rect, Color.Yellow, 2);

                // Use tabs to organize content
                gui.Tabs(ref _activeTabIndex, tabs =>
                {
                    tabs.Tab("Basic Text", () =>
                    {
                        using (gui.Node().Expand().Direction(Axis.Vertical).Padding(15).Enter())
                        {
                            gui.ScrollY(Color.Black, Color.Gray);
                            gui.ClipContent();
                            gui.DrawText("Basic Text Examples:", 18);

                            gui.Node(10, 10); // Spacer

                            gui.DrawText("Single line text", 14);
                            gui.DrawText("Multi-line text:\nLine 1\nLine 2\nLine 3", 14, Color.LightGray);
                            gui.DrawText("Colored text", 14, Color.Red);
                            gui.DrawText("Large text", 20);
                            gui.DrawText("Small text", 10);

                            gui.Node(10, 20); // Spacer

                            // Special characters and formatting
                            gui.DrawText("Special Characters & Formatting:", 18);
                            gui.DrawText("Unicode: 🚩❓💣★ ♥ ♦ ♣ ♠ → ← ↑ ↓", 14);
                            gui.DrawText("Numbers: 0123456789", 14);
                            gui.DrawText("Symbols: !@#$%^&*()_+-=[]{}|;':\",./<>?", 14);
                            gui.DrawText("Mixed: çáããüaáéíóú! 🌟", 14);

                            gui.Node(10, 20); // Spacer

                            // Text wrapping examples
                            gui.DrawText("Text Wrapping Examples:", 18);

                            gui.Node(10, 10); // Spacer

                            gui.DrawText("Unwrapped long text:", 14);
                            gui.DrawText(
                                "This is a very long line of text that should not wrap and will extend beyond the normal width boundaries to demonstrate the difference between wrapped and unwrapped text.",
                                12);

                            gui.Node(10, 10); // Spacer

                            gui.DrawText("Wrapped text (400px width):", 14);
                            gui.DrawText(
                                "This is a very long line of text that should wrap at 400 pixels width and demonstrate how the text wrapping functionality works in the Guinevere GUI system.",
                                12, wrapWidth: 400);

                            gui.Node(10, 10); // Spacer

                            gui.DrawText("Wrapped text (300px width):", 14);
                            gui.DrawText(
                                "This is another long line of text that should wrap at 300 pixels width, showing how different wrap widths affect the text layout and appearance.",
                                12, wrapWidth: 300);

                            gui.Node(10, 20); // Spacer

                            // Add more content to ensure scrolling is needed
                            for (var i = 0; i < 10; i++)
                            {
                                gui.DrawText(
                                    $"Extra text line {i + 1}: The quick brown fox jumps over the lazy dog", 12);
                            }
                        }
                    });

                    tabs.Tab("Color Demo", () =>
                    {
                        using (gui.Node().Expand().Direction(Axis.Vertical).Padding(15).Enter())
                        {
                            gui.ScrollY(Color.Black, Color.Gray);
                            gui.DrawText("Color Variations:", 18, Color.Yellow);

                            gui.Node(10, 15); // Spacer

                            // Rainbow text demonstration
                            for (var i = 0; i < 25; i++)
                            {
                                var hue = (i * 15) % 360;
                                var color = Color.FromArgb(255,
                                    (int)(Math.Sin(hue * Math.PI / 180) * 127 + 128),
                                    (int)(Math.Sin((hue + 120) * Math.PI / 180) * 127 + 128),
                                    (int)(Math.Sin((hue + 240) * Math.PI / 180) * 127 + 128));
                                gui.DrawText(
                                    $"Rainbow text line {i + 1} - The quick brown fox jumps over the lazy dog", 14,
                                    color);
                            }

                            gui.Node(10, 20); // Spacer

                            // Gradient-like effect with different shades
                            gui.DrawText("Gradient-like Color Effects:", 16, Color.Yellow);
                            for (var i = 0; i < 10; i++)
                            {
                                var intensity = (byte)(50 + i * 20);
                                gui.DrawText($"Shade level {i + 1}: Various intensities of red", 12,
                                    Color.FromArgb(255, intensity, 0, 0));
                            }

                            for (var i = 0; i < 10; i++)
                            {
                                var intensity = (byte)(50 + i * 20);
                                gui.DrawText($"Shade level {i + 1}: Various intensities of green", 12,
                                    Color.FromArgb(255, 0, intensity, 0));
                            }

                            for (var i = 0; i < 10; i++)
                            {
                                var intensity = (byte)(50 + i * 20);
                                gui.DrawText($"Shade level {i + 1}: Various intensities of blue", 12,
                                    Color.FromArgb(255, 0, 0, intensity));
                            }
                        }
                    });

                    tabs.Tab("Text Inputs", () =>
                    {
                        using (gui.Node().Expand().Direction(Axis.Vertical).Padding(15).Enter())
                        {
                            gui.ScrollY(Color.Black, Color.Gray);
                            gui.DrawText("Text Input Examples:", 18, Color.Yellow);

                            gui.Node(10, 15); // Small spacer

                            gui.DrawText("Text Input:", 14, Color.White);

                            gui.TextInput(ref _textInput, width: 500);

                            gui.Node(10, 15); // Small spacer

                            gui.DrawText("Password Input:", 14, Color.White);

                            gui.PasswordInput(ref _passwordInput, width: 500, placeholder: "Enter password...");

                            gui.Node(10, 15); // Small spacer

                            gui.DrawText("Text Area (Auto-scrollable):", 14);

                            gui.TextArea(ref _textArea, width: 400, height: 250);

                            gui.Node(10, 15); // Small spacer

                            // Display input values
                            gui.DrawText("Current Values:", 16);
                            gui.DrawText($"Text Input: '{_textInput}'", 12, Color.LightBlue);
                            gui.DrawText($"Password Length: {_passwordInput.Length} characters", 12,
                                Color.LightBlue);
                            gui.DrawText(
                                $"Text Area Preview: {_textArea.Replace('\n', ' ').Substring(0, Math.Min(50, _textArea.Length))}...",
                                12, Color.LightBlue);

                            gui.Node(10, 20); // Spacer

                            // Add some extra content for scrolling
                            gui.DrawText("Additional Input Information:", 16, Color.Yellow);
                            gui.DrawText(
                                "- Text inputs support standard keyboard shortcuts (Ctrl+A, Ctrl+C, Ctrl+V, etc.)",
                                12, Color.White);
                            gui.DrawText("- Password inputs hide the actual characters typed", 12, Color.White);
                            gui.DrawText("- Text areas support multi-line input with automatic scrolling", 12,
                                Color.White);
                            gui.DrawText("- All inputs show a blinking cursor when focused", 12, Color.White);
                            gui.DrawText("- Click anywhere in an input field to position the cursor", 12,
                                Color.White);

                            for (var i = 0; i < 10; i++)
                            {
                                gui.DrawText(
                                    $"Extra info line {i + 1}: More content to demonstrate scrolling in this tab",
                                    11, Color.LightGray);
                            }
                        }
                    });

                    tabs.Tab("Long Content", () =>
                    {
                        using (gui.Node().Expand().Direction(Axis.Vertical).Padding(15).Enter())
                        {
                            gui.ScrollY(Color.Black, Color.Gray);
                            gui.DrawText("Long Content (Scroll to see more):", 18, Color.Yellow);

                            gui.Node(10, 15); // Spacer

                            // Lorem ipsum content
                            StringBuilder text = new();
                            for (var i = 0; i < 40; i++)
                            {
                                text.AppendLine(
                                    $"Line {i + 1}: Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.");
                            }

                            gui.DrawText(text.ToString(), 12, Color.DarkSlateGray);

                            gui.Node(10, 20); // Spacer

                            gui.DrawText("Mixed Content Section:", 16);

                            // More content with various sizes and colors
                            for (var i = 0; i < 30; i++)
                            {
                                var fontSize = 10 + (i % 4) * 2;
                                var color = Color.FromArgb(255,
                                    100 + (i * 15) % 155,
                                    150,
                                    200 - (i * 10) % 100);
                                gui.Node(10, 20);
                                {
                                    gui.DrawText(
                                        $"Mixed line {i + 1}: Various text sizes and colors to fill the scrollable area - this demonstrates the scrolling functionality",
                                        fontSize, color);
                                }
                            }

                            gui.Node(10, 30); // Bottom spacer

                            gui.DrawText("End of Long Content", 16, Color.Red);
                        }
                    });
                });
            }

            gui.Node(10, 15); // Small Spacer

            // Instructions
            gui.DrawText("Instructions:", 14, Color.White);
            gui.DrawText("• Use mouse wheel over the yellow box to scroll vertically", 12, Color.White);
            gui.DrawText("• Hold Shift + mouse wheel for horizontal scrolling (if content is wider)", 12,
                Color.LightGray);
            gui.DrawText("• Drag the red scrollbar thumb to scroll directly", 12, Color.White);
            gui.DrawText("• Click on tabs above to switch between different content sections", 12, Color.White);
            gui.DrawText("• Text inputs show white cursors when focused", 12, Color.White);
        }
    }
}
