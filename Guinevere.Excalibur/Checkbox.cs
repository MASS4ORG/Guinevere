using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// A checkbox styled by the <c>checkbox</c> rules of the GUI's sheets; its box is a <c>checkbox &gt; indicator</c>
    /// child matching <c>:checked</c> or <c>:mixed</c>, and the mark is drawn in the indicator's <c>color</c>.
    /// Clicking the row or pressing Space while focused flips <paramref name="isChecked"/>.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="isChecked">The value, flipped when the checkbox is activated.</param>
    /// <param name="label">Text after the box.</param>
    /// <param name="size">Side of the box.</param>
    /// <param name="fontSize">Label size.</param>
    /// <param name="spacing">Gap between the box and the label.</param>
    /// <param name="enabled">When false the checkbox matches <c>:disabled</c> and never changes.</param>
    /// <param name="mixed">Shows the mixed mark and matches <c>:mixed</c> instead of <c>:checked</c>.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Element id for <c>checkbox#id</c> rules.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    public static void Checkbox(this Gui gui, ref bool isChecked, string label = "",
        float size = ControlMetrics.IndicatorSize,
        float fontSize = ControlMetrics.FontSize,
        float spacing = ControlMetrics.Spacing,
        bool enabled = true, bool mixed = false,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        size = gui.ControlStyle.IndicatorSizeOr(size);
        fontSize = gui.ControlStyle.FontSizeOr(fontSize);
        spacing = gui.ControlStyle.SpacingOr(spacing);

        CheckboxCore(gui, ref isChecked, label, size, fontSize, spacing, enabled, mixed, classes, id, filePath,
            lineNumber);
    }

    /// <summary>A checkbox that returns the value after this frame's activation instead of changing a field.</summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="isChecked">The current value.</param>
    /// <param name="label">Text after the box.</param>
    /// <param name="size">Side of the box.</param>
    /// <param name="fontSize">Label size.</param>
    /// <param name="spacing">Gap between the box and the label.</param>
    /// <param name="enabled">When false the checkbox matches <c>:disabled</c> and never changes.</param>
    /// <param name="mixed">Shows the mixed mark and matches <c>:mixed</c> instead of <c>:checked</c>.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Element id for <c>checkbox#id</c> rules.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>The value, flipped on the frame the checkbox is activated.</returns>
    public static bool Checkbox(this Gui gui, bool isChecked, string label = "",
        float size = ControlMetrics.IndicatorSize,
        float fontSize = ControlMetrics.FontSize,
        float spacing = ControlMetrics.Spacing,
        bool enabled = true, bool mixed = false,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        gui.Checkbox(ref isChecked, label, size, fontSize, spacing, enabled, mixed, classes, id, filePath, lineNumber);
        return isChecked;
    }

    static void CheckboxCore(Gui gui, ref bool isChecked, string label, float size, float fontSize, float spacing,
        bool enabled, bool mixed, IReadOnlyList<string>? classes, string? id, string filePath, int lineNumber)
    {
        var row = ChoiceRowNode(gui, "checkbox", label, size, size, fontSize, spacing, enabled, classes, id, filePath,
            lineNumber);
        using (row.Enter())
        {
            if (ChoiceActivated(gui, enabled)) isChecked = !isChecked;
            var modifiers = mixed ? MixedModifier : isChecked ? CheckedModifier : NoModifiers;
            using (ChoicePart(gui, "indicator", size, size, modifiers).Enter())
            {
                if (gui.Pass == Pass.Pass2Render && (mixed || isChecked)) RenderCheckboxMark(gui, size, mixed);
            }
            ChoiceLabel(gui, label, fontSize);
        }
    }

    static void RenderCheckboxMark(Gui gui, float size, bool mixed)
    {
        var rect = gui.CurrentNode.Rect;
        var color = gui.CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value;
        if (mixed)
        {
            gui.DrawRect(new Rect(rect.X + size * 0.2f, rect.Y + size * 0.45f, size * 0.6f, size * 0.1f), color);
            return;
        }

        DrawCheckmark(gui, rect, size, color);
    }

    /// <summary>Draws a two-stroke check mark centred in <paramref name="rect"/>.</summary>
    static void DrawCheckmark(Gui gui, Rect rect, float size, Color color)
    {
        var (centerX, centerY) = (rect.X + rect.W * 0.5f, rect.Y + rect.H * 0.5f);
        var checkSize = size * 0.3f;
        var p1 = new Vector2(centerX - checkSize * 0.5f, centerY);
        var p2 = new Vector2(centerX - checkSize * 0.1f, centerY + checkSize * 0.4f);
        var p3 = new Vector2(centerX + checkSize * 0.6f, centerY - checkSize * 0.4f);
        gui.DrawLine(p1, p2, color, 2f);
        gui.DrawLine(p2, p3, color, 2f);
    }
}
