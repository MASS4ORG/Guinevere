using Guinevere;
using JetBrains.Annotations;

namespace Controls01;

public abstract partial class Program
{
    static int _dropdown1 = -1;
    static int _dropdown2 = 1;
    static int _radioChoice = 1;
    static readonly string[] DropdownOptions = ["Option 1", "Option 2", "Option 3", "Option 4", "Option 5"];
    static readonly FileDialogState OpenFileState = new();
    static readonly FileDialogState SelectFolderState = new();
    static IReadOnlyList<string> _multiSelection = ["Option 2"];
    static DemoPermissions _permissions = DemoPermissions.Read;
    static DemoMode _enumMode;

    [Flags, PublicAPI]
    enum DemoPermissions
    {
        None = 0,
        Read = 1,
        Write = 2,
        Execute = 4,
        All = Read | Write | Execute
    }

    [PublicAPI]
    enum DemoMode
    {
        Preview,
        Edit,
        Play
    }


    static Color _fieldColor = Color.ParseHex("#a855f7");
    static Color _fieldColor2 = Color.ParseHex("#22c55e80");
    static Hsva _compact = new(150f, 0.7f, 0.6f);
    static readonly ColorPickerOptions WheelPicker = new()
    {
        Shape = ColorPickerShapes.Wheel
    };
    static readonly ColorPickerOptions CompactPicker = new()
    {
        Width = 180,
        Body = (Gui gui, ref Hsva value, Color? _) =>
        {
            var changed = gui.ColorArea(ref value, width: 0, height: 150);
            changed |= gui.ColorStrip(ref value, HsvChannel.Hue, length: 0);
            changed |= gui.ColorStrip(ref value, HsvChannel.Alpha, length: 0);
            return changed;
        }
    };

    // Choice controls take no colors: this class restyles a toggle's track from a sheet over the default one.
    static readonly StyleSheet SelectionSheet = StyleSheet.Parse(
        """
        toggle.purple > track { background-color = #9e9e9e; :checked { background-color = #9c27b0; } }
        """);

    static void SelectionContent(Gui gui)
    {
        using (gui.Node().Expand().Padding(10).Enter())
        {
            gui.ScrollY();
            if (!gui.StyleSheets.Contains(SelectionSheet))
            {
                gui.StyleSheets.Add(SelectionSheet);
            }

            Section(gui, "Checkboxes", () => CheckboxRow(gui));
            Section(gui, "Toggles", () => ToggleRow(gui));
            Section(gui, "Radio Buttons", () => RadioButtonRow(gui));
            Section(gui, "Dropdowns", () => DropdownRow(gui));
            Section(gui, "Multiple selection and enums", () => MultiDropdownRow(gui));
            Section(gui, "Color fields", () =>
            {
                ColorFieldRow(gui, "Accent", ref _fieldColor, null);
                ColorFieldRow(gui, "Overlay", ref _fieldColor2, WheelPicker);

                gui.ColorPicker(ref _compact, CompactPicker);
                gui.ColorPicker(ref _compact, WheelPicker);
            });
        }
    }

    static void CheckboxRow(Gui gui)
    {
        using (gui.Node().Height(30).Direction(Axis.Horizontal).Gap(20).Enter())
        {
            gui.Checkbox(ref _checkbox1, "Enable notifications");
            gui.Checkbox(ref _checkbox2, "Auto-save documents");
            gui.Checkbox(ref _checkbox1, "Disabled checkbox", enabled: false);
        }
    }

    static void ToggleRow(Gui gui)
    {
        using (gui.Node().Height(30).Direction(Axis.Horizontal).Gap(20).Enter())
        {
            gui.Toggle(ref _toggle1, "Dark mode");
            gui.Toggle(ref _toggle2, "High contrast", classes: ["purple"]);
            gui.Toggle(ref _toggle1, "Disabled toggle", enabled: false);
        }
    }

    static void RadioButtonRow(Gui gui)
    {
        using (gui.Node().Height(120).Direction(Axis.Horizontal).Gap(40).Enter())
        {
            gui.DrawText("DockTheme:", 14, Token(gui, "text"));

            gui.RadioGroup(ref _radioChoice,
                [(0, "Light"), (1, "Dark"), (2, "Mono Light"), (3, "Mono Dark")]);
            gui.RadioButton(ref _radioChoice, 4, "Disabled radio", enabled: false);
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
                // A token set on a node themes its whole subtree, including the dropdown's option list.
                gui.SetStyleToken("selected", Color.FromArgb(255, 76, 175, 80));
                gui.Dropdown(DropdownOptions, ref _dropdown2);
            }

            gui.Dropdown(DropdownOptions, ref _dropdown1, placeholder: "Disabled dropdown",
                enabled: false);
        }
    }

    static void MultiDropdownRow(Gui gui)
    {
        using (gui.Node().Height(40).Direction(Axis.Horizontal).Gap(10).Enter())
        {
            var selection = gui.MultiDropdown(DropdownOptions, _multiSelection, width: 240, chips: true);
            if (selection.Changed)
            {
                _multiSelection = selection.Selected;
            }

            gui.EnumDropdown(ref _permissions, width: 200);
            gui.EnumDropdown(ref _enumMode, EnumPresentation.Paging, width: 200);
        }

        using (gui.Node().Height(40).Direction(Axis.Horizontal).Enter())
        {
            gui.EnumDropdown(ref _permissions, EnumPresentation.ToggleButtons, width: 480);
        }
    }

    static void ColorFieldRow(Gui gui, string label, ref Color color, ColorPickerOptions? options)
    {
        using (gui.Node().ExpandWidth().Height(30).Direction(Axis.Horizontal).Gap(8).AlignContent(0f, 0.5f).Enter())
        {
            using (gui.Node().Width(70).Height(24).Enter())
            {
                gui.DrawText(label, 14, Token(gui, "text"), centerInRect: false);
            }

            gui.ColorField(ref color, options);
        }
    }
}
