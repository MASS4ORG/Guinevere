using Guinevere;

namespace Controls_01;

public abstract partial class Program
{
    private static int _dropdown1 = -1;
    private static int _dropdown2 = 1;
    private static readonly string[] DropdownOptions = ["Option 1", "Option 2", "Option 3", "Option 4", "Option 5"];

    private static void SelectionContent(Gui gui)
    {
        Section(gui, "Checkboxes", () => CheckboxRow(gui));
        Section(gui, "Toggles", () => ToggleRow(gui));
        Section(gui, "Dropdowns", () => DropdownRow(gui));
    }

    private static void CheckboxRow(Gui gui)
    {
        using (gui.Node().Height(30).Direction(Axis.Horizontal).Gap(20).Enter())
        {
            gui.Checkbox(ref _checkbox1, "Enable notifications");
            gui.Checkbox(ref _checkbox2, "Auto-save documents");
        }
    }

    private static void ToggleRow(Gui gui)
    {
        using (gui.Node().Height(30).Direction(Axis.Horizontal).Gap(20).Enter())
        {
            gui.Toggle(ref _toggle1, "Dark mode");
            gui.Toggle(ref _toggle2, "High contrast",
                onColor: Color.FromArgb(255, 156, 39, 176),
                offColor: Color.FromArgb(255, 158, 158, 158));
        }
    }

    private static void DropdownRow(Gui gui)
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
}
