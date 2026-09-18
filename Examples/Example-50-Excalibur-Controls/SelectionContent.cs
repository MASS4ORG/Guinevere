using Guinevere;

namespace Controls_01;

public abstract partial class Program
{
    static int _dropdown1 = -1;
    static int _dropdown2 = 1;
    static int _radioChoice = 1;
    static readonly string[] DropdownOptions = ["Option 1", "Option 2", "Option 3", "Option 4", "Option 5"];

    static void SelectionContent(Gui gui)
    {
        gui.SetTextColor(Color.Green);
        Section(gui, "Checkboxes", () => CheckboxRow(gui));
        Section(gui, "Toggles", () => ToggleRow(gui));
        Section(gui, "Radio Buttons", () => RadioButtonRow(gui));
        Section(gui, "Dropdowns", () => DropdownRow(gui));
        Section(gui, "Disabled Controls", () => DisabledSelectionRow(gui));
    }

    static void CheckboxRow(Gui gui)
    {
        using (gui.Node().Height(30).Direction(Axis.Horizontal).Gap(20).Enter())
        {
            gui.Checkbox(ref _checkbox1, "Enable notifications");
            gui.Checkbox(ref _checkbox2, "Auto-save documents");
        }
    }

    static void ToggleRow(Gui gui)
    {
        using (gui.Node().Height(30).Direction(Axis.Horizontal).Gap(20).Enter())
        {
            gui.Toggle(ref _toggle1, "Dark mode");
            gui.Toggle(ref _toggle2, "High contrast",
                onColor: Color.FromArgb(255, 156, 39, 176),
                offColor: Color.FromArgb(255, 158, 158, 158));
        }
    }

    static void RadioButtonRow(Gui gui)
    {
        using (gui.Node().Height(120).Direction(Axis.Horizontal).Gap(40).Enter())
        {
            gui.DrawText("Pick a theme:", 14, Color.FromArgb(255, 51, 51, 51));

            gui.RadioGroup(ref _radioChoice,
                [(0, "Light"), (1, "Dark"), (2, "System")],
                selectedColor: Color.FromArgb(255, 76, 175, 80));
        }
    }

    static void DropdownRow(Gui gui)
    {
        using (gui.Node().Height(40).Direction(Axis.Horizontal).Gap(10).Enter())
        {
            using (gui.Node().Width(200).Enter())
            {
                gui.Dropdown(DropdownOptions, ref _dropdown1, placeholder: "Choose an option...");
            }

            using (gui.Node().Width(200).Enter())
            {
                gui.Dropdown(DropdownOptions, ref _dropdown2, selectedColor: Color.FromArgb(255, 76, 175, 80));
            }
        }
    }

    static void DisabledSelectionRow(Gui gui)
    {
        using (gui.Node().Height(30).Direction(Axis.Horizontal).Gap(20).Enter())
        {
            gui.Checkbox(ref _checkbox1, "Disabled checkbox", enabled: false);
            gui.Toggle(ref _toggle1, "Disabled toggle", enabled: false);
            gui.RadioButton(ref _radioChoice, 3, "Disabled radio", enabled: false);
        }

        using (gui.Node().Height(40).Margin(0, 8, 0, 0).Direction(Axis.Horizontal).Gap(10).Enter())
        {
            using (gui.Node().Width(200).Enter())
            {
                gui.Dropdown(DropdownOptions, ref _dropdown1, placeholder: "Disabled dropdown",
                    enabled: false);
            }
        }
    }
}
