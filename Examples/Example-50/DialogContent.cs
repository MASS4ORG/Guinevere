using Guinevere;

namespace Controls_01;

public abstract partial class Program
{
    static bool _fileBrowsersInitialized;
    static string _selectedFile = "No file selected";
    static string _selectedFolder = "No folder selected";

    static void DialogContent(Gui gui)
    {
        Section(gui, "File Browsers", () => FileBrowserExamples(gui));
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
            size: 12, color: Token(gui, "text-dim"));

        using (gui.Node().ExpandWidth().Direction(Axis.Vertical).Gap(12).Enter())
        {
            FileBrowserPanel(gui, "Open a file", _selectedFile, OpenFileState, OpenFileBrowser);
            FileBrowserPanel(gui, "Select a folder", _selectedFolder, SelectFolderState, OpenFolderBrowser);
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

    static void FileBrowserPanel(Gui gui, string title, string result, FileDialogState state, Action reopen)
    {
        using (gui.Node().Width(520).Height(500).Padding(8).Direction(Axis.Vertical).Gap(6).Enter())
        {
            gui.DrawText(title, size: 14, color: Token(gui, "text"));
            gui.DrawText(result, size: 11, color: Token(gui, "text-dim"), wrapWidth: 500);

            if (state.IsOpen) gui.FileBrowser(state, width: 504, height: 450, fontSize: 12);
            else if (gui.Button("Browse again", width: 120, height: 28, fontSize: 12)) reopen();
        }
    }
}
