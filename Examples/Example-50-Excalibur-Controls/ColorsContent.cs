using Guinevere;

namespace Controls_01;

public abstract partial class Program
{
    static Color _fieldColor = Color.ParseHex("#a855f7");
    static Color _fieldColor2 = Color.ParseHex("#22c55e80");
    static Color _inlineColor = Color.ParseHex("#3b82f6");
    static Color _inlineOriginal = Color.ParseHex("#3b82f6");
    static Hsva _compact = new(150f, 0.7f, 0.6f);
    static int _shape;

    /// <summary>A PanGui-style compact picker built from parts: a surface over horizontal hue and alpha strips.</summary>
    static readonly ColorPickerOptions CompactPicker = new()
    {
        Width = 180,
        Body = (Gui gui, ref Hsva value, Color? _) =>
        {
            var changed = gui.ColorArea(ref value, width: 0, height: 150);
            changed |= gui.ColorStrip(ref value, HsvChannel.Hue, length: 0);
            changed |= gui.ColorStrip(ref value, HsvChannel.Alpha, length: 0);
            return changed;
        }
    };

    static void ColorsContent(Gui gui)
    {
        using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(10).Padding(10).Enter())
        {
            using (gui.Node().ExpandWidth().Direction(Axis.Vertical).Gap(10).Enter())
            {
                Section(gui, "Color fields", () =>
                {
                    ColorFieldRow(gui, "Accent", ref _fieldColor);
                    ColorFieldRow(gui, "Overlay", ref _fieldColor2);
                    gui.DrawText("Click a swatch to open the picker; the hex text accepts #rgb, #rgba, #rrggbb and " +
                                 "#rrggbbaa, typed or pasted.", 12, Token(gui, "text-dim"));
                });
                Section(gui, "System appearance", () => SystemAppearanceInfo(gui));
            }

            Section(gui, "Inline picker", () =>
            {
                using (gui.Node().Direction(Axis.Horizontal).Gap(6).Height(28).Enter())
                {
                    for (var i = 0; i < ColorPickerShapes.All.Count; i++)
                        if (gui.Button(ColorPickerShapes.All[i].Name, height: 24,
                                classes: i == _shape ? ["color-mode", "checked"] : ["color-mode"]))
                            _shape = i;
                }
                gui.ColorPicker(ref _inlineColor, new ColorPickerOptions { Shape = ColorPickerShapes.All[_shape] },
                    original: _inlineOriginal);
                if (gui.Button("Keep as original", height: 24)) _inlineOriginal = _inlineColor;
            });

            Section(gui, "Picker built from parts", () =>
            {
                gui.ColorPicker(ref _compact, CompactPicker);
                gui.DrawText(_compact.ToColor().ToHex(), 12, Token(gui, "text"));
            });
        }
    }

    static void ColorFieldRow(Gui gui, string label, ref Color color)
    {
        using (gui.Node().ExpandWidth().Height(30).Direction(Axis.Horizontal).Gap(8).AlignContent(0f, 0.5f).Enter())
        {
            using (gui.Node().Width(70).Height(24).Enter())
                gui.DrawText(label, 14, Token(gui, "text"), centerInRect: false);
            gui.ColorField(ref color);
        }
    }

    static void SystemAppearanceInfo(Gui gui)
    {
        var appearance = gui.SystemAppearance;
        gui.DrawText($"Color scheme: {appearance.ColorScheme}", 13, Token(gui, "text"));
        gui.DrawText($"Contrast: {appearance.Contrast}", 13, Token(gui, "text"));
        using (gui.Node().Direction(Axis.Horizontal).Gap(8).Height(24).AlignContent(0f, 0.5f).Enter())
        {
            gui.DrawText($"Accent: {appearance.AccentColor?.ToHex() ?? "none"}", 13, Token(gui, "text"));
            if (appearance.AccentColor is { } accent) gui.ColorPreview(accent, width: 24, height: 18);
        }
        gui.DrawText(_followSystem
                ? "Following the system theme; change the OS appearance to see the gallery switch."
                : "Enable View > Follow system theme to track the OS appearance.",
            12, Token(gui, "text-dim"));
    }
}
