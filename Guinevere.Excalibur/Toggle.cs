using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>Width of a toggle's track relative to its height.</summary>
    const float ToggleAspect = 50f / ControlMetrics.CompactHeight;

    /// <summary>
    /// A switch styled by the <c>toggle</c> rules of the GUI's sheets; its track is a <c>toggle &gt; track</c> child
    /// holding a <c>thumb</c>, both matching <c>:checked</c> while on. Clicking the row or pressing Space while focused
    /// flips <paramref name="isOn"/>. The track is the scope's compact height tall.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="isOn">The value, flipped when the switch is activated.</param>
    /// <param name="label">What follows the track: text, an icon, other widgets or a custom type.</param>
    /// <param name="enabled">When false the switch matches <c>:disabled</c> and never changes.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Optional id for <c>toggle#id</c> rules and a stable identity; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>
    /// The switch's node, <c>true</c> as a <c>bool</c> on the frame it is activated
    /// (<see cref="LayoutNode.Changed"/> after a chain).
    /// </returns>
    public static ControlNode Toggle(this Gui gui, ref bool isOn, Content label = default,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var height = gui.ControlStyle.CompactHeight;
        var row = ChoiceRow(gui, "toggle", enabled, classes, id, filePath, lineNumber);
        using (row.Enter())
        {
            // The thumb's side is layout, settled in the build pass; a click shows on the next frame.
            var thumbSide = isOn ? 1f : 0f;
            if (ChoiceActivated(gui, row, enabled))
            {
                isOn = !isOn;
                row.Raise(NodeSignals.Changed);
            }

            var modifiers = isOn ? CheckedModifier : NoModifiers;
            var thumb = height * 0.8f;
            using (ChoicePart(gui, "track", height * ToggleAspect, height, modifiers).Padding((height - thumb) * 0.5f)
                       .AlignContentX(thumbSide).AlignContentY(0.5f).Enter())
            using (ChoicePart(gui, "thumb", thumb, thumb, modifiers).Enter())
            {
            }

            gui.Content(label);
        }

        return row;
    }
}
