using Guinevere;
using Guinevere.OpenGL.SilkNET;

namespace Sample_50_Controls;

public abstract class Program
{
    private static bool _checkbox1;
    private static bool _checkbox2 = true;
    private static bool _toggle1;
    private static bool _toggle2 = true;
    private static string _textInput = "Hello World";
    private static string _passwordInput = "";

    private static string _textArea =
        "This is a\nmultiline\ntext area\nThis is a\nmultiline\ntext area\nThis is a\nmultiline\ntext area";

    private static int _dropdown1 = -1;
    private static int _dropdown2 = 1;
    private static readonly string[] DropdownOptions = ["Option 1", "Option 2", "Option 3", "Option 4", "Option 5"];
    private static int _activeTab;
    private static int _verticalTab;
    private static int _pillTab;

    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui, 1000, 1200, "Primitive UI Controls Demo");

        win.RunGui(() => Draw(gui));
    }

    private static void Draw(Gui gui)
    {
        gui.DrawRect(gui.ScreenRect, Color.FromArgb(255, 245, 245, 245));

        using (gui.Node().Expand().Margin(20).Direction(Axis.Vertical).Gap(20).Enter())
        {
            gui.DrawBackgroundRect(Color.White, radius: 10);

            // Title
            using (gui.Node().Height(50).Enter())
            {
                gui.DrawText("Primitive UI Controls Demo", size: 24, color: Color.FromArgb(255, 51, 51, 51));
            }

            // Main content area
            using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(20).Margin(20).Enter())
            {
                // Left side - Tabs
                using (gui.Node().Width(600).Enter())
                {
                    gui.Tabs(ref _activeTab, tabs =>
                    {
                        tabs.Tab("Buttons", () => RenderButtonsTab(gui));
                        tabs.Tab("Inputs", () => RenderInputsTab(gui));
                        tabs.Tab("Tab Controls", () => RenderTabControlsTab(gui));
                    });
                }

                // Right side - Current Values Display
                using (gui.Node().Width(300).Enter())
                {
                    RenderCurrentValues(gui);
                }
            }

            // Footer
            using (gui.Node().Height(40).Enter())
            {
                gui.DrawText($"Frame: {gui.Time.Frames} | FPS: {gui.Time.SmoothFps:N1}",
                    size: 12, color: Color.FromArgb(255, 153, 153, 153));
            }
        }
    }

    private static void RenderButtonsTab(Gui gui)
    {
        using (gui.Node().Direction(Axis.Vertical).Gap(15).Enter())
        {
            gui.DrawText("Button Controls", size: 18, color: Color.FromArgb(255, 51, 51, 51));

            using (gui.Node().Height(40).Direction(Axis.Horizontal).Gap(10).Enter())
            {
                if (gui.Button(new("Primary Button")))
                {
                    Console.WriteLine("Primary button clicked!");
                }

                if (gui.Button("Secondary", backgroundColor: Color.FromArgb(255, 108, 117, 125)))
                {
                    Console.WriteLine("Secondary button clicked!");
                }

                if (gui.Button("Success", backgroundColor: Color.FromArgb(255, 76, 175, 80)))
                {
                    Console.WriteLine("Success button clicked!");
                }

                if (gui.Button("Danger", backgroundColor: Color.FromArgb(255, 244, 67, 54)))
                {
                    Console.WriteLine("Danger button clicked!");
                }

                if (gui.IconButton("🔍", size: 40))
                {
                    Console.WriteLine("Search icon clicked!");
                }
            }
        }
    }

    private static void RenderInputsTab(Gui gui)
    {
        using (gui.Node().Direction(Axis.Vertical).Gap(15).Enter())
        {
            gui.DrawText("Input Controls", size: 18, color: Color.FromArgb(255, 51, 51, 51));

            // Checkboxes
            using (gui.Node().Height(80).Direction(Axis.Vertical).Gap(10).Enter())
            {
                using (gui.Node().Height(30).Direction(Axis.Horizontal).Gap(20).Enter())
                {
                    gui.Checkbox(ref _checkbox1, "Enable notifications");
                    gui.Checkbox(ref _checkbox2, "Auto-save documents");
                }
            }

            // Toggles
            using (gui.Node().Height(80).Direction(Axis.Vertical).Gap(10).Enter())
            {
                using (gui.Node().Height(30).Direction(Axis.Horizontal).Gap(20).Enter())
                {
                    gui.Toggle(ref _toggle1, "Dark mode");
                    gui.Toggle(ref _toggle2, "High contrast",
                        onColor: Color.FromArgb(255, 156, 39, 176),
                        offColor: Color.FromArgb(255, 158, 158, 158));
                }
            }

            // Text Inputs
            using (gui.Node().Height(120).Direction(Axis.Vertical).Gap(10).Enter())
            {
                using (gui.Node().Height(40).Direction(Axis.Horizontal).Gap(10).Enter())
                {
                    using (gui.Node().Width(200).Enter())
                    {
                        _textInput = gui.TextInput(_textInput, placeholder: "Enter text here...");
                    }

                    using (gui.Node().Width(200).Enter())
                    {
                        _passwordInput = gui.PasswordInput(_passwordInput, placeholder: "Password");
                    }
                }

                // Text Area
                using (gui.Node().Height(60).Enter())
                {
                    _textArea = gui.TextArea(_textArea, width: 420, height: 60, placeholder: "Enter multiline text...");
                }
            }

            // Dropdowns
            using (gui.Node().Height(100).Direction(Axis.Vertical).Gap(10).Enter())
            {
                using (gui.Node().Height(40).Direction(Axis.Horizontal).Gap(10).Enter())
                {
                    using (gui.Node().Width(200).Enter())
                    {
                        gui.Dropdown(DropdownOptions, ref _dropdown1, placeholder: "Choose an option...");
                    }

                    using (gui.Node().Width(200).Enter())
                    {
                        gui.Dropdown(DropdownOptions, ref _dropdown2,
                            selectedColor: Color.FromArgb(255, 76, 175, 80));
                    }
                }
            }
        }
    }

    private static void RenderTabControlsTab(Gui gui)
    {
        using (gui.Node().Direction(Axis.Vertical).Gap(15).Enter())
        {
            // Pill Tabs
            using (gui.Node().Height(80).Enter())
            {
                gui.PillTabs(ref _pillTab, tabs =>
                {
                    tabs.Tab("Overview");
                    tabs.Tab("Details");
                    tabs.Tab("History");
                }, activeTabColor: Color.FromArgb(255, 76, 175, 80));
            }

            // Vertical Tabs
            using (gui.Node().Height(120).Direction(Axis.Horizontal).Enter())
            {
                gui.VerticalTabs(ref _verticalTab, tabs =>
                {
                    tabs.Tab("Profile", () =>
                    {
                        gui.DrawText("User profile information and avatar settings",
                            size: 12, color: Color.FromArgb(255, 102, 102, 102));
                    });
                    tabs.Tab("Security", () =>
                    {
                        gui.DrawText("Password and two-factor authentication settings",
                            size: 12, color: Color.FromArgb(255, 102, 102, 102));
                    });
                    tabs.Tab("Notifications", () =>
                    {
                        gui.DrawText("Email and push notification preferences",
                            size: 12, color: Color.FromArgb(255, 102, 102, 102));
                    });
                });
            }
        }
    }

    private static void RenderCurrentValues(Gui gui)
    {
        using (gui.Node().Direction(Axis.Vertical).Gap(10).Enter())
        {
            gui.DrawBackgroundRect(Color.FromArgb(255, 248, 249, 250), radius: 8);

            using (gui.Node().Margin(15).Direction(Axis.Vertical).Gap(8).Enter())
            {
                gui.DrawText("Current Values", size: 16, color: Color.FromArgb(255, 51, 51, 51));

                gui.DrawText($"Checkbox 1: {_checkbox1}", size: 12, color: Color.FromArgb(255, 102, 102, 102));
                gui.DrawText($"Checkbox 2: {_checkbox2}", size: 12, color: Color.FromArgb(255, 102, 102, 102));
                gui.DrawText($"Toggle 1: {_toggle1}", size: 12, color: Color.FromArgb(255, 102, 102, 102));
                gui.DrawText($"Toggle 2: {_toggle2}", size: 12, color: Color.FromArgb(255, 102, 102, 102));
                gui.DrawText($"Text Input: \"{_textInput}\"", size: 12, color: Color.FromArgb(255, 102, 102, 102));
                gui.DrawText($"Password: {new string('*', _passwordInput.Length)}", size: 12,
                    color: Color.FromArgb(255, 102, 102, 102));
                gui.DrawText($"Text Area Lines: {_textArea.Split('\n').Length}", size: 12,
                    color: Color.FromArgb(255, 102, 102, 102));
                gui.DrawText($"Dropdown 1: {(_dropdown1 >= 0 ? DropdownOptions[_dropdown1] : "None")}", size: 12,
                    color: Color.FromArgb(255, 102, 102, 102));
                gui.DrawText($"Dropdown 2: {(_dropdown2 >= 0 ? DropdownOptions[_dropdown2] : "None")}", size: 12,
                    color: Color.FromArgb(255, 102, 102, 102));
                gui.DrawText($"Active Tab: {_activeTab}", size: 12, color: Color.FromArgb(255, 102, 102, 102));
                gui.DrawText($"Pill Tab: {_pillTab}", size: 12, color: Color.FromArgb(255, 102, 102, 102));
                gui.DrawText($"Vertical Tab: {_verticalTab}", size: 12, color: Color.FromArgb(255, 102, 102, 102));
            }
        }
    }
}
