using Example_76_Odin_Attributes;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Autoformers;

public class AttributeTourTests
{
    public static TheoryData<int> PageIndices => [.. Enumerable.Range(0, DemoPages.All.Count)];

    [Theory]
    [MemberData(nameof(PageIndices))]
    public void EveryPageBuildsItsCodeAndLiveForm(int index)
    {
        using var harness = new FrameHarness(1280, 860);
        var tour = new AttributeTour { Selected = index };
        var page = DemoPages.All[index];

        harness.Frame(tour.Draw);
        harness.Frame(tour.Draw);

        Assert.Equal(page.IsAvailable, Find(harness.Gui, "page/form") is not null);
        Assert.Equal(page.IsAvailable, Find(harness.Gui, "page/code") is not null);
    }

    [Fact]
    public void EveryShownSourceIsTheCompiledDemoClass()
    {
        foreach (var page in DemoPages.All.Where(page => page.IsAvailable))
        {
            var source = page.Source();

            Assert.Contains($"class {page.Demo!.Name}", source);
            Assert.DoesNotContain("namespace ", source);
        }
    }

    [Fact]
    public void ComingSoonPagesHaveNoSource()
    {
        Assert.All(DemoPages.All.Where(page => !page.IsAvailable), page => Assert.Equal("", page.Source()));
    }

    [Fact]
    public void ClickingASidebarEntryOpensItsPage()
    {
        using var harness = new FrameHarness(1280, 860);
        var tour = new AttributeTour();

        harness.Frame(tour.Draw);
        harness.Click(tour.Draw, FrameHarness.Center(Find(harness.Gui, "nav/3")!));

        Assert.Equal(3, tour.Selected);
    }

    [Fact]
    public void ButtonPageRunsItsMethodOncePerClick()
    {
        using var harness = new FrameHarness(1280, 860);
        var page = DemoPages.All.Single(page => page.Demo == typeof(ButtonDemo));
        var tour = new AttributeTour { Selected = DemoPages.All.ToList().IndexOf(page) };
        var demo = (ButtonDemo)tour.TargetOf(page);

        harness.Frame(tour.Draw);
        harness.Click(tour.Draw, FrameHarness.Center(Find(harness.Gui, "page/form/s0/b0")!));
        harness.Click(tour.Draw, FrameHarness.Center(Find(harness.Gui, "page/form/s0/b0")!));

        Assert.Equal(2, demo.Clicks);
    }

    [Fact]
    public void WideWindowPutsTheCodeBesideTheForm()
    {
        var page = Probe(1920, 0);

        Assert.Equal(420, page.ResultW, 2);
        Assert.Equal(page.ResultX + page.ResultW + 16, page.CodeX, 2);
        Assert.Equal(page.ResultY, page.CodeY, 2);
    }

    [Fact]
    public void NarrowWindowStacksTheCodeBelowTheForm()
    {
        var page = Probe(1000, 0);

        Assert.Equal(page.ResultW, page.CodeW, 2);
        Assert.Equal(page.ResultX, page.CodeX, 2);
        Assert.True(page.CodeY >= page.ResultY + page.ResultH);
    }

    [Fact]
    public void CodeKeepsItsLineWidthAndPansInsteadOfWrapping()
    {
        var index = IndexOf(typeof(ListsAndArraysDemo));
        var wide = Probe(1920, index);
        var narrow = Probe(1000, index);

        Assert.True(wide.CodeBodyInsidePanel);
        Assert.True(narrow.CodePans);
        Assert.Equal(wide.TextW, narrow.TextW, 2);
    }

    /// <summary>How a page's panels measured at one window width, in points.</summary>
    sealed record PagePanels(float ResultX, float ResultY, float ResultW, float ResultH,
                             float CodeX, float CodeY, float CodeW, float BodyW, float TextW,
                             bool CodeBodyInsidePanel, bool CodePans);

    static PagePanels Probe(int windowWidth, int index)
    {
        using var harness = new FrameHarness(windowWidth, 900);
        var tour = new AttributeTour { Selected = index };

        harness.Frame(tour.Draw);
        harness.Frame(tour.Draw);

        var result = Find(harness.Gui, "page/result")!;
        var code = Find(harness.Gui, "page/code")!;
        var body = code.Children[1];
        var text = body.Children[0];

        return new PagePanels(result.Rect.X, result.Rect.Y, result.Rect.W, result.Rect.H,
            code.Rect.X, code.Rect.Y, code.Rect.W, body.Rect.W, text.Rect.W,
            body.Parent == code && text.Parent == body,
            harness.Gui.GetScrollState(body.Id) is not null);
    }

    static int IndexOf(Type demo) => DemoPages.All.ToList().IndexOf(DemoPages.All.Single(page => page.Demo == demo));

    static LayoutNode? Find(Gui gui, string id) => Walk(gui.RootNode!).FirstOrDefault(node => node.Id == id);

    static IEnumerable<LayoutNode> Walk(LayoutNode node) => node.Children.SelectMany(Walk).Prepend(node);
}
