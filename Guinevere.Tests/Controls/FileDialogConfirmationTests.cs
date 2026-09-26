namespace Guinevere.Tests.Controls;

public sealed class FileDialogConfirmationTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), $"guinevere-confirm-{Guid.NewGuid():N}");

    public FileDialogConfirmationTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "existing.txt"), "existing");
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    async Task<FileDialogState> Open(FileDialogMode mode, Action<string?> complete, Func<string, string?>? validate = null)
    {
        var state = new FileDialogState();
        await state.OpenAsync(new FileDialogRequest
        {
            Mode = mode,
            StartPath = _root,
            OnComplete = complete,
            Validate = validate,
        });
        return state;
    }

    [Fact]
    public async Task OpenFileRequiresASelectionAndRejectsADeletedFile()
    {
        string? completed = null;
        var state = await Open(FileDialogMode.OpenFile, path => completed = path);

        await ControlsExtensions.ConfirmAsync(state);
        Assert.Equal("Select a file.", state.Message);
        Assert.True(state.IsOpen);

        state.Selected = Assert.Single(state.Browser.Entries, entry => entry.Name == "existing.txt");
        File.Delete(Path.Combine(_root, "existing.txt"));
        await ControlsExtensions.ConfirmAsync(state);
        Assert.Equal("That file no longer exists.", state.Message);
        Assert.Null(completed);
    }

    [Fact]
    public async Task OpenFileCompletesWithTheSelectedPath()
    {
        string? completed = null;
        var state = await Open(FileDialogMode.OpenFile, path => completed = path);
        state.Selected = Assert.Single(state.Browser.Entries, entry => entry.Name == "existing.txt");

        await ControlsExtensions.ConfirmAsync(state);

        Assert.Equal(Path.Combine(_root, "existing.txt"), completed);
        Assert.False(state.IsOpen);
    }

    [Fact]
    public async Task SaveFileRequiresSecondConfirmationBeforeOverwrite()
    {
        string? completed = null;
        var state = await Open(FileDialogMode.SaveFile, path => completed = path);
        state.Name = "existing.txt";

        await ControlsExtensions.ConfirmAsync(state);
        Assert.True(state.IsOpen);
        Assert.Equal(Path.Combine(_root, "existing.txt"), state.PendingOverwrite);
        Assert.Null(completed);

        await ControlsExtensions.ConfirmAsync(state);
        Assert.Equal(Path.Combine(_root, "existing.txt"), completed);
        Assert.False(state.IsOpen);
    }

    [Fact]
    public async Task ValidationCanRefuseAChoiceWithoutClosing()
    {
        string? completed = null;
        var state = await Open(FileDialogMode.SaveFile, path => completed = path,
            _ => "Only PNG files are allowed.");
        state.Name = "new.txt";

        await ControlsExtensions.ConfirmAsync(state);

        Assert.Equal("Only PNG files are allowed.", state.Message);
        Assert.True(state.IsOpen);
        Assert.Null(completed);
    }

    [Fact]
    public async Task CreateFolderCreatesTheDirectoryBeforeCompleting()
    {
        string? completed = null;
        var state = await Open(FileDialogMode.CreateFolder, path => completed = path);
        state.Name = "created";

        await ControlsExtensions.ConfirmAsync(state);

        Assert.Equal(Path.Combine(_root, "created"), completed);
        Assert.True(Directory.Exists(completed));
    }
}
