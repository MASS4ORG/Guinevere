using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    static readonly Size DotFraction = Size.Percentage(0.4f);

    /// <summary>
    /// A radio button that selects <paramref name="value"/> within a shared group by writing it into
    /// <paramref name="selectedIndex"/>. Styled by the <c>radio</c> rules; its circle is a <c>radio &gt; indicator</c>
    /// child holding a <c>dot</c>, both matching <c>:checked</c> while the value is selected.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="selectedIndex">The group's selection, set to <paramref name="value"/> when activated.</param>
    /// <param name="value">This radio's value.</param>
    /// <param name="label">What follows the circle: text, an icon, other widgets or a custom type.</param>
    /// <param name="enabled">When false the radio matches <c>:disabled</c> and never selects.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Optional id for <c>radio#id</c> rules and a stable identity; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>
    /// The radio's node, <c>true</c> as a <c>bool</c> on the frame it is activated; <see cref="LayoutNode.Changed"/>
    /// only when that moved the selection to <paramref name="value"/>.
    /// </returns>
    public static ControlNode RadioButton(this Gui gui, ref int selectedIndex, int value, Content label = default,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var size = gui.ControlStyle.IndicatorSize;
        var row = ChoiceRow(gui, "radio", enabled, classes, id, filePath, lineNumber);
        using (row.Enter())
        {
            if (ChoiceActivated(gui, row, enabled) && selectedIndex != value)
            {
                selectedIndex = value;
                row.Raise(NodeSignals.Changed);
            }

            var modifiers = selectedIndex == value ? CheckedModifier : NoModifiers;
            using (ChoicePart(gui, "indicator", size, size, modifiers).AlignContent(0.5f).Enter())
            using (ChoicePart(gui, "dot", DotFraction, DotFraction, modifiers).Enter())
            {
            }

            gui.Content(label);
        }

        return row;
    }

    /// <summary>
    /// Radios laid out vertically in a <c>radio-group</c> control, bound to one selection index. Each entry supplies
    /// the radio's value and label; the gap between rows is the scope's control spacing unless set on the node.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="selectedIndex">The group's selection.</param>
    /// <param name="options">Value and label of each radio, top to bottom.</param>
    /// <param name="enabled">When false every radio matches <c>:disabled</c>.</param>
    /// <param name="classes">Extra classes for the group, so <c>radio-group.x radio</c> rules reach its radios.</param>
    /// <param name="id">Optional id for <c>radio-group#id</c> rules and a stable identity.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>The group's node, <c>true</c> as a <c>bool</c> on the frame the selection changes.</returns>
    public static ControlNode RadioGroup(this Gui gui, ref int selectedIndex,
        IReadOnlyList<(int Value, Content Label)> options,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(options);
        ExcaliburStyles.Ensure(gui);
        var gap = gui.ControlStyle.Spacing;
        var group = gui.StyledControl("radio-group", classes, id, NoModifiers, disabled: !enabled,
            filePath: filePath, lineNumber: lineNumber);
        group.Direction(Axis.Vertical).Gap(gap);
        using (group.Enter())
        {
            foreach (var (value, label) in options)
            {
                if (gui.RadioButton(ref selectedIndex, value, label, enabled).Changed)
                {
                    group.Raise(NodeSignals.Changed);
                }
            }
        }

        return group;
    }
}
