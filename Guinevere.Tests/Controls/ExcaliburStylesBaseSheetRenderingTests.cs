using Autoformers;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Exercises Excalibur controls with a compact standalone base that imports no shipped rules.</summary>
public class ExcaliburStylesBaseSheetRenderingTests
{
    static readonly StyleSheet Base = StyleSheet.Parse("""
        $base-background = #102030; $surface = #29445d; $surface-hover = #385570; $surface-active = #496681;
        $popup = #1b354e; $border = #789abc; $border-active = #abcdef; $divider = #56789a;
        $accent = #ffa040; $accent-hover = #ffb060; $accent-subtle = #704520;
        $text = #fefdfc; $text-dim = #c0b0a0; $text-disabled = #807060; $text-on-accent = #102030;
        $selected = #704520; $positive = #40cc80; $negative = #e06040; $warning = #d0a020; $info = #4090d0;
        $focus-ring = #ffa04080; $text-selection = #ffa04060; $shadow = #00000080; $overlay = #00000080;
        $radius = 0;
        * { color = $text; background-color = $surface; border-color = $border; border-width = 1; }
        button { padding = 6 12; cursor = pointer; }
        button:hover { background-color = $surface-hover; }
        input { cursor = text; }
        checkbox, radio, toggle, dropdown, option, swatch, color-preview { cursor = pointer; }
        checkbox>indicator:checked, radio>indicator:checked, toggle>track:checked { background-color = $selected; }
        radio>dot:checked, toggle>thumb { background-color = $text; }
        slider { flex-direction = row; gap = 6; align-items = center; }
        slider>track { height = 6; }
        slider>thumb { width = 14; height = 14; }
        slider>fill, progress>fill { background-color = $accent; }
        progress { width = expand; height = 6; }
        splitter:horizontal { width = 6; height = 100%; cursor = ew-resize; }
        splitter:vertical { width = 100%; height = 6; cursor = ns-resize; }
        tabbar>tab:selected, tabstrip>tab:selected, option:selected, tree-row:selected { background-color = $selected; }
        color-picker { flex-direction = column; gap = 8; }
        color-picker-row { flex-direction = row; gap = 8; align-items = center; }
        color-hex, color-channels, color-channel, color-field { flex-direction = row; gap = 4; align-items = center; }
        swatches { flex-direction = row; flex-wrap = wrap; gap = 4; }
        color-area { cursor = crosshair; }
        color-strip { cursor = pointer; }
        color-area>cursor { width = 12; height = 12; border-color = #ffffff; border-width = 2; }
        color-strip>thumb { width = 6; height = 6; border-color = #ffffff; border-width = 2; }
        checker { background-color = #eeeeee; color = #aaaaaa; }
        file-header { border-bottom-color = $border; border-bottom-width = 1; }
        dialog-footer { border-top-color = $border; border-top-width = 1; }
        toast>accent { width = 4; }
        toast.info>accent { background-color = $info; }
        toast.positive>accent { background-color = $positive; }
        toast.warning>accent { background-color = $warning; }
        toast.negative>accent { background-color = $negative; }
        form {
            dim-color = $text-dim; accent-color = $accent; divider-color = $divider;
            negative-color = $negative; base-background = $base-background; border-radius = $radius;
        }
        """);

    /// <summary>Every control family renders through the selected standalone base.</summary>
    [Fact]
    public void ControlCatalogUsesTheReplacement()
    {
        Assert.Empty(ExcaliburStyles.ValidateBaseSheet(Base));
        var flag = true;
        var index = 0;
        var text = "Theme";
        var value = 0.5f;
        var color = Color.ParseHex("#4080c0");
        var hsv = Hsva.FromColor(color);
        var axis = Axis.Horizontal;
        var tree = new TreeViewState();
        var dock = new DockLayout { Root = new DockLeaf("panel") };
        var files = new FileDialogState();
        var model = FormBuilder.Build(new FormSettings());
        using var surface = SKSurface.Create(new SKImageInfo(16, 16));
        using var image = surface.Snapshot();
        using var font = new SKFont();
        Action<Gui>[] controls =
        [
            g => g.Button("Button"),
            g => g.IconButton("+"),
            g => g.ImageButton(image, image, image),
            g => g.Checkbox(ref flag, "Checkbox"),
            g => g.RadioButton(ref index, 0, "Radio"),
            g => g.RadioGroup(ref index, [(0, "First"), (1, "Second")]),
            g => g.Toggle(ref flag, "Toggle"),
            g => g.TextInput(ref text),
            g => g.PasswordInput(ref text),
            g => g.TextArea(ref text),
            g => g.NumberField(ref value),
            g => g.Slider(ref value, 0, 1, showValue: true),
            g => g.Dropdown(["First", "Second"], ref index),
            g => g.MultiDropdown<string>(["First", "Second"], ["First"], chips: true),
            g => g.EnumDropdown(ref axis),
            g => g.EnumDropdown(ref axis, EnumPresentation.ToggleButtons),
            g => g.EnumDropdown(ref axis, EnumPresentation.Paging),
            g => g.ObjectField("Object", "object"),
            g => g.ProgressBar(value),
            g => g.Splitter(ref value, Axis.Horizontal),
            g => g.Splitter(ref value, Axis.Vertical),
            g => g.Tabs(ref index, tabs => tabs.Tab("Tab", () => g.Label("Content"))),
            g => g.PillTabs(ref index, tabs => tabs.Tab("Tab", () => g.Label("Content"))),
            g => g.VerticalTabs(ref index, tabs => tabs.Tab("Tab", () => g.Label("Content"))),
            g => g.TabBar(["First", "Second"], ref index),
            g => g.TabStrip([new("tab", "Tab")], "tab"),
            g => g.TreeView(tree, [new("row", "Row", 0)]),
            g => g.DockSpace(dock, _ => new("Panel"), (_, gui) => gui.Label("Content")),
            g => g.Breadcrumb([new("Home", () => { }), new("Page", IsCurrent: true)]),
            g => g.Label("Label"),
            g => g.WrappedLabel("Wrapped label", 14, font),
            g => g.MenuBar(menu => menu.Menu("File", items => items.Item("Open", () => { }))),
            g => g.Flyout(ref flag, new Vector2(20), menu => menu.Item("Open", () => { })),
            g => g.CascadeMenu(ref flag, new Vector2(20), menu => menu.Item("Open", () => { })),
            g => g.ContextMenu(ref flag, menu => menu.Item("Open", () => { })),
            g => g.Popup(ref flag, () => g.Label("Content"), title: "Popup"),
            g => g.ModalPopup(ref flag, () => g.Label("Content"), title: "Modal"),
            g => g.Dialog(ref flag, "Dialog", () => g.Label("Content"), footer: () => g.Button("Close")),
            g => g.Tooltip("Tooltip"),
            g => { using var bar = g.AppBar(); g.Label("Application"); },
            g => g.FileBrowser(files),
            g => g.ColorPicker(ref color),
            g => g.ColorField(ref color),
            g => g.ColorArea(ref hsv),
            g => g.ColorStrip(ref hsv, HsvChannel.Hue),
            g => g.ColorSwatches(ref hsv, [Color.Red, Color.Blue]),
            g => g.ColorPreview(color),
            g => g.ColorHex(ref hsv),
            g => g.ColorChannels(ref hsv),
            g => g.Form(model, "form"),
            g => g.Toasts()
        ];

        foreach (var draw in controls)
        {
            using var harness = new FrameHarness(1000, 700);
            ExcaliburStyles.SetBaseSheet(harness.Gui, Base);
            harness.Gui.Toast("Theme notification");
            harness.Frame(draw);
            harness.Frame(draw);

            Assert.Equal([Base], harness.Gui.StyleSheets);
            Assert.Equal(Color.ParseHex("#fefdfc"), ExcaliburStyles.TokenColor(harness.Gui, "text"));
        }
    }

    /// <summary>The selected base supplies button pixels and supports pointer input.</summary>
    [Fact]
    public void ReplacementDrawsAndHandlesInput()
    {
        using var harness = new FrameHarness(400, 300);
        ExcaliburStyles.SetBaseSheet(harness.Gui, Base);
        harness.Input.MoveTo(new Vector2(350, 250));
        var clicked = false;
        void DrawButton(Gui g) => clicked |= g.Button("Save", width: 200, height: 60);
        harness.Frame(DrawButton);
        using (var snapshot = harness.Snapshot())
        using (var bitmap = SKBitmap.FromImage(snapshot))
            Assert.Equal(SKColor.Parse("#29445d"), bitmap.GetPixel(10, 10));
        harness.Click(DrawButton, new Vector2(10, 10));
        Assert.True(clicked);

        var hsv = new Hsva(0, 0.5f, 0.5f);
        void DrawArea(Gui g) => g.ColorArea(ref hsv, width: 202, height: 102);
        harness.Frame(DrawArea);
        harness.Click(DrawArea, new Vector2(101, 51));
        Assert.InRange(hsv.S, 0.49f, 0.51f);
        Assert.InRange(hsv.V, 0.49f, 0.51f);
        Assert.Equal([Base], harness.Gui.StyleSheets);
    }

    sealed class FormSettings
    {
        /// <summary>A boolean field rendered by the form's choice control.</summary>
        public bool Enabled { get; set; } = true;
        /// <summary>A text field rendered by the form's input control.</summary>
        public string Name { get; set; } = "Theme";
        /// <summary>A color field rendered by the form's color editor.</summary>
        public Color Tint { get; set; } = Color.Red;
    }
}
