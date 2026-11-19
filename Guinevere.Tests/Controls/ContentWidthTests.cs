using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Every content kind must size a flyout's rows and page a tab strip without throwing, and an overflowing strip must
/// move to the next page when its next button is clicked.
/// </summary>
public class ContentWidthTests
{
    const string LongLabel = "A much longer label here";
    static readonly Vector2 Origin = new(20, 20);

    [Fact]
    public void FlyoutBuildsEveryContentKind()
    {
        using var harness = new FrameHarness(800, 600);
        var open = true;
        void Draw(Gui gui) => gui.Flyout(ref open, Origin, menu => menu
            .Item(Icon.FromGlyph("+").Labeled("Add"))
            .Item(Content.Row(Icon.FromGlyph("+"), LongLabel))
            .Item(Content.Build(() => gui.DrawText("w")))
            .Item(Content.Text(() => "lazy")));

        harness.Frame(Draw);
        harness.Frame(Draw);

        var menu = Assert.Single(harness.Gui.RootNode!.Children);
        Assert.Equal(4, menu.Children.Count);
    }

    [Fact]
    public void MenuIsAtLeastTheWidestLabelPlusPaddingAndNeverNarrowerThanTheMinimum()
    {
        using var harness = new FrameHarness(800, 600);
        var open = true;
        void Draw(Gui gui) => gui.Flyout(ref open, Origin, menu => menu
            .Item("Short")
            .Item(Content.Row(Icon.FromGlyph("+"), LongLabel)));

        harness.Frame(Draw);

        var menu = Assert.Single(harness.Gui.RootNode!.Children);
        var style = harness.Gui.ControlStyle;
        using var font = new SKFont { Size = style.CompactFontSize };
        font.MeasureText(LongLabel, out var bounds);
        Assert.True(menu.Rect.W >= bounds.Width + style.Spacing * 2,
            $"menu {menu.Rect.W} narrower than label {bounds.Width} plus padding");
        Assert.True(menu.Rect.W >= 0.75f * style.FieldWidth,
            $"menu {menu.Rect.W} narrower than the minimum {0.75f * style.FieldWidth}");
    }

    [Fact]
    public void AnIconAndLongTextRowMakesTheMenuWiderThanAShortLabel()
    {
        var shortMenu = MenuWidth(static menu => menu.Item("Add"));
        var rowMenu = MenuWidth(static menu => menu.Item(Content.Row(Icon.FromGlyph("+"), LongLabel)));

        Assert.True(rowMenu > shortMenu, $"row menu {rowMenu} should be wider than short menu {shortMenu}");
    }

    [Fact]
    public void OverflowingTabStripPagesForwardWhenNextIsClicked()
    {
        using var harness = new FrameHarness(400, 300);
        string? active = null;
        void Draw(Gui gui)
        {
            using (gui.Node(200, 40).Enter())
            {
                gui.TabStrip(ref active, tabs =>
                {
                    for (var i = 0; i < 20; i++)
                    {
                        tabs.Tab($"Tab {i}", key: $"t{i}");
                    }
                }, id: "strip");
            }
        }

        // The first frame learns the strip's viewport width; the second frame pages with it.
        harness.Frame(Draw);
        harness.Frame(Draw);

        var root = harness.Gui.RootNode!;
        Assert.NotNull(root.FindChildById("strip/previous"));
        var next = root.FindChildById("strip/next");
        Assert.NotNull(next);
        Assert.NotNull(root.FindChildById("strip/t0"));

        harness.Click(Draw, FrameHarness.Center(next!));

        Assert.Null(harness.Gui.RootNode!.FindChildById("strip/t0"));
    }

    static float MenuWidth(Action<FlyoutBuilder> items)
    {
        using var harness = new FrameHarness(800, 600);
        var open = true;
        void Draw(Gui gui) => gui.Flyout(ref open, Origin, items);

        harness.Frame(Draw);

        return Assert.Single(harness.Gui.RootNode!.Children).Rect.W;
    }
}
