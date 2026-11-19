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

    static LayoutNode? FindNode(Gui gui, string suffix) =>
        Walk(gui.RootNode!).FirstOrDefault(node => node.Id.EndsWith(suffix, StringComparison.Ordinal));

    static IEnumerable<LayoutNode> Walk(LayoutNode node) => node.Children.SelectMany(Walk).Prepend(node);
}
