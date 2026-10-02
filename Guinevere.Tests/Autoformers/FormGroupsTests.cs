using Autoformers;
using Guinevere.Tests.Mocks;
using MASS4.Attributes;

namespace Guinevere.Tests.Autoformers;

public class FormGroupsTests
{
    [Fact]
    public void NestedObjectsDrawInACompartmentHoldingHeadingAndBody()
    {
        var gui = Render(new FormRenderContext(), Field(new Outer(), nameof(Outer.Middle)));
        var box = Walk(Find(gui, "f0/box")!).ToList();

        Assert.Contains(Find(gui, "f0/head")!, box);
        Assert.Contains(Find(gui, "f0/body")!, box);
    }

    [Fact]
    public void CompartmentBodyIsIndentedAndSitsRightUnderItsHeading()
    {
        var gui = Render(new FormRenderContext(), Field(new Outer(), nameof(Outer.Middle)));
        var head = Find(gui, "f0/head")!.Rect;
        var body = Find(gui, "f0/body")!.Rect;

        Assert.Equal(head.X + FormIndent.Default, body.X, 0.5f);
        Assert.InRange(body.Y - (head.Y + head.H), 0f, 4f);
    }

    [Fact]
    public void FoldedCompartmentKeepsOnlyItsHeading()
    {
        var gui = Render(new FormRenderContext { Collapsed = { "f0" } }, Field(new Outer(), nameof(Outer.Middle)));

        Assert.NotNull(Find(gui, "f0/box"));
        Assert.NotNull(Find(gui, "f0/head"));
        Assert.Null(Find(gui, "f0/body"));
    }

    [Fact]
    public void NestingDepthGrowsPerCompartmentAndStaysStableAcrossPasses()
    {
        var seen = new Dictionary<string, int>();
        var drawers = new FormDrawers();
        using var _ = drawers.Add(field => field.ValueType == typeof(int), new DepthProbe(seen));
        var target = new Outer();

        Render(new FormRenderContext { Drawers = drawers }, Field(target, nameof(Outer.Top)),
            Field(target, nameof(Outer.Middle)));

        Assert.Equal(0, seen[nameof(Outer.Top)]);
        Assert.Equal(1, seen[nameof(Middle.Level)]);
        Assert.Equal(2, seen[nameof(Inner.Deep)]);
    }

    [Fact]
    public void CompartmentFillAlternatesWithDepth()
    {
        var background = ControlPalette.Light.BaseBackground;
        var ink = ControlPalette.Light.Text;

        Assert.NotEqual(background, FormGroups.CompartmentFill(background, ink, 0));
        Assert.NotEqual(FormGroups.CompartmentFill(background, ink, 0), FormGroups.CompartmentFill(background, ink, 1));
        Assert.Equal(FormGroups.CompartmentFill(background, ink, 0), FormGroups.CompartmentFill(background, ink, 2));
    }

    [Fact]
    public void NestedObjectsInCollectionEntriesGetCompartments()
    {
        var gui = Render(new FormRenderContext(), Field(new Outer(), nameof(Outer.Items)));

        Assert.NotNull(Find(gui, "f0/entry0/box"));
    }

    [Fact]
    public void PagingSlicesClampsAndCanBeTurnedOff()
    {
        var settings = new ListDrawerSettingsAttribute();
        var pages = new Dictionary<string, int> { ["late"] = 9 };

        Assert.Equal(new FormGroups.PageView(0, 4, 0, 10), FormGroups.Paging(40, settings, pages, "x"));
        Assert.Equal(new FormGroups.PageView(3, 4, 30, 40), FormGroups.Paging(40, settings, pages, "late"));
        Assert.Equal(new FormGroups.PageView(0, 1, 0, 0), FormGroups.Paging(0, settings, pages, "x"));
        Assert.Equal(new FormGroups.PageView(0, 1, 0, 40),
            FormGroups.Paging(40, new ListDrawerSettingsAttribute { ShowPaging = false }, pages, "x"));
        Assert.Equal(new FormGroups.PageView(0, 40, 0, 1),
            FormGroups.Paging(40, new ListDrawerSettingsAttribute { NumberOfItemsPerPage = 0 }, pages, "x"));
    }

    [Fact]
    public void PageButtonsTurnPagesOnePerClick()
    {
        using var harness = new FrameHarness(800, 1200);
        var context = new FormRenderContext();
        var field = Field(new Lists(), nameof(Lists.Many));
        void Draw(Gui gui) => gui.FormField(field, "many", context);

        harness.Frame(Draw);
        Assert.NotNull(Find(harness.Gui, "many/entry9"));
        Assert.Null(Find(harness.Gui, "many/entry10"));

        harness.Click(Draw, FrameHarness.Center(Find(harness.Gui, "many/next")!));
        harness.Frame(Draw);
        Assert.Equal(1, context.Pages["many"]);
        Assert.NotNull(Find(harness.Gui, "many/entry10"));

        harness.Click(Draw, FrameHarness.Center(Find(harness.Gui, "many/prev")!));
        Assert.Equal(0, context.Pages["many"]);
    }

    [Fact]
    public void AddingFollowsTheNewEntryToTheLastPage()
    {
        using var harness = new FrameHarness(800, 1200);
        var target = new Lists();
        var context = new FormRenderContext();
        var field = Field(target, nameof(Lists.Many));
        void Draw(Gui gui) => gui.FormField(field, "many", context);

        harness.Frame(Draw);
        harness.Click(Draw, FrameHarness.Center(Find(harness.Gui, "many/add")!));

        Assert.Equal(41, target.Many.Count);
        Assert.Equal(4, context.Pages["many"]);
    }

    [Fact]
    public void ListEntriesHideIndexLabelsUnlessAskedDictionariesKeepKeys()
    {
        var target = new Lists();
        var gui = Render(new FormRenderContext(), Field(target, nameof(Lists.Words)), Field(target, nameof(Lists.Labelled)),
            Field(target, nameof(Lists.Scores)));

        Assert.NotNull(Find(gui, "f0/entry0/value"));
        Assert.Null(Find(gui, "f0/entry0/label"));
        Assert.NotNull(Find(gui, "f1/entry0/label"));
        Assert.NotNull(Find(gui, "f2/entry0/label"));
    }

    [Fact]
    public void DragHandlesAppearOnlyOnReorderableCollections()
    {
        var target = new Lists();
        var gui = Render(new FormRenderContext(), Field(target, nameof(Lists.Words)), Field(target, nameof(Lists.Fixed)),
            Field(target, nameof(Lists.Scores)));

        Assert.NotNull(Find(gui, "f0/entry0/handle"));
        Assert.Null(Find(gui, "f1/entry0/handle"));
        Assert.Null(Find(gui, "f2/entry0/handle"));

        var frozen = Render(new FormRenderContext(),
            FormField.ForMember(typeof(Lists).GetProperty(nameof(Lists.Words))!, target,
                new FormOptions { ReadOnly = true }));
        Assert.Null(Find(frozen, "f0/entry0/handle"));
    }

    [Fact]
    public void DraggingAnEntryOntoAnotherMovesItOnce()
    {
        using var harness = new FrameHarness(800, 1200);
        var target = new Lists();
        var notified = new List<object>();
        var field = FormField.ForMember(typeof(Lists).GetProperty(nameof(Lists.Words))!, target,
            new FormOptions { MutationNotifier = notified.Add });
        void Draw(Gui gui) => gui.FormField(field, "words");

        harness.Frame(Draw);
        DragTo(harness, Draw, Find(harness.Gui, "words/entry0/handle")!, Find(harness.Gui, "words/entry2")!);

        Assert.Equal(["b", "c", "a"], target.Words);
        Assert.Single(notified);
    }

    [Fact]
    public void EntriesNeverMoveAcrossCollections()
    {
        using var harness = new FrameHarness(800, 1200);
        var target = new Lists();
        var words = Field(target, nameof(Lists.Words));
        var others = Field(target, nameof(Lists.Others));
        void Draw(Gui gui)
        {
            using (gui.Node(-1, -1, "root").ExpandWidth().Direction(Axis.Vertical).Enter())
            {
                gui.FormField(words, "words");
                gui.FormField(others, "others");
            }
        }

        harness.Frame(Draw);
        DragTo(harness, Draw, Find(harness.Gui, "words/entry0/handle")!, Find(harness.Gui, "others/entry1")!);

        Assert.Equal(["a", "b", "c"], target.Words);
        Assert.Equal(["x", "y"], target.Others);
    }

    static void DragTo(FrameHarness harness, Action<Gui> draw, LayoutNode from, LayoutNode to)
    {
        var start = FrameHarness.Center(from);
        var end = FrameHarness.Center(to);
        harness.Input.MoveTo(start);
        harness.Input.PressButton(MouseButton.Left);
        harness.Frame(draw);
        for (var step = 1; step <= 6; step++)
        {
            harness.Input.MoveTo(Vector2.Lerp(start, end, step / 6f));
            harness.Frame(draw);
        }

        harness.Input.ReleaseButton(MouseButton.Left);
        for (var i = 0; i < 3; i++) harness.Frame(draw);
    }

    sealed class Lists
    {
        public List<int> Many { get; set; } = [.. Enumerable.Range(0, 40)];
        public List<string> Words { get; set; } = ["a", "b", "c"];
        public List<string> Others { get; set; } = ["x", "y"];
        [ListDrawerSettings(ShowIndexLabels = true)] public List<int> Labelled { get; set; } = [1];
        [ListDrawerSettings(DraggableItems = false)] public List<int> Fixed { get; set; } = [1];
        public Dictionary<string, int> Scores { get; set; } = new() { ["one"] = 1 };
    }

    static Gui Render(FormRenderContext context, params FormField[] fields)
    {
        var harness = new FrameHarness(800, 1200);
        void Draw(Gui gui)
        {
            using (gui.Node(-1, -1, "root").ExpandWidth().Direction(Axis.Vertical).Enter())
                for (var i = 0; i < fields.Length; i++)
                    gui.FormField(fields[i], $"f{i}", context);
        }

        harness.Frame(Draw);
        harness.Frame(Draw);
        return harness.Gui;
    }

    static FormField Field(object target, string name) =>
        FormField.ForMember(target.GetType().GetMember(name)[0], target);

    static LayoutNode? Find(Gui gui, string id) => Walk(gui.RootNode!).FirstOrDefault(node => node.Id == id);

    static IEnumerable<LayoutNode> Walk(LayoutNode node) => node.Children.SelectMany(Walk).Prepend(node);

    sealed class Inner
    {
        public int Deep { get; set; }
    }

    sealed class Middle
    {
        public int Level { get; set; }
        public Inner Inner { get; set; } = new();
    }

    sealed class Outer
    {
        public int Top { get; set; }
        public Middle Middle { get; set; } = new();
        public List<Inner> Items { get; set; } = [new()];
    }

    sealed class DepthProbe(Dictionary<string, int> seen) : IPropertyDrawer
    {
        public void Draw(Gui gui, FormField field, string id, FormRenderContext context) =>
            seen[field.Name] = new FormStyle(gui).Depth;

        public bool DrawValue(Gui gui, FormField field, string id, FormRenderContext context) => false;
    }
}
