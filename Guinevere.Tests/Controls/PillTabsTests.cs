using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Pill tabs: switching, opt-in middle-click closing and the empty case.</summary>
public class PillTabsTests
{
    int _active;
    readonly List<(int Index, string Title)> _closed = [];
    readonly List<string> _contentBuilt = [];

    void Draw(Gui gui, bool closable = false, params string[] titles)
    {
        gui.PillTabs(ref _active, tabs =>
        {
            foreach (var title in titles) tabs.Tab(title, () => _contentBuilt.Add(title), closable: closable);
        }, id: "pills", onTabClosed: (index, title) => _closed.Add((index, title)));
    }

    static LayoutNode Tab(Gui gui, int index) => gui.RootNode!.Children[0].Children[0].Children[index];

    [Fact]
    public void ClickingATabActivatesItAndBuildsItsContent()
    {
        using var harness = new FrameHarness();
        void Frame(Gui gui) => Draw(gui, false, "A", "B", "C");

        harness.Frame(Frame);
        Assert.Contains("A", _contentBuilt);

        harness.Click(Frame, FrameHarness.Center(Tab(harness.Gui, 1)));
        harness.Frame(Frame);

        Assert.Equal(1, _active);
        Assert.Contains("B", _contentBuilt);
    }

    [Fact]
    public void MiddleClickClosesAClosableTab()
    {
        using var harness = new FrameHarness();
        void Frame(Gui gui) => Draw(gui, true, "A", "B");

        harness.Frame(Frame);
        harness.Click(Frame, FrameHarness.Center(Tab(harness.Gui, 0)), MouseButton.Middle);

        Assert.Equal([(0, "A")], _closed);
        Assert.Single(harness.Gui.RootNode!.Children[0].Children[0].Children);
    }

    [Fact]
    public void MiddleClickLeavesAFixedTabOpen()
    {
        using var harness = new FrameHarness();
        void Frame(Gui gui) => Draw(gui, false, "A", "B");

        harness.Frame(Frame);
        harness.Click(Frame, FrameHarness.Center(Tab(harness.Gui, 0)), MouseButton.Middle);

        Assert.Empty(_closed);
    }

    [Fact]
    public void NoTabsBuildNothing()
    {
        using var harness = new FrameHarness();
        _active = 3;

        harness.Frame(gui => Draw(gui));

        Assert.Equal(-1, _active);
        Assert.Empty(harness.Gui.RootNode!.Children);
    }
}
