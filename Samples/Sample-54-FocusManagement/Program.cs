using Guinevere;
using Guinevere.OpenGL.SilkNET;

namespace Sample_54_FocusManagement;

public abstract class Program
{
    // Form controls
    private static string _firstName = "John";
    private static string _lastName = "Doe";
    private static string _email = "john.doe@example.com";
    private static string _phone = "555-0123";
    private static string _address = "123 Main St\nAnytown, USA 12345";
    private static string _notes = "Enter additional notes here...";

    // Preferences
    private static bool _emailNotifications = true;
    private static bool _smsNotifications = false;
    private static bool _pushNotifications = true;
    private static bool _darkMode = false;
    private static bool _autoSave = true;

    // Settings
    private static bool _enableLogging = false;
    private static bool _debugMode = false;
    private static string _logLevel = "Info";
    private static string _theme = "Default";

    // Panel states
    private static bool _showAdvanced = false;
    private static int _activeTab = 0;

    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui, 1200, 800, "Focus Management Demo - Use Tab/Shift+Tab to navigate");

        win.RunGui(() =>
        {
            // Background
            gui.DrawRect(gui.ScreenRect, Color.FromArgb(255, 240, 242, 247));

            using (gui.Node().Expand().Margin(20).Direction(Axis.Vertical).Gap(15).Enter())
            {
                // Header
                RenderHeader(gui);

                // Main content area
                using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(20).Enter())
                {
                    // Left panel - User Form
                    using (gui.Node().Width(400).Enter())
                    {
                        RenderUserForm(gui);
                    }

                    // Middle panel - Preferences
                    using (gui.Node().Width(350).Enter())
                    {
                        RenderPreferences(gui);
                    }

                    // Right panel - Focus Info & Advanced Settings
                    using (gui.Node().Expand().Enter())
                    {
                        RenderFocusInfo(gui);
                        gui.Node().Height(20).Enter().Dispose(); // Spacer
                        RenderAdvancedSettings(gui);
                    }
                }

                // Footer with instructions
                RenderFooter(gui);
            }
        });
    }

    private static void RenderHeader(Gui gui)
    {
        using (gui.Node().Height(60).Enter())
        {
            gui.DrawBackgroundRect(Color.White, radius: 10);

            using (gui.Node().Margin(20, 20, 20, 20).Direction(Axis.Vertical).Enter())
            {
                gui.DrawText("Focus Management Demo", size: 24, color: Color.FromArgb(255, 51, 51, 51));
                gui.DrawText("Demonstrate Tab navigation, cascaded focus, and focus visual indicators",
                    size: 14, color: Color.FromArgb(255, 102, 102, 102));
            }
        }
    }

    private static void RenderUserForm(Gui gui)
    {
        using (gui.Node().Direction(Axis.Vertical).Gap(15).Enter())
        {
            gui.DrawBackgroundRect(Color.White, radius: 10);

            using (gui.Node().Margin(20).Direction(Axis.Vertical).Gap(12).Enter())
            {
                // Panel title with focus indicator
                var panelHasFocus = gui.HasFocusWithin();
                var titleColor = panelHasFocus ? Color.FromArgb(255, 100, 149, 237) : Color.FromArgb(255, 51, 51, 51);
                var titleText = panelHasFocus ? "👥 User Information (FOCUSED)" : "👥 User Information";

                gui.DrawText(titleText, size: 18, color: titleColor);

                // Focus panel registration (for cascaded focus)
                gui.RegisterFocusable(canReceiveFocus: false, isInteractable: false);

                // Name fields
                using (gui.Node().Height(40).Direction(Axis.Horizontal).Gap(10).Enter())
                {
                    using (gui.Node().Width(180).Direction(Axis.Vertical).Gap(5).Enter())
                    {
                        gui.DrawText("First Name:", size: 12, color: Color.FromArgb(255, 102, 102, 102));
                        _firstName = gui.TextInput(_firstName, placeholder: "Enter first name");
                    }

                    using (gui.Node().Width(180).Direction(Axis.Vertical).Gap(5).Enter())
                    {
                        gui.DrawText("Last Name:", size: 12, color: Color.FromArgb(255, 102, 102, 102));
                        _lastName = gui.TextInput(_lastName, placeholder: "Enter last name");
                    }
                }

                // Email
                using (gui.Node().Height(40).Direction(Axis.Vertical).Gap(5).Enter())
                {
                    gui.DrawText("Email:", size: 12, color: Color.FromArgb(255, 102, 102, 102));
                    _email = gui.TextInput(_email, placeholder: "Enter email address");
                }

                // Phone
                using (gui.Node().Height(40).Direction(Axis.Vertical).Gap(5).Enter())
                {
                    gui.DrawText("Phone:", size: 12, color: Color.FromArgb(255, 102, 102, 102));
                    _phone = gui.TextInput(_phone, placeholder: "Enter phone number");
                }

                // Address (text area)
                using (gui.Node().Height(80).Direction(Axis.Vertical).Gap(5).Enter())
                {
                    gui.DrawText("Address:", size: 12, color: Color.FromArgb(255, 102, 102, 102));
                    _address = gui.TextArea(_address, width: 360, height: 60, placeholder: "Enter address");
                }

                // Notes (text area)
                using (gui.Node().Height(80).Direction(Axis.Vertical).Gap(5).Enter())
                {
                    gui.DrawText("Notes:", size: 12, color: Color.FromArgb(255, 102, 102, 102));
                    _notes = gui.TextArea(_notes, width: 360, height: 60, placeholder: "Additional notes");
                }
            }
        }
    }

    private static void RenderPreferences(Gui gui)
    {
        using (gui.Node().Direction(Axis.Vertical).Gap(15).Enter())
        {
            gui.DrawBackgroundRect(Color.White, radius: 10);

            using (gui.Node().Margin(20).Direction(Axis.Vertical).Gap(12).Enter())
            {
                // Panel title with focus indicator
                var panelHasFocus = gui.HasFocusWithin();
                var titleColor = panelHasFocus ? Color.FromArgb(255, 100, 149, 237) : Color.FromArgb(255, 51, 51, 51);
                var titleText = panelHasFocus ? "⚙️ Preferences (FOCUSED)" : "⚙️ Preferences";

                gui.DrawText(titleText, size: 18, color: titleColor);

                // Focus panel registration
                gui.RegisterFocusable(canReceiveFocus: false, isInteractable: false);

                // Notification settings
                gui.DrawText("Notifications:", size: 14, color: Color.FromArgb(255, 85, 85, 85));

                using (gui.Node().Direction(Axis.Vertical).Gap(8).MarginLeft(10).Enter())
                {
                    gui.Checkbox(ref _emailNotifications, "Email notifications");
                    gui.Checkbox(ref _smsNotifications, "SMS notifications");
                    gui.Checkbox(ref _pushNotifications, "Push notifications");
                }

                gui.Node().Height(15).Enter().Dispose(); // Spacer

                // General settings
                gui.DrawText("General:", size: 14, color: Color.FromArgb(255, 85, 85, 85));

                using (gui.Node().Direction(Axis.Vertical).Gap(8).MarginLeft(10).Enter())
                {
                    gui.Toggle(ref _darkMode, "Dark mode",
                        onColor: Color.FromArgb(255, 76, 175, 80));
                    gui.Toggle(ref _autoSave, "Auto-save documents",
                        onColor: Color.FromArgb(255, 100, 149, 237));
                }
            }
        }
    }

    private static void RenderFocusInfo(Gui gui)
    {
        using (gui.Node().Height(200).Direction(Axis.Vertical).Gap(10).Enter())
        {
            gui.DrawBackgroundRect(Color.FromArgb(255, 248, 249, 250), radius: 8);

            using (gui.Node().Margin(15).Direction(Axis.Vertical).Gap(8).Enter())
            {
                gui.DrawText("🎯 Focus Status", size: 16, color: Color.FromArgb(255, 51, 51, 51));

                var currentFocus = gui.Focus.CurrentFocusedId ?? "None";
                var focusText = currentFocus.Length > 30 ? $"{currentFocus[..27]}..." : currentFocus;

                gui.DrawText($"Current Focus: {focusText}", size: 12,
                    color: gui.Focus.HasAnyFocus ? Color.FromArgb(255, 100, 149, 237) : Color.FromArgb(255, 102, 102, 102));

                gui.DrawText($"Focus Changed: {(gui.Focus.FocusChangedThisFrame ? "Yes" : "No")}",
                    size: 12, color: Color.FromArgb(255, 102, 102, 102));

                gui.DrawText($"Has Focus: {(gui.Focus.HasAnyFocus ? "Yes" : "No")}",
                    size: 12, color: Color.FromArgb(255, 102, 102, 102));

                gui.Node().Height(10).Enter().Dispose(); // Spacer

                gui.DrawText("📋 Instructions:", size: 14, color: Color.FromArgb(255, 51, 51, 51));
                gui.DrawText("• Press Tab to move forward", size: 11, color: Color.FromArgb(255, 102, 102, 102));
                gui.DrawText("• Press Shift+Tab to move backward", size: 11, color: Color.FromArgb(255, 102, 102, 102));
                gui.DrawText("• Click controls to focus them", size: 11, color: Color.FromArgb(255, 102, 102, 102));
                gui.DrawText("• Space bar activates focused toggles/checkboxes", size: 11, color: Color.FromArgb(255, 102, 102, 102));
                gui.DrawText("• Panel titles show focus status", size: 11, color: Color.FromArgb(255, 102, 102, 102));
            }
        }
    }

    private static void RenderAdvancedSettings(Gui gui)
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(10).Enter())
        {
            gui.DrawBackgroundRect(Color.White, radius: 8);

            using (gui.Node().Margin(15).Direction(Axis.Vertical).Gap(10).Enter())
            {
                // Collapsible section header
                using (gui.Node().Height(30).Direction(Axis.Horizontal).Gap(8).Enter())
                {
                    // Register the toggle button as focusable
                    gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);

                    var interactable = gui.GetInteractable();
                    if (interactable.OnClick())
                    {
                        gui.RequestFocus(FocusReason.Mouse);
                        _showAdvanced = !_showAdvanced;
                    }

                    // Handle keyboard interaction for focused toggle
                    if (gui.HasFocus() && gui.Input.IsKeyPressed(KeyboardKey.Space))
                    {
                        _showAdvanced = !_showAdvanced;
                    }

                    var arrow = _showAdvanced ? "▼" : "▶";
                    var textColor = gui.HasFocus() ? Color.FromArgb(255, 100, 149, 237) : Color.FromArgb(255, 51, 51, 51);

                    // Draw focus indicator
                    if (gui.HasFocus())
                    {
                        var focusRect = gui.CurrentNode.Rect;
                        gui.DrawRectBorder(focusRect, Color.FromArgb(255, 100, 149, 237), 2f, 4);
                    }

                    gui.DrawText($"{arrow} Advanced Settings", size: 16, color: textColor);
                }

                if (_showAdvanced)
                {
                    using (gui.Node().Direction(Axis.Vertical).Gap(10).MarginLeft(20).Enter())
                    {
                        // Panel title with focus indicator
                        var panelHasFocus = gui.HasFocusWithin();
                        if (panelHasFocus)
                        {
                            gui.DrawText("🔧 Advanced (FOCUSED)", size: 14, color: Color.FromArgb(255, 100, 149, 237));
                        }
                        else
                        {
                            gui.DrawText("🔧 Advanced", size: 14, color: Color.FromArgb(255, 85, 85, 85));
                        }

                        // Register advanced panel for cascaded focus
                        gui.RegisterFocusable(canReceiveFocus: false, isInteractable: false);

                        gui.Checkbox(ref _enableLogging, "Enable detailed logging");
                        gui.Checkbox(ref _debugMode, "Debug mode");

                        // Custom text inputs for advanced settings
                        using (gui.Node().Height(40).Direction(Axis.Vertical).Gap(5).Enter())
                        {
                            gui.DrawText("Log Level:", size: 12, color: Color.FromArgb(255, 102, 102, 102));
                            _logLevel = gui.TextInput(_logLevel, placeholder: "Debug, Info, Warning, Error");
                        }

                        using (gui.Node().Height(40).Direction(Axis.Vertical).Gap(5).Enter())
                        {
                            gui.DrawText("Theme:", size: 12, color: Color.FromArgb(255, 102, 102, 102));
                            _theme = gui.TextInput(_theme, placeholder: "Default, Dark, Light, Custom");
                        }
                    }
                }
            }
        }
    }

    private static void RenderFooter(Gui gui)
    {
        using (gui.Node().Height(50).Enter())
        {
            gui.DrawBackgroundRect(Color.FromArgb(255, 248, 249, 250), radius: 8);

            using (gui.Node().Margin(15, 15, 15, 15).Direction(Axis.Horizontal).Enter())
            {
                gui.DrawText($"Frame: {gui.Time.Frames} | FPS: {gui.Time.SmoothFps:N1}",
                    size: 12, color: Color.FromArgb(255, 153, 153, 153));

                var statusText = gui.Focus.HasAnyFocus ? $"Active Control: {gui.Focus.CurrentFocusedId}" : "No active control";

                using (gui.Node().ExpandWidth().Enter())
                {
                    // Left side content - already placed above
                }

                using (gui.Node().Enter())
                {
                    gui.DrawText(statusText, size: 12, color: Color.FromArgb(255, 100, 149, 237));
                }
            }
        }
    }
}
