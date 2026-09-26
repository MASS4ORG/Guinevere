using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

public sealed class FileDialogRenderTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), $"guinevere-dialog-{Guid.NewGuid():N}");

    public FileDialogRenderTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "sample.txt"), "sample");
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData(FileDialogMode.OpenFile)]
    [InlineData(FileDialogMode.SaveFile)]
    [InlineData(FileDialogMode.SelectFolder)]
    public async Task OpenDialogBuildsAUsableFrame(FileDialogMode mode)
    {
        using var harness = new FrameHarness(1000, 700);
        var state = new FileDialogState();
        await state.OpenAsync(new FileDialogRequest { Mode = mode, StartPath = _root },
            TestContext.Current.CancellationToken);

        harness.Frame(gui => gui.FileDialog(state, width: 840, height: 460));

        Assert.True(state.IsOpen);
        Assert.NotEmpty(harness.Gui.RootNode!.Children);
        Assert.Null(state.Message);
    }

    [Fact]
    public async Task EditingThePathKeepsTheDialogOpen()
    {
        using var harness = new FrameHarness(1000, 700);
        var state = new FileDialogState();
        await state.OpenAsync(new FileDialogRequest { Mode = FileDialogMode.OpenFile, StartPath = _root },
            TestContext.Current.CancellationToken);
        state.EditingPath = _root;

        harness.Frame(gui => gui.FileDialog(state, width: 840, height: 460));

        Assert.True(state.IsOpen);
        Assert.Equal(_root, state.EditingPath);
    }
}
