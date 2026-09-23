using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Flyout rows: actions, separators, shortcuts, submenus and disabled items.</summary>
public class FlyoutTests
{
    const float ItemHeight = 32;
    static readonly Vector2 Origin = new(20, 20);

    bool _open = true;
    readonly List<string> _ran = [];

    void Draw(Gui gui) =>
        gui.Flyout(ref _open, Origin, menu => menu
            .Item("Open", () => _ran.Add("Open"), shortcut: "Ctrl+O")
            .Separator()
            .Submenu("More", sub => sub.Item("Nested"))
            .Item("Off", () => _ran.Add("Off"), enabled: false), itemHeight: ItemHeight);

    static Vector2 Row(int index) => new(Origin.X + 30, Origin.Y + ItemHeight * (index + 0.5f));

    [Fact]
    public void BuildsOneRowPerItem()
    {
        using var harness = new FrameHarness();

        harness.Frame(Draw);

        var menu = Assert.Single(harness.Gui.RootNode!.Children);
        Assert.Equal(4, menu.Children.Count);
        Assert.All(menu.Children, row => Assert.Equal(ItemHeight, row.Rect.H, 2));
    }

    [Fact]
    public void ClickingAnItemRunsItsActionAndCloses()
    {
        using var harness = new FrameHarness();
        harness.Frame(Draw);

        harness.Click(Draw, Row(0));

        Assert.Equal(["Open"], _ran);
        Assert.False(_open);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SeparatorsSubmenusAndDisabledItemsKeepTheMenuOpen(int row)
    {
        using var harness = new FrameHarness();
        harness.Frame(Draw);

        harness.Input.MoveTo(Row(row));
        harness.Frame(Draw);
        harness.Click(Draw, Row(row));

        Assert.Empty(_ran);
        Assert.True(_open);
    }

    [Fact]
    public void ClickingOutsideCloses()
    {
        using var harness = new FrameHarness();
        harness.Frame(Draw);

        harness.Click(Draw, new Vector2(390, 290));

        Assert.False(_open);
    }
}
