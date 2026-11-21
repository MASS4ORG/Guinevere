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

        harness.Frame(gui => gui.FileDialog(state).Width(840).Height(544));

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

        harness.Frame(gui => gui.FileDialog(state).Width(840).Height(544));

        Assert.True(state.IsOpen);
        Assert.Equal(_root, state.EditingPath);
    }

    [Fact]
    public async Task PickingASecondFilterRefiltersTheListing()
    {
        using var harness = new FrameHarness(1000, 700);
        var state = new FileDialogState();
        var request = new FileDialogRequest
        {
            Mode = FileDialogMode.SaveFile,
            StartPath = _root,
            Filters = [FileDialogFilter.Of("Scenes", ".scene"), FileDialogFilter.Of("All", ".*")],
        };
        await state.OpenAsync(request, TestContext.Current.CancellationToken);

        void Draw(Gui gui) => gui.FileDialog(state).Width(840).Height(544);
        harness.Frame(Draw);
        harness.Frame(Draw);
        harness.Frame(Draw);

        harness.Click(Draw, FrameHarness.Center(FindNode(harness.Gui, "/button")!));
        harness.Frame(Draw);
        harness.Click(Draw, FrameHarness.Center(FindNode(harness.Gui, "/list/1")!));
        harness.Frame(Draw);
        harness.Frame(Draw);

        Assert.Equal(1, state.FilterIndex);
        Assert.Equal(request.Filters[1], state.Browser.Filter);
    }

    [Fact]
    public async Task ASingleFilterOffersNoDropdown()
    {
        using var harness = new FrameHarness(1000, 700);
        var state = new FileDialogState();
        await state.OpenAsync(
            new FileDialogRequest { Mode = FileDialogMode.OpenFile, StartPath = _root,
                Filters = [FileDialogFilter.Of("Scenes", ".scene")] },
            TestContext.Current.CancellationToken);

        void Draw(Gui gui) => gui.FileDialog(state).Width(840).Height(544);
        harness.Frame(Draw);
        harness.Frame(Draw);

        Assert.Null(FindNode(harness.Gui, "/button"));
    }

    [Fact]
    public async Task EnterInThePathBoxNavigatesInsteadOfAcceptingTheDialog()
    {
        var fileSystem = new InMemoryFileDialogFileSystem().AddFile("docs", "a.txt").AddFile("root.txt");
        using var rig = await FileDialogRig.OpenAsync(fileSystem);
        rig.State.Selected = rig.State.Browser.Entries.Single(static entry => entry.Name == "root.txt");
        rig.SetPathText(fileSystem.P("docs"));

        rig.Press(KeyboardKey.Enter);

        Assert.True(rig.State.IsOpen);
        Assert.Null(rig.Completed);
        Assert.Equal(fileSystem.P("docs"), rig.State.Browser.CurrentPath);
        Assert.Null(rig.State.EditingPath);
    }

    [Fact]
    public async Task EnterInTheSearchBoxDoesNotAcceptTheDialog()
    {
        var fileSystem = new InMemoryFileDialogFileSystem().AddFile("root.txt");
        using var rig = await FileDialogRig.OpenAsync(fileSystem);
        rig.State.Selected = rig.State.Browser.Entries.Single();
        rig.Click(rig.Find("/search")!);

        rig.Press(KeyboardKey.Enter);

        Assert.True(rig.State.IsOpen);
        Assert.Null(rig.Completed);
    }

    [Fact]
    public async Task EnterInTheListingStillAcceptsTheSelection()
    {
        var fileSystem = new InMemoryFileDialogFileSystem().AddFile("root.txt");
        using var rig = await FileDialogRig.OpenAsync(fileSystem);
        rig.Click(rig.Find("/row/" + fileSystem.P("root.txt"))!);

        rig.Press(KeyboardKey.Enter);

        Assert.Equal(fileSystem.P("root.txt"), rig.Completed);
    }

    [Fact]
    public async Task EnterAfterSearchingThenClickingARowAcceptsTheSelection()
    {
        var fileSystem = new InMemoryFileDialogFileSystem().AddFile("root.txt");
        using var rig = await FileDialogRig.OpenAsync(fileSystem);
        rig.Click(rig.Find("/search")!);
        rig.Click(rig.Find("/row/" + fileSystem.P("root.txt"))!);

        rig.Press(KeyboardKey.Enter);

        Assert.Equal(fileSystem.P("root.txt"), rig.Completed);
    }

    [Fact]
    public async Task EscapeWhileEditingThePathCancelsTheEditAndKeepsTheDialog()
    {
        var fileSystem = new InMemoryFileDialogFileSystem().AddFile("root.txt");
        using var rig = await FileDialogRig.OpenAsync(fileSystem);
        rig.SetPathText(fileSystem.P("typed"));

        rig.Press(KeyboardKey.Escape);

        Assert.True(rig.State.IsOpen);
        Assert.Null(rig.State.EditingPath);
        Assert.Equal(fileSystem.Root, rig.State.Browser.CurrentPath);

        rig.Press(KeyboardKey.Escape);

        Assert.False(rig.State.IsOpen);
        Assert.True(rig.Closed);
        Assert.Null(rig.Completed);
    }

    static LayoutNode? FindNode(Gui gui, string suffix) =>
        Walk(gui.RootNode!).FirstOrDefault(node => node.Id.EndsWith(suffix, StringComparison.Ordinal));

    static IEnumerable<LayoutNode> Walk(LayoutNode node) => node.Children.SelectMany(Walk).Prepend(node);
}
