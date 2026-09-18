namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// Creates a radio button that selects <paramref name="value"/> within a shared group by writing
    /// it into <paramref name="selectedIndex"/>. Only the radio whose value matches the current
    /// selection shows a filled dot.
    /// </summary>
    public static void RadioButton(this Gui gui, ref int selectedIndex, int value, string label = "",
        float size = 20,
        Color? backgroundColor = null,
        Color? selectedColor = null,
        Color? borderColor = null,
        Color? labelColor = null,
        float fontSize = 14,
        float spacing = 8,
        bool enabled = true)
    {
        RadioButtonCore(gui, ref selectedIndex, value, label, size, backgroundColor, selectedColor,
            borderColor, labelColor, fontSize, spacing, enabled);
    }

    /// <summary>
    /// Creates a radio button that reports whether its value is selected, without letting the caller
    /// mutate the group index in place.
    /// </summary>
    public static bool RadioButton(this Gui gui, int selectedIndex, int value, string label = "",
        float size = 20,
        Color? backgroundColor = null,
        Color? selectedColor = null,
        Color? borderColor = null,
        Color? labelColor = null,
        float fontSize = 14,
        float spacing = 8,
        bool enabled = true)
    {
        var index = selectedIndex;
        RadioButtonCore(gui, ref index, value, label, size, backgroundColor, selectedColor,
            borderColor, labelColor, fontSize, spacing, enabled);
        return index == value;
    }

    /// <summary>
    /// Lays a set of radios out vertically and binds them to one selection index. Each entry supplies
    /// the radio's value and label.
    /// </summary>
    public static void RadioGroup(this Gui gui, ref int selectedIndex,
        IReadOnlyList<(int Value, string Label)> options,
        float size = 20,
        Color? backgroundColor = null,
        Color? selectedColor = null,
        Color? borderColor = null,
        Color? labelColor = null,
        float fontSize = 14,
        float spacing = 8,
        float gap = 8,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(options);

        using (gui.Node().Direction(Axis.Vertical).Gap(gap).Enter())
        {
            foreach (var (value, label) in options)
                gui.RadioButton(ref selectedIndex, value, label, size, backgroundColor, selectedColor,
                    borderColor, labelColor, fontSize, spacing, enabled);
        }
    }

    static void RadioButtonCore(Gui gui, ref int selectedIndex, int value, string label,
        float size, Color? backgroundColor, Color? selectedColor, Color? borderColor, Color? labelColor,
        float fontSize, float spacing, bool enabled)
    {
        var totalWidth = CalculateRadioWidth(label, size, fontSize, spacing);
        var totalHeight = Math.Max(size, fontSize + 4);

        using (gui.Node(totalWidth, totalHeight)
                   .Direction(Axis.Horizontal)
                   .Gap(spacing)
                   .Enter())
        {
            HandleRadioInteraction(gui, ref selectedIndex, value, enabled);
            RenderRadioCircle(gui, selectedIndex == value, size, backgroundColor, selectedColor,
                borderColor, enabled);
            RenderRadioLabel(gui, label, fontSize, labelColor, enabled);
        }
    }

    static float CalculateRadioWidth(string label, float size, float fontSize, float spacing)
    {
        return string.IsNullOrEmpty(label)
            ? size
            : size + spacing + MeasureTextWidth(new SKFont { Size = fontSize }, label);
    }

    static void HandleRadioInteraction(Gui gui, ref int selectedIndex, int value, bool enabled)
    {
        if (gui.Pass != Pass.Pass2Render || !enabled) return;

        gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
        var interactable = gui.GetInteractable();

        if (interactable.OnClick())
        {
            gui.RequestFocus(FocusReason.Mouse);
            selectedIndex = value;
        }
        else if (gui.HasFocus() && gui.Input.IsKeyPressed(KeyboardKey.Space))
        {
            selectedIndex = value;
        }
    }

    static void RenderRadioCircle(Gui gui, bool isSelected, float size,
        Color? backgroundColor, Color? selectedColor, Color? borderColor, bool enabled)
    {
        using (gui.Node(size, size).Enter())
        {
            if (gui.Pass != Pass.Pass2Render) return;

            var rect = gui.CurrentNode.Rect;
            var centre = new Vector2(rect.X + rect.W * 0.5f, rect.Y + rect.H * 0.5f);
            var radius = size * 0.32f;

            var border = enabled ? borderColor ?? gui.Controls.Border : DisabledBorder;
            var fill = enabled
                ? backgroundColor ?? gui.Controls.Surface
                : DisabledFill;

            if (enabled && gui.HasFocus())
                gui.DrawCircleBorder(centre, radius + 3.5f, Color.FromArgb(128, gui.Controls.Accent), 3f);

            gui.DrawCircleFilled(centre, radius, fill);

            var accent = enabled
                ? selectedColor ?? gui.Controls.Accent
                : DisabledBorder;
            gui.DrawCircleBorder(centre, radius, isSelected ? accent : border, isSelected ? 2f : 1f);

            if (isSelected)
            {
                var dotColor = enabled ? gui.Controls.Knob : DisabledText;
                gui.DrawCircleFilled(centre, radius * 0.42f, dotColor);
            }
        }
    }

    static void RenderRadioLabel(Gui gui, string label, float fontSize, Color? labelColor,
        bool enabled)
    {
        if (string.IsNullOrEmpty(label)) return;

        var color = enabled
            ? labelColor ?? gui.Controls.Text
            : DisabledText;
        gui.DrawText(label, fontSize, color, centerInRect: false);
    }
}
