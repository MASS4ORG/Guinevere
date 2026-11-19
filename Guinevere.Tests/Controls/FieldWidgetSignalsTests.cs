using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// The field, value and selection widgets return their node: it reports <c>Changed</c>/<c>Submitted</c>/<c>Clicked</c> in
/// the render pass only when something really happened, takes the scope's default size, and lets a sheet or the
/// returned node override it.
/// </summary>
public class FieldWidgetSignalsTests
{
    [Fact]
    public void TextInputReportsChangedOnlyOnTheFrameTheTextChanges()
    {
        using var harness = new FrameHarness();
        var text = "";
        var changes = 0;
        LayoutNode? field = null;
        void Draw(Gui gui)
        {
            var node = gui.TextInput(ref text);
            field = node;
            if (gui.Pass == Pass.Pass2Render && node.Changed)
            {
                changes++;
            }
        }

        harness.Frame(Draw);
        harness.Click(Draw, FrameHarness.Center(field!));
        harness.Frame(Draw);
        Assert.Equal(0, changes);

        harness.Input.TypeText("a");
        harness.Frame(Draw);
        harness.Frame(Draw);

        Assert.Equal("a", text);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void TextInputReportsSubmittedOnEnterWhileFocused()
    {
        using var harness = new FrameHarness();
        var text = "go";
        var submissions = 0;
        LayoutNode? field = null;
        void Draw(Gui gui)
        {
            var node = gui.TextInput(ref text);
            field = node;
            if (gui.Pass == Pass.Pass2Render && node.Submitted)
            {
                submissions++;
            }
        }

        harness.Frame(Draw);
        harness.Click(Draw, FrameHarness.Center(field!));
        harness.Input.PressKey(KeyboardKey.Enter);
        harness.Frame(Draw);

        Assert.Equal(1, submissions);
        Assert.Equal("go", text);
    }

    [Fact]
    public void FieldsTakeTheScopeSizeUnlessASheetOrTheNodeSaysOtherwise()
    {
        using var harness = new FrameHarness();
        var text = "";
        var value = 0.5f;
        LayoutNode? input = null, slider = null;
        harness.Frame(gui =>
        {
            input = gui.TextInput(ref text);
            slider = gui.Slider(ref value, 0, 1);
        });
        Assert.Equal(ControlMetrics.FieldWidth, input!.Rect.W);
        Assert.Equal(ControlMetrics.FieldHeight, input.Rect.H);
        Assert.Equal(ControlMetrics.CompactHeight, slider!.Rect.H);

        harness.Gui.StyleSheets.Add(StyleSheet.Parse("input { width = 120; height = 40; }"));
        LayoutNode? styled = null, resized = null;
        harness.Frame(gui =>
        {
            styled = gui.TextInput(ref text, id: "styled");
            resized = gui.TextInput(ref text, id: "resized").Width(80);
        });
        Assert.Equal(120f, styled!.Rect.W);
        Assert.Equal(40f, styled.Rect.H);
        Assert.Equal(80f, resized!.Rect.W);
    }

    [Fact]
    public void SliderReportsChangedWhenThePointerMovesTheValue()
    {
        using var harness = new FrameHarness();
        var value = 0f;
        var changes = 0;
        LayoutNode? slider = null;
        void Draw(Gui gui)
        {
            var node = gui.Slider(ref value, 0, 10);
            slider = node;
            if (gui.Pass == Pass.Pass2Render && node.Changed)
            {
                changes++;
            }
        }

        harness.Frame(Draw);
        harness.Click(Draw, new Vector2(slider!.Rect.X + slider.Rect.W * 0.75f, FrameHarness.Center(slider).Y));
        harness.Frame(Draw);

        Assert.Equal(7.5f, value, 1);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void BreadcrumbReportsClickedWhenACrumbRunsItsAction()
    {
        using var harness = new FrameHarness();
        var runs = 0;
        var clicks = 0;
        IReadOnlyList<BreadcrumbItem> items =
        [
            new("Home", () => runs++),
            new("Page", IsCurrent: true),
        ];
        void Draw(Gui gui)
        {
            var node = gui.Breadcrumb(items);
            if (gui.Pass == Pass.Pass2Render && node.Clicked)
            {
                clicks++;
            }
        }

        harness.Frame(Draw);
        var home = Crumbs(harness.Gui).First();
        harness.Click(Draw, FrameHarness.Center(home));

        Assert.Equal(1, runs);
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void DropdownOptionsTakeAnyContentAndReportChanged()
    {
        using var harness = new FrameHarness(400, 400);
        var selected = -1;
        var changes = 0;
        LayoutNode? button = null;
        void Draw(Gui gui)
        {
            var node = gui.Dropdown([Icon.FromGlyph("+").Labeled("Add"), "Second"], ref selected, id: "choice");
            button = node;
            if (gui.Pass == Pass.Pass2Render && node.Changed)
            {
                changes++;
            }
        }

        harness.Frame(Draw);
        harness.Click(Draw, FrameHarness.Center(button!));
        harness.Frame(Draw);
        var option = Find(harness.Gui.RootNode!, "choice/list/1");
        Assert.NotNull(option);
        harness.Click(Draw, FrameHarness.Center(option));
        harness.Frame(Draw);

        Assert.Equal(1, selected);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void DisabledFieldsReportNothing()
    {
        using var harness = new FrameHarness();
        var text = "";
        var signalled = false;
        LayoutNode? field = null;
        void Draw(Gui gui)
        {
            var node = gui.TextInput(ref text, enabled: false);
            field = node;
            signalled |= node;
        }

        harness.Frame(Draw);
        harness.Click(Draw, FrameHarness.Center(field!));
        harness.Input.TypeText("x");
        harness.Frame(Draw);

        Assert.False(signalled);
        Assert.Equal("", text);
    }

    static List<LayoutNode> Crumbs(Gui gui)
    {
        var found = new List<LayoutNode>();
        Visit(gui.RootNode!);
        return found;

        void Visit(LayoutNode node)
        {
            if (node.StyleTarget?.Type == "crumb")
            {
                found.Add(node);
            }

            foreach (var child in node.Children)
            {
                Visit(child);
            }
        }
    }

    static LayoutNode? Find(LayoutNode node, string idSuffix)
    {
        if (node.Id.EndsWith(idSuffix, StringComparison.Ordinal))
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            if (Find(child, idSuffix) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
