using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

public class VerticalTabsTests
{
    int _active;
    readonly List<(int Index, string Title)> _closed = [];
    readonly List<string> _contentBuilt = [];

    void Draw(Gui gui, bool closable = false)
    {
        gui.VerticalTabs(ref _active, tabs =>
        {
            tabs.Tab("A", () => _contentBuilt.Add("A"), closable: closable);
            tabs.Tab("B", () => _contentBuilt.Add("B"), closable: closable);
        }, id: "vertical", onTabClosed: (index, title) => _closed.Add((index, title)));
    }

    static LayoutNode Tab(Gui gui, int index) => gui.RootNode!.Children[0].Children[0].Children[index];

    [Fact]
    public void ClickingAnotherVerticalTabActivatesItsContent()
    {
        using var harness = new FrameHarness();
        void Frame(Gui gui) => Draw(gui);

        harness.Frame(Frame);
        harness.Click(Frame, FrameHarness.Center(Tab(harness.Gui, 1)));
        harness.Frame(Frame);

        Assert.Equal(1, _active);
        Assert.Contains("B", _contentBuilt);
        Assert.Empty(_closed);
    }

    [Fact]
    public void MiddleClickClosesOnlyAClosableVerticalTab()
    {
        using var harness = new FrameHarness();
        void Fixed(Gui gui) => Draw(gui);
        void Closable(Gui gui) => Draw(gui, closable: true);

        harness.Frame(Fixed);
        harness.Click(Fixed, FrameHarness.Center(Tab(harness.Gui, 0)), MouseButton.Middle);
        Assert.Empty(_closed);

        harness.Frame(Closable);
        harness.Click(Closable, FrameHarness.Center(Tab(harness.Gui, 0)), MouseButton.Middle);
        Assert.Equal([(0, "A")], _closed);
    }
}
