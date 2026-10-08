using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// A switch styled by the <c>toggle</c> rules of the GUI's sheets; its track is a <c>toggle &gt; track</c> child
    /// holding a <c>thumb</c>, both matching <c>:checked</c> while on. Clicking the row or pressing Space while focused
    /// flips <paramref name="isOn"/>.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="isOn">The value, flipped when the switch is activated.</param>
    /// <param name="label">Text after the track.</param>
    /// <param name="width">Track width.</param>
    /// <param name="height">Track height.</param>
    /// <param name="fontSize">Label size.</param>
    /// <param name="spacing">Gap between the track and the label.</param>
    /// <param name="enabled">When false the switch matches <c>:disabled</c> and never changes.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Element id for <c>toggle#id</c> rules.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    public static void Toggle(this Gui gui, ref bool isOn, string label = "",
        float width = 50,
        float height = ControlMetrics.CompactHeight,
        float fontSize = ControlMetrics.FontSize,
        float spacing = ControlMetrics.Spacing,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        height = gui.ControlStyle.CompactHeightOr(height);
        fontSize = gui.ControlStyle.FontSizeOr(fontSize);
        spacing = gui.ControlStyle.SpacingOr(spacing);

        var row = ChoiceRowNode(gui, "toggle", label, width, height, fontSize, spacing, enabled, classes, id, filePath,
            lineNumber);
        using (row.Enter())
        {
            // The thumb's side is layout, settled in the build pass; a click shows on the next frame.
            var thumbSide = isOn ? 1f : 0f;
            if (ChoiceActivated(gui, enabled)) isOn = !isOn;
            var modifiers = isOn ? CheckedModifier : NoModifiers;
            var thumb = height * 0.8f;
            using (ChoicePart(gui, "track", width, height, modifiers).Padding((height - thumb) * 0.5f)
                       .ContentAlignX(thumbSide).ContentAlignY(0.5f).Enter())
            using (ChoicePart(gui, "thumb", thumb, thumb, modifiers).Enter())
            {
            }
            ChoiceLabel(gui, label, fontSize);
        }
    }

    /// <summary>A switch that returns the value after this frame's activation instead of changing a field.</summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="isOn">The current value.</param>
    /// <param name="label">Text after the track.</param>
    /// <param name="width">Track width.</param>
    /// <param name="height">Track height.</param>
    /// <param name="fontSize">Label size.</param>
    /// <param name="spacing">Gap between the track and the label.</param>
    /// <param name="enabled">When false the switch matches <c>:disabled</c> and never changes.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Element id for <c>toggle#id</c> rules.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>The value, flipped on the frame the switch is activated.</returns>
    public static bool Toggle(this Gui gui, bool isOn, string label = "",
        float width = 50,
        float height = ControlMetrics.CompactHeight,
        float fontSize = ControlMetrics.FontSize,
        float spacing = ControlMetrics.Spacing,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        gui.Toggle(ref isOn, label, width, height, fontSize, spacing, enabled, classes, id, filePath, lineNumber);
        return isOn;
    }
}
