using Autoformers;
using Guinevere.Tests.Mocks;
using MASS4.Attributes;

namespace Guinevere.Tests.Autoformers;

public class AttributeDrawersTests
{
    [Fact]
    public void TitleDrawsHeadingSubtitleAndLineAboveTheField()
    {
        var target = new Model();
        var gui = Render(new FormRenderContext(), Field(target, nameof(Model.Full)), Field(target, nameof(Model.Plain)));

        Assert.NotNull(Find(gui, "f0/title/text"));
        Assert.NotNull(Find(gui, "f0/title/subtitle"));
        Assert.NotNull(Find(gui, "f0/title/line"));
        Assert.NotNull(Find(gui, "f1/title/text"));
        Assert.Null(Find(gui, "f1/title/subtitle"));
        Assert.Null(Find(gui, "f1/title/line"));
    }

    [Fact]
    public void RequiredShowsAnErrorOnlyWhileTheValueIsMissing()
    {
        var target = new Model { Name = "", Tags = [], Present = "x" };
        var gui = Render(new FormRenderContext(), Field(target, nameof(Model.Name)), Field(target, nameof(Model.Tags)),
            Field(target, nameof(Model.Present)), Field(target, nameof(Model.Missing)));

        Assert.NotNull(Find(gui, "f0/required/error"));
        Assert.NotNull(Find(gui, "f1/required/error"));
        Assert.Null(Find(gui, "f2/required/error"));
        Assert.NotNull(Find(gui, "f3/required/error"));
    }

    [Fact]
    public void RequiredNeverBlocksAWrite()
    {
        var target = new Model { Present = "x" };

        Assert.True(Field(target, nameof(Model.Present)).SetValue(""));
        Assert.Equal("", target.Present);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("a", false)]
    [InlineData(0, false)]
    public void IsMissingCoversNullEmptyTextAndEmptyCollections(object? value, bool missing)
    {
        Assert.Equal(missing, RequiredDrawer.IsMissing(value));
        Assert.True(RequiredDrawer.IsMissing(new List<int>()));
        Assert.False(RequiredDrawer.IsMissing(new List<int> { 1 }));
    }

    [Fact]
    public void RequiredMessageDefaultsToTheLabel()
    {
        var target = new Model();

        Assert.Equal("Name is required", RequiredDrawer.Message(Field(target, nameof(Model.Name)), new RequiredAttribute()));
        Assert.Equal("Pick tags", RequiredDrawer.Message(Field(target, nameof(Model.Tags)), new RequiredAttribute("Pick tags")));
    }

    [Fact]
    public void TitleWrapsRequiredSoTheErrorStaysUnderTheField()
    {
        var gui = Render(new FormRenderContext(), Field(new Model(), nameof(Model.Name)));

        Assert.Contains(Find(gui, "f0/required")!, Walk(Find(gui, "f0/title")!));
    }

    [Fact]
    public void GuiColorTintsTextAndSurfaceForEverythingInside()
    {
        var seen = new List<(Color Text, Color Surface)>();
        var drawers = new FormDrawers();
        using var _ = drawers.Add(field => field.Name == nameof(Model.Tinted), new Probe(seen));

        Render(new FormRenderContext { Drawers = drawers }, Field(new Model(), nameof(Model.Tinted)));

        var (text, surface) = seen[^1];
        Assert.Equal(Color.FromArgb(255, 255, 0, 0), text);
        Assert.NotEqual(ControlPalette.Light.Surface, surface);
        Assert.Equal(GuiColorDrawer.Blend(ControlPalette.Light.Surface, text, 0.25f), surface);
    }

    [Fact]
    public void HideLabelDrawsTheValueAcrossTheRow()
    {
        var gui = Render(new FormRenderContext(), Field(new Model(), nameof(Model.Unlabelled)));

        Assert.NotNull(Find(gui, "f0/value"));
        Assert.Null(Find(gui, "f0/label"));
    }

    [Fact]
    public void HideLabelFallsBackToDrawWhenTheDrawerHasNoValueForm()
    {
        var drawn = new List<string>();
        var drawers = new FormDrawers();
        using var _ = drawers.Add(typeof(int), new ValueLess(drawn));

        Render(new FormRenderContext { Drawers = drawers }, Field(new Model(), nameof(Model.Unlabelled)));

        Assert.Contains(nameof(Model.Unlabelled), drawn);
    }

    [Fact]
    public void ReadOnlyReachesPredicateDrawersInNestedObjectsAndCollectionEntries()
    {
        var seen = new List<(string Name, bool ReadOnly)>();
        var drawers = new FormDrawers();
        using var _ = drawers.Add(field => field.ValueType == typeof(int) || field.ValueType == typeof(string),
            new ReadOnlyProbe(seen));
        var target = new Model { Tags = ["a"] };
        var options = new FormOptions { ReadOnly = true };

        Render(new FormRenderContext { Drawers = drawers }, Field(target, nameof(Model.Inner), options),
            Field(target, nameof(Model.Tags), options));

        Assert.Contains((nameof(Inner.Value), true), seen);
        Assert.Contains(("[0]", true), seen);
        Assert.DoesNotContain(seen, entry => !entry.ReadOnly);
    }

    [Fact]
    public void TextAreaGrowsWithItsLinesBetweenMinAndMax()
    {
        var target = new Model { Notes = "one", Long = string.Join('\n', Enumerable.Repeat("line", 20)) };
        var gui = Render(new FormRenderContext(), Field(target, nameof(Model.Notes)), Field(target, nameof(Model.Long)),
            Field(target, nameof(Model.Present)));
        var row = Find(gui, "f2")!.Rect.H;
        var shortArea = Find(gui, "f0")!.Rect.H;
        var longArea = Find(gui, "f1")!.Rect.H;

        Assert.True(shortArea > row);
        Assert.True(longArea > shortArea);
        Assert.Equal(BuiltinDrawers.TextAreaHeight(gui, target.Long, new TextAreaAttribute(3, 10)), longArea);
    }

    [Fact]
    public void TextAreaEditsThroughTheSameStringDrawer()
    {
        var gui = Render(new FormRenderContext(), editorOnly: true, Field(new Model { Notes = "a" }, nameof(Model.Notes)));

        Assert.NotEmpty(Find(gui, "root")!.Children);
    }

    [Fact]
    public void ReadOnlyTextAreaIsASummaryRow()
    {
        var gui = Render(new FormRenderContext(), Field(new Model { Notes = "a\nb\nc\nd" }, nameof(Model.Notes),
            new FormOptions { ReadOnly = true }));

        Assert.Equal(new FormStyle(gui).RowHeight, Find(gui, "f0")!.Rect.H);
    }

    static Gui Render(FormRenderContext context, params FormField[] fields) => Render(context, false, fields);

    static Gui Render(FormRenderContext context, bool editorOnly, params FormField[] fields)
    {
        var harness = new FrameHarness(800, 1200);
        void Draw(Gui gui)
        {
            using (gui.Node(-1, -1, "root").ExpandWidth().Direction(Axis.Vertical).Enter())
                for (var i = 0; i < fields.Length; i++)
                    if (editorOnly) gui.FormFieldEditor(fields[i], $"f{i}", context);
                    else gui.FormField(fields[i], $"f{i}", context);
        }

        harness.Frame(Draw);
        return harness.Gui;
    }

    static FormField Field(object target, string name, FormOptions? options = null) =>
        FormField.ForMember(target.GetType().GetMember(name)[0], target, options);

    static LayoutNode? Find(Gui gui, string id) => Walk(gui.RootNode!).FirstOrDefault(node => node.Id == id);

    static IEnumerable<LayoutNode> Walk(LayoutNode node) => node.Children.SelectMany(Walk).Prepend(node);

    sealed class Inner
    {
        public int Value { get; set; }
    }

    sealed class Model
    {
        [Title("Audio", "Mixer settings")] public int Full { get; set; }
        [Title("Plain", horizontalLine: false)] public int Plain { get; set; }
        [Title("Identity"), Required] public string? Name { get; set; }
        [Required("Pick tags")] public List<string> Tags { get; set; } = [];
        [Required] public string? Present { get; set; }
        [Required] public Inner? Missing { get; set; }
        [GuiColor("#FF0000")] public int Tinted { get; set; }
        [HideLabel] public int Unlabelled { get; set; }
        [TextArea] public string Notes { get; set; } = "";
        [TextArea(3, 10)] public string Long { get; set; } = "";
        public Inner Inner { get; set; } = new();
    }

    sealed class Probe(List<(Color, Color)> seen) : IPropertyDrawer
    {
        public void Draw(Gui gui, FormField field, string id, FormRenderContext context)
        {
            var style = new FormStyle(gui);
            seen.Add((style.Ink, style.Field));
        }

        public bool DrawValue(Gui gui, FormField field, string id, FormRenderContext context) => false;
    }

    sealed class ValueLess(List<string> drawn) : IPropertyDrawer
    {
        public void Draw(Gui gui, FormField field, string id, FormRenderContext context) => drawn.Add(field.Name);
        public bool DrawValue(Gui gui, FormField field, string id, FormRenderContext context) => false;
    }

    sealed class ReadOnlyProbe(List<(string, bool)> seen) : IPropertyDrawer
    {
        public void Draw(Gui gui, FormField field, string id, FormRenderContext context) =>
            seen.Add((field.Name, field.IsReadOnly));

        public bool DrawValue(Gui gui, FormField field, string id, FormRenderContext context) => false;
    }
}
