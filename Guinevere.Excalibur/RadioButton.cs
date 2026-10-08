using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// A radio button that selects <paramref name="value"/> within a shared group by writing it into
    /// <paramref name="selectedIndex"/>. Styled by the <c>radio</c> rules; its circle is a <c>radio &gt; indicator</c>
    /// child holding a <c>dot</c>, both matching <c>:checked</c> while the value is selected.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="selectedIndex">The group's selection, set to <paramref name="value"/> when activated.</param>
    /// <param name="value">This radio's value.</param>
    /// <param name="label">Text after the circle.</param>
    /// <param name="size">Diameter of the circle.</param>
    /// <param name="fontSize">Label size.</param>
    /// <param name="spacing">Gap between the circle and the label.</param>
    /// <param name="enabled">When false the radio matches <c>:disabled</c> and never selects.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Element id for <c>radio#id</c> rules.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    public static void RadioButton(this Gui gui, ref int selectedIndex, int value, string label = "",
        float size = ControlMetrics.IndicatorSize,
        float fontSize = ControlMetrics.FontSize,
        float spacing = ControlMetrics.Spacing,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        size = gui.ControlStyle.IndicatorSizeOr(size);
        fontSize = gui.ControlStyle.FontSizeOr(fontSize);
        spacing = gui.ControlStyle.SpacingOr(spacing);

        var row = ChoiceRowNode(gui, "radio", label, size, size, fontSize, spacing, enabled, classes, id, filePath,
            lineNumber);
        using (row.Enter())
        {
            if (ChoiceActivated(gui, enabled)) selectedIndex = value;
            var modifiers = selectedIndex == value ? CheckedModifier : NoModifiers;
            using (ChoicePart(gui, "indicator", size, size, modifiers).ContentAlignX(0.5f).ContentAlignY(0.5f)
                       .Enter())
            using (ChoicePart(gui, "dot", size * 0.4f, size * 0.4f, modifiers).Enter())
            {
            }
            ChoiceLabel(gui, label, fontSize);
        }
    }

    /// <summary>
    /// A radio button that reports whether its value is selected, without letting the caller mutate the group index
    /// in place.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="selectedIndex">The group's selection.</param>
    /// <param name="value">This radio's value.</param>
    /// <param name="label">Text after the circle.</param>
    /// <param name="size">Diameter of the circle.</param>
    /// <param name="fontSize">Label size.</param>
    /// <param name="spacing">Gap between the circle and the label.</param>
    /// <param name="enabled">When false the radio matches <c>:disabled</c> and never selects.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Element id for <c>radio#id</c> rules.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>Whether <paramref name="value"/> is selected after this frame's activation.</returns>
    public static bool RadioButton(this Gui gui, int selectedIndex, int value, string label = "",
        float size = ControlMetrics.IndicatorSize,
        float fontSize = ControlMetrics.FontSize,
        float spacing = ControlMetrics.Spacing,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        gui.RadioButton(ref selectedIndex, value, label, size, fontSize, spacing, enabled, classes, id, filePath,
            lineNumber);
        return selectedIndex == value;
    }

    /// <summary>
    /// Lays a set of radios out vertically and binds them to one selection index. Each entry supplies the radio's
    /// value and label.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="selectedIndex">The group's selection.</param>
    /// <param name="options">Value and label of each radio, top to bottom.</param>
    /// <param name="size">Diameter of each circle.</param>
    /// <param name="fontSize">Label size.</param>
    /// <param name="spacing">Gap between each circle and its label.</param>
    /// <param name="gap">Gap between rows.</param>
    /// <param name="enabled">When false every radio matches <c>:disabled</c>.</param>
    /// <param name="classes">Extra classes for every radio.</param>
    public static void RadioGroup(this Gui gui, ref int selectedIndex,
        IReadOnlyList<(int Value, string Label)> options,
        float size = ControlMetrics.IndicatorSize,
        float fontSize = ControlMetrics.FontSize,
        float spacing = ControlMetrics.Spacing,
        float gap = ControlMetrics.Spacing,
        bool enabled = true,
        IReadOnlyList<string>? classes = null)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(options);
        gap = gui.ControlStyle.SpacingOr(gap);

        using (gui.Node().Direction(Axis.Vertical).Gap(gap).Enter())
        {
            foreach (var (value, label) in options)
                gui.RadioButton(ref selectedIndex, value, label, size, fontSize, spacing, enabled, classes);
        }
    }
}
