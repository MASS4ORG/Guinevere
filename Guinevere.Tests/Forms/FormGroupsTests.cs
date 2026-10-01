using Guinevere.Forms;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Forms;

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
        Guinevere.Forms.FormField.ForMember(target.GetType().GetMember(name)[0], target);

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
