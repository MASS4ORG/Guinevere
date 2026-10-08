using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Golden;

/// <summary>
/// Golden images of the Excalibur controls in their main states, using bundled fonts and a fixed frame interval.
/// </summary>
public class ControlGoldenTests
{
    /// <summary>Where the pointer is during a capture.</summary>
    public enum Pointer { Away, Over, Pressed }

    static readonly Font Text = Font.FromFile(Fonts("Roboto-Regular.ttf"));
    static readonly Font Emoji = Font.FromFile(Fonts("NotoEmoji-VariableFont_wght.ttf"));
    static readonly Font WidgetIcons = Font.FromFile(Fonts("widget-icons.ttf"));

    static string Fonts(string file) =>
        Path.Combine(GoldenImage.RepositoryRoot(), "Integrations", "Resources", "Fonts", file);

    /// <summary>Every control state captured, keyed by its golden name.</summary>
    static readonly Dictionary<string, (int Width, int Height, Pointer Pointer, Action<Gui> Draw)> Cases = new()
    {
        ["button-normal"] = (160, 60, Pointer.Away, static g => g.Button("Save")),
        ["button-hover"] = (160, 60, Pointer.Over, static g => g.Button("Save")),
        ["button-pressed"] = (160, 60, Pointer.Pressed, static g => g.Button("Save")),
        ["button-disabled"] = (160, 60, Pointer.Away, static g => g.Button("Save", enabled: false)),
        ["button-sized"] = (200, 70, Pointer.Away, static g => g.Button("Wide", width: 160, height: 40)),
        ["button-primary"] = (160, 60, Pointer.Away, static g => g.Button("Save", classes: ["primary"])),
        ["button-themed"] = (160, 60, Pointer.Away, static g =>
        {
            g.SetStyleToken("surface", Color.FromArgb(255, 40, 44, 52));
            g.SetStyleToken("text", Color.White);
            g.Button("Save");
        }
        ),
        ["icon-button-checked"] = (60, 60, Pointer.Away, static g => g.IconButton("⚙", classes: ["checked"])),
        ["icon-button-normal"] = (60, 60, Pointer.Away, static g => g.IconButton("⚙")),
        ["icon-button-hover"] = (60, 60, Pointer.Over, static g => g.IconButton("⚙")),
        ["checkbox-off"] = (160, 50, Pointer.Away, static g => g.Checkbox(false, "Option")),
        ["checkbox-on"] = (160, 50, Pointer.Away, static g => g.Checkbox(true, "Option")),
        ["checkbox-hover"] = (160, 50, Pointer.Over, static g => g.Checkbox(false, "Option")),
        ["radio-off"] = (160, 50, Pointer.Away, static g => g.RadioButton(0, 1, "Choice")),
        ["radio-on"] = (160, 50, Pointer.Away, static g => g.RadioButton(1, 1, "Choice")),
        ["toggle-off"] = (160, 50, Pointer.Away, static g => g.Toggle(false, "Power")),
        ["toggle-on"] = (160, 50, Pointer.Away, static g => g.Toggle(true, "Power")),
        ["checkbox-mixed"] = (160, 50, Pointer.Away, static g => g.Checkbox(false, "Option", mixed: true)),
        ["checkbox-disabled"] = (160, 50, Pointer.Away, static g => g.Checkbox(true, "Option", enabled: false)),
        ["checkbox-pressed"] = (160, 50, Pointer.Pressed, static g => g.Checkbox(false, "Option")),
        ["radio-disabled"] = (160, 50, Pointer.Away, static g => g.RadioButton(1, 1, "Choice", enabled: false)),
        ["toggle-disabled"] = (160, 50, Pointer.Away, static g => g.Toggle(true, "Power", enabled: false)),
        ["text-input-empty"] = (240, 60, Pointer.Away, static g => g.TextInput("", placeholder: "Name")),
        ["text-input-filled"] = (240, 60, Pointer.Away, static g => g.TextInput("Guinevere")),
        ["text-input-disabled"] = (240, 60, Pointer.Away, static g => g.TextInput("Guinevere", enabled: false)),
        ["text-input-focused"] = (240, 60, Pointer.Pressed, static g => g.TextInput("", placeholder: "Name")),
        ["slider"] = (240, 50, Pointer.Away, static g => Slide(g)),
        ["slider-hover"] = (240, 50, Pointer.Over, static g => Slide(g)),
        ["slider-pressed"] = (240, 50, Pointer.Pressed, static g => Slide(g)),
        ["slider-disabled"] = (240, 50, Pointer.Away, static g => Slide(g, enabled: false)),
        ["slider-sized"] = (240, 50, Pointer.Away, static g => Slide(g, width: 160, height: 12)),
        ["progress"] = (240, 40, Pointer.Away, static g => g.ProgressBar(0.4f, 200)),
        ["progress-empty"] = (240, 40, Pointer.Away, static g => g.ProgressBar(0, 200)),
        ["progress-full"] = (240, 40, Pointer.Away, static g => g.ProgressBar(1, 200)),
        ["progress-indeterminate"] = (240, 40, Pointer.Away, static g => g.ProgressBar(null, 200)),
        ["splitter-horizontal"] = (240, 50, Pointer.Away, static g => Split(g, Axis.Horizontal)),
        ["splitter-vertical"] = (240, 50, Pointer.Away, static g => Split(g, Axis.Vertical)),
        ["splitter-hover"] = (240, 50, Pointer.Over, static g => Split(g, Axis.Vertical)),
        ["splitter-pressed"] = (240, 50, Pointer.Pressed, static g => Split(g, Axis.Vertical)),
        ["number-field"] = (240, 60, Pointer.Away, static g => Number(g)),
        ["dropdown-closed"] = (240, 60, Pointer.Away, static g => g.Dropdown(["One", "Two"], 0)),
        ["dropdown-placeholder"] = (240, 60, Pointer.Away, static g => g.Dropdown(["One", "Two"], enabled: false)),
        ["tabs"] = (300, 120, Pointer.Away, static g => g.Tabs(0, tabs => tabs.Tab("First").Tab("Second").DisabledTab("Third"))),
        ["label"] = (200, 40, Pointer.Away, static g => g.Label("Label text")),
    };

    static void Slide(Gui gui, bool enabled = true, float width = ControlMetrics.FieldWidth,
        float height = ControlMetrics.CompactHeight)
    {
        var value = 0.3f;
        gui.Slider(ref value, 0f, 1f, width: width, height: height, enabled: enabled);
    }

    static void Split(Gui gui, Axis axis)
    {
        var fraction = 0.5f;
        using (gui.Node(200, 30).Direction(axis).Enter()) gui.Splitter(ref fraction, axis);
    }

    static void Number(Gui gui)
    {
        var value = 42.5f;
        gui.NumberField(ref value);
    }

    /// <summary>The golden names, for the theory.</summary>
    public static TheoryData<string> Names => [.. Cases.Keys];

    /// <summary>Each control state renders exactly like its golden.</summary>
    [Theory]
    [MemberData(nameof(Names))]
    public void Control_MatchesGolden(string name)
    {
        var (width, height, pointer, draw) = Cases[name];
        using var harness = new FrameHarness(width, height);
        harness.Gui.ConfigureFonts(Text, Emoji, WidgetIcons);

        void Frame(Gui gui)
        {
            gui.DrawBackgroundRect(Color.White);
            using (gui.Node(width, height).Padding(10).Enter()) draw(gui);
        }

        var inside = name.StartsWith("splitter", StringComparison.Ordinal) ? new Vector2(13, 13) : new Vector2(18, 18);
        harness.Input.MoveTo(pointer == Pointer.Away ? new Vector2(-50, -50) : inside);
        if (pointer == Pointer.Pressed) harness.Input.PressButton(MouseButton.Left);
        for (var i = 0; i < 3; i++) harness.Frame(Frame);

        using var image = harness.Snapshot();
        GoldenImage.AssertMatches(Path.Combine("Controls", name), image);
    }
}
