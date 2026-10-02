using Guinevere.Tests.Mocks;
using MASS4.Attributes;
using MASS4.Autoformers;

namespace Guinevere.Tests.Autoformers;

public class FormRendererTests
{
    [Fact]
    public void ClickingABoolWritesAndNotifiesOnce()
    {
        using var harness = new FrameHarness(600, 400);
        var target = new Settings();
        var notified = new List<object>();
        var field = Field(target, nameof(Settings.Enabled), new FormOptions { MutationNotifier = notified.Add });
        void Draw(Gui gui) => gui.FormField(field, "enabled");

        harness.Frame(Draw);
        harness.Click(Draw, FrameHarness.Center(Find(harness.Gui, "enabled/editor")!.Children[0]));

        Assert.True(target.Enabled);
        Assert.Single(notified);
    }

    [Fact]
    public void WritableTypeDrawerRunsBeforePredicateDrawer()
    {
        var (typed, predicate) = (new Recording(), new Recording());
        var drawers = new FormDrawers();
        using var a = drawers.Add(typeof(int), typed);
        using var b = drawers.Add(_ => true, predicate);

        Render(new FormRenderContext { Drawers = drawers }, Field(new Settings(), nameof(Settings.Count)));

        Assert.NotEmpty(typed.Drawn);
        Assert.Empty(predicate.Drawn);
    }

    [Fact]
    public void ReadOnlyFieldSkipsTypeDrawerForPredicateDrawer()
    {
        var (typed, predicate) = (new Recording(), new Recording());
        var drawers = new FormDrawers();
        using var a = drawers.Add(typeof(int), typed);
        using var b = drawers.Add(_ => true, predicate);

        Render(new FormRenderContext { Drawers = drawers }, Field(new Settings(), nameof(Settings.Locked)));

        Assert.Empty(typed.Drawn);
        Assert.NotEmpty(predicate.Drawn);
    }

    [Fact]
    public void CollectionsNestedObjectsAndBuiltinsDrawTheirOwnNodes()
    {
        var target = new Settings();
        var gui = Render(new FormRenderContext(), Field(target, nameof(Settings.Names)),
            Field(target, nameof(Settings.Inner)), Field(target, nameof(Settings.Count)),
            Field(target, nameof(Settings.Mode)), Field(target, nameof(Settings.Title)),
            Field(target, nameof(Settings.Position)), Field(target, nameof(Settings.Tint)),
            Field(target, nameof(Settings.Locked)));

        Assert.NotNull(Find(gui, "f0/entry1"));
        Assert.NotNull(Find(gui, "f1/body"));
        Assert.NotEmpty(Find(gui, "f2/editor")!.Children);
        Assert.NotNull(Find(gui, "f5/a2/axis"));
        Assert.NotNull(Find(gui, "f6/swatch"));
    }

    [Fact]
    public void CanInlineFalseDrawsTheNestedObjectAsASummary()
    {
        var gui = Render(new FormRenderContext { CanInline = _ => false }, Field(new Settings(), nameof(Settings.Inner)));

        Assert.Null(Find(gui, "f0/body"));
        Assert.NotNull(Find(gui, "f0/label"));
    }

    [Fact]
    public void FoldedGroupsDrawNoBody()
    {
        var context = new FormRenderContext { Collapsed = { "f0", "f1" } };
        var target = new Settings();
        var gui = Render(context, Field(target, nameof(Settings.Names)), Field(target, nameof(Settings.Inner)));

        Assert.Null(Find(gui, "f0/entry0"));
        Assert.Null(Find(gui, "f1/body"));
    }

    [Fact]
    public void ClickingAGroupHeadingFoldsAndUnfoldsIt()
    {
        using var harness = new FrameHarness(600, 400);
        var context = new FormRenderContext();
        var field = Field(new Settings(), nameof(Settings.Names));
        void Draw(Gui gui) => gui.FormField(field, "names", context);

        harness.Frame(Draw);
        var heading = FrameHarness.Center(Find(harness.Gui, "names/head/label")!);
        harness.Click(Draw, heading);
        Assert.Contains("names", context.Collapsed);

        harness.Click(Draw, heading);
        Assert.DoesNotContain("names", context.Collapsed);
    }

    [Fact]
    public void AddAndRemoveButtonsResizeTheCollectionOncePerClick()
    {
        using var harness = new FrameHarness(600, 400);
        var target = new Settings();
        var field = Field(target, nameof(Settings.Names));
        void Draw(Gui gui) => gui.FormField(field, "names");

        harness.Frame(Draw);
        harness.Click(Draw, FrameHarness.Center(Find(harness.Gui, "names/add")!));
        Assert.Equal(3, target.Names.Count);

        harness.Frame(Draw);
        harness.Click(Draw, FrameHarness.Center(Find(harness.Gui, "names/entry0/remove")!));
        Assert.Equal(["b", ""], target.Names);
    }

    [Fact]
    public void EditingANestedStructWritesItBackToItsOwner()
    {
        var target = new Settings();
        var notified = new List<object>();
        var drawers = new FormDrawers();
        using var _ = drawers.Add(typeof(int), new Writing(42));

        Render(new FormRenderContext { Drawers = drawers },
            Field(target, nameof(Settings.Range), new FormOptions { MutationNotifier = notified.Add }));

        Assert.Equal(42, target.Range.Low);
        Assert.Equal(42, target.Range.High);
        Assert.NotEmpty(notified);
    }

    [Fact]
    public void NestedFormsInheritReadOnly()
    {
        var target = new Settings();
        var drawers = new FormDrawers();
        using var _ = drawers.Add(_ => true, new Writing(42));

        Render(new FormRenderContext { Drawers = drawers },
            Field(target, nameof(Settings.Inner), new FormOptions { ReadOnly = true }));

        Assert.Equal(0, target.Inner.Value);
    }

    [Fact]
    public void AttributeDecoratorsWrapByOrder()
    {
        var calls = new List<string>();
        var drawers = new FormDrawers();
        using var a = drawers.Add<RangeAttribute>(new Decorating("range", 1, calls));
        using var b = drawers.Add<TooltipAttribute>(new Decorating("tooltip", 0, calls));

        Render(new FormRenderContext { Drawers = drawers }, Field(new Settings(), nameof(Settings.Volume)));

        Assert.Equal(["tooltip", "range", "tooltip", "range"], calls);
    }

    [Fact]
    public void EditorOnlyUsesTypeDrawerThenBuiltinFallback()
    {
        var drawers = new FormDrawers();
        var typed = new Recording();
        using var _ = drawers.Add(typeof(int), typed);
        var target = new Settings();
        var translated = new List<string>();
        var context = new FormRenderContext { Drawers = drawers, Translate = label => { translated.Add(label); return label; } };

        var gui = Render(context, editorOnly: true, Field(target, nameof(Settings.Count)),
            Field(target, nameof(Settings.Mode)), Field(target, nameof(Settings.Locked)));

        Assert.NotEmpty(typed.ValuesDrawn);
        Assert.NotEmpty(Find(gui, "root")!.Children);
        Assert.Contains("Fast Mode", translated);
    }

    [Fact]
    public void ModifiedFieldsDrawTheirRows()
    {
        var target = new Settings();
        var gui = Render(new FormRenderContext { IsModified = _ => true }, Field(target, nameof(Settings.Count)),
            Field(target, nameof(Settings.Names)), Field(target, nameof(Settings.Inner)));

        Assert.NotNull(Find(gui, "f0/label"));
    }

    [Fact]
    public void FormDrawsSectionsWithSwitchFieldsAndButtons()
    {
        using var harness = new FrameHarness(600, 600);
        var target = new Settings();
        var section = FormBuilder.Section(target, "Settings");
        var model = new FormModel(target,
            [section with { EnabledField = section.Fields.Single(f => f.Name == nameof(Settings.Enabled)) }]);
        void Draw(Gui gui) => gui.Form(model, "form");

        harness.Frame(Draw);
        Assert.Equal(2, Find(harness.Gui, "form/s0/head")!.Children.Count);
        harness.Click(Draw, FrameHarness.Center(Find(harness.Gui, "form/s0/b0")!));

        Assert.Equal(1, target.Resets);
    }

    [Fact]
    public void SummaryPrefersNameAndReportsThrowingGetters()
    {
        var failures = new List<FormFailure>();
        var field = Field(new Settings(), nameof(Settings.Inner), new FormOptions { FailureReporter = failures.Add });

        Assert.Equal("named", FormRenderer.Summary(field, new Named { Name = "named" }));
        Assert.Equal("Inner", FormRenderer.Summary(field, new Named()));
        Assert.Equal("Inner", FormRenderer.Summary(field, new Broken()));
        Assert.Equal(FormFailureKind.Read, Assert.Single(failures).Kind);
    }

    [Theory]
    [InlineData(nameof(Settings.Flat))]
    [InlineData(nameof(Settings.Position))]
    [InlineData(nameof(Settings.Spatial))]
    public void DraggingAVectorAxisLabelChangesThatAxis(string member)
    {
        using var harness = new FrameHarness(600, 400);
        var target = new Settings();
        var field = Field(target, member);
        void Draw(Gui gui) => gui.FormField(field, "pos");

        harness.Frame(Draw);
        Drag(harness, Draw, FrameHarness.Center(Find(harness.Gui, "pos/a0/axis")!), 20f);

        var (x, y) = field.GetValue() switch
        {
            Vector2 v => (v.X, v.Y),
            Vector3 v => (v.X, v.Y),
            Vector4 v => (v.X, v.Y),
            _ => (0f, 0f),
        };
        Assert.True(x > 0f);
        Assert.Equal(0f, y);
    }

    [Fact]
    public void DraggingANumberLabelScrubsWithinTheTypeRange()
    {
        using var harness = new FrameHarness(600, 400);
        var target = new Settings();
        var field = Field(target, nameof(Settings.Small));
        void Draw(Gui gui) => gui.FormField(field, "small");

        harness.Frame(Draw);
        Drag(harness, Draw, FrameHarness.Center(Find(harness.Gui, "small/label")!), 400f);

        Assert.Equal(byte.MaxValue, target.Small);
    }

    [Fact]
    public void VectorRowWritesThroughAndTouchesTheOwner()
    {
        using var harness = new FrameHarness(600, 400);
        var target = new Settings();
        var touched = new List<object>();
        var owner = Field(target, nameof(Settings.Inner), new FormOptions { MutationNotifier = touched.Add });
        var written = Vector3.Zero;
        var context = new FormRenderContext { IsModified = _ => true };
        void Draw(Gui gui)
        {
            FormControls.VectorRow(gui, "v2", "V2", Vector2.Zero, _ => { }, owner, context);
            FormControls.VectorRow(gui, "v3", "V3", written, v => written = v, owner, context);
            FormControls.VectorRow(gui, "v4", "V4", Vector4.Zero, _ => { }, owner);
        }

        harness.Frame(Draw);
        Drag(harness, Draw, FrameHarness.Center(Find(harness.Gui, "v3/a1/axis")!), 20f);

        Assert.True(written.Y > 0f);
        Assert.NotEmpty(touched);
    }

    [Fact]
    public void PopupAnchorStaysInsideTheWindow()
    {
        using var harness = new FrameHarness(400, 300);

        Assert.Equal(new Vector2(10, 30), PopupAnchor.Below(harness.Gui, new Rect(10, 10, 50, 20), 100, 50));
        Assert.Equal(new Vector2(300, 220), PopupAnchor.Below(harness.Gui, new Rect(390, 290, 50, 20), 100, 50));
    }

    [Fact]
    public void ToNumberSaturatesAtTheTypeLimits()
    {
        Assert.Equal(byte.MaxValue, FormControls.ToNumber(1000, typeof(byte)));
        Assert.Equal(byte.MinValue, FormControls.ToNumber(-5, typeof(byte)));
        Assert.Equal((float.MinValue, float.MaxValue), FormControls.TypeRange(typeof(Vector2)));
    }

    static void Drag(FrameHarness harness, Action<Gui> draw, Vector2 from, float dx)
    {
        harness.Input.MoveTo(from);
        harness.Input.PressButton(MouseButton.Left);
        harness.Frame(draw);
        for (var i = 1; i <= 4; i++)
        {
            harness.Input.MoveTo(from + new Vector2(dx * i / 4f, 0));
            harness.Frame(draw);
        }

        harness.Input.ReleaseButton(MouseButton.Left);
        harness.Frame(draw);
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

    static IEnumerable<LayoutNode> Walk(LayoutNode node) =>
        node.Children.SelectMany(Walk).Prepend(node);

    enum Mode
    {
        Slow,
        [EnumLabel("Fast Mode")] Fast,
    }

    struct Bounds
    {
        public int Low { get; set; }
        public int High { get; set; }
    }

    sealed class Inner
    {
        public int Value { get; set; }
    }

    sealed class Named
    {
        public string Name { get; set; } = "";
    }

    sealed class Broken
    {
        public string Name => throw new InvalidOperationException();
    }

    sealed class Settings
    {
        public bool Enabled { get; set; }
        public int Count { get; set; }
        public byte Small { get; set; }
        public Mode Mode { get; set; }
        public string Title { get; set; } = "t";
        public List<string> Names { get; set; } = ["a", "b"];
        public Inner Inner { get; set; } = new();
        public Bounds Range { get; set; }
        public Vector2 Flat { get; set; }
        public Vector3 Position { get; set; }
        public Vector4 Spatial { get; set; }
        public Color Tint { get; set; } = Color.White;
        [Range(0, 1), Tooltip("Loudness")] public float Volume { get; set; }
        [ReadOnly] public int Locked { get; set; } = 3;
        public int Resets;

        [Button] public void Reset() => Resets++;
    }

    sealed class Recording : IPropertyDrawer
    {
        public List<string> Drawn { get; } = [];
        public List<string> ValuesDrawn { get; } = [];

        public void Draw(Gui gui, FormField field, string id, FormRenderContext context) => Drawn.Add(field.Name);

        public bool DrawValue(Gui gui, FormField field, string id, FormRenderContext context)
        {
            ValuesDrawn.Add(field.Name);
            return false;
        }
    }

    sealed class Writing(int value) : IPropertyDrawer
    {
        public void Draw(Gui gui, FormField field, string id, FormRenderContext context)
        {
            if (gui.Pass == Pass.Pass2Render) field.SetValue(value);
        }

        public bool DrawValue(Gui gui, FormField field, string id, FormRenderContext context) => false;
    }

    sealed class Decorating(string name, int order, List<string> calls) : IAttributeDrawer
    {
        public int Order => order;

        public void Draw(Gui gui, FormField field, Attribute attribute, string id, FormRenderContext context,
            Action next)
        {
            calls.Add(name);
            next();
        }
    }
}
