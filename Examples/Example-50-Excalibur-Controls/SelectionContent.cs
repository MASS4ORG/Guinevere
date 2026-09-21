using Guinevere;

namespace Controls_01;

public abstract partial class Program
{
    static int _dropdown1 = -1;
    static int _dropdown2 = 1;
    static int _radioChoice = 1;
    static readonly string[] DropdownOptions = ["Option 1", "Option 2", "Option 3", "Option 4", "Option 5"];
    static readonly FileDialogState OpenFileState = new();
    static readonly FileDialogState SelectFolderState = new();

    static bool _fileBrowsersInitialized;
    static string _selectedFile = "No file selected";
    static string _selectedFolder = "No folder selected";

    static void SelectionContent(Gui gui)
    {
        gui.SetTextColor(Color.Green);
        Section(gui, "Checkboxes", () => CheckboxRow(gui));
        Section(gui, "Toggles", () => ToggleRow(gui));
        Section(gui, "Radio Buttons", () => RadioButtonRow(gui));
        Section(gui, "Dropdowns", () => DropdownRow(gui));
        Section(gui, "File Browsers", () => FileBrowserExamples(gui));
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
            gui.Toggle(ref _toggle2, "High contrast",
                onColor: Color.FromArgb(255, 156, 39, 176),
                offColor: Color.FromArgb(255, 158, 158, 158));
            gui.Toggle(ref _toggle1, "Disabled toggle", enabled: false);
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
            gui.RadioButton(ref _radioChoice, 3, "Disabled radio", enabled: false);
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
            gui.Dropdown(DropdownOptions, ref _dropdown1, placeholder: "Disabled dropdown",
                enabled: false);
        }
    }

    static void FileBrowserExamples(Gui gui)
    {
        if (!_fileBrowsersInitialized)
        {
            _fileBrowsersInitialized = true;
            OpenFileBrowser();
            OpenFolderBrowser();
        }

        gui.DrawText("The same asynchronous control can select files or folders and can be embedded in any panel.",
            size: 12, color: gui.Controls.TextDim);

        using (gui.Node().ExpandWidth().Margin(0, 8, 0, 0).Direction(Axis.Horizontal).Gap(12).Enter())
        {
            FileBrowserPanel(gui, "Open a file", _selectedFile, OpenFileState, OpenFileBrowser);
            FileBrowserPanel(gui, "Select a folder", _selectedFolder, SelectFolderState, OpenFolderBrowser);
        }
    }

    static void FileBrowserPanel(Gui gui, string title, string result, FileDialogState state, Action reopen)
    {
        using (gui.Node().Width(520).Height(500).Padding(8).Direction(Axis.Vertical).Gap(6).Enter())
        {
            gui.DrawText(title, size: 14, color: gui.Controls.Text);
            gui.DrawText(result, size: 11, color: gui.Controls.TextDim, wrapWidth: 500);

            if (state.IsOpen) gui.FileBrowser(state, width: 504, height: 450, fontSize: 12);
            else if (gui.Button("Browse again", width: 120, height: 28, fontSize: 12)) reopen();
        }
    }

    static void OpenFileBrowser() => OpenFileState.Open(new FileDialogRequest
    {
        Mode = FileDialogMode.OpenFile,
        Title = "Open a file",
        StartPath = Environment.CurrentDirectory,
        Filters =
        [
            FileDialogFilter.All,
            FileDialogFilter.Of("C# files", ".cs"),
            FileDialogFilter.Of("Images", ".png", ".jpg", ".jpeg")
        ],
        OnClosed = result => _selectedFile = result.WasCancelled
            ? "File selection cancelled"
            : $"Selected: {result.Path}",
    });

    static void OpenFolderBrowser() => SelectFolderState.Open(new FileDialogRequest
    {
        Mode = FileDialogMode.SelectFolder,
        Title = "Select a folder",
        StartPath = Environment.CurrentDirectory,
        OnClosed = result => _selectedFolder = result.WasCancelled
            ? "Folder selection cancelled"
            : $"Selected: {result.Path}",
    });

}
