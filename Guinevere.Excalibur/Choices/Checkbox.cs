using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// A checkbox styled by the <c>checkbox</c> rules of the GUI's sheets; its box is a <c>checkbox &gt; indicator</c>
    /// child matching <c>:checked</c> or <c>:mixed</c>, and the mark is drawn in the indicator's <c>color</c>.
    /// Clicking the row or pressing Space while focused flips <paramref name="isChecked"/>. The box is the scope's
    /// indicator size unless the sheet sizes the indicator.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="isChecked">The value, flipped when the checkbox is activated.</param>
    /// <param name="label">What follows the box: text, an icon, other widgets or a custom type.</param>
    /// <param name="mixed">Shows the mixed mark and matches <c>:mixed</c> instead of <c>:checked</c>.</param>
    /// <param name="enabled">When false the checkbox matches <c>:disabled</c> and never changes.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Optional id for <c>checkbox#id</c> rules and a stable identity; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>
    /// The checkbox's node, <c>true</c> as a <c>bool</c> on the frame it is activated
    /// (<see cref="LayoutNode.Changed"/> after a chain).
    /// </returns>
    public static ControlNode Checkbox(this Gui gui, ref bool isChecked, Content label = default,
        bool mixed = false,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var size = gui.ControlStyle.IndicatorSize;
        var row = ChoiceRow(gui, "checkbox", enabled, classes, id, filePath, lineNumber);
        using (row.Enter())
        {
            if (ChoiceActivated(gui, row, enabled))
            {
                isChecked = !isChecked;
                row.Raise(NodeSignals.Changed);
            }

            var modifiers = mixed ? MixedModifier : isChecked ? CheckedModifier : NoModifiers;
            using (ChoicePart(gui, "indicator", size, size, modifiers).Enter())
            {
                if (gui.Pass == Pass.Pass2Render && (mixed || isChecked))
                {
                    RenderCheckboxMark(gui, mixed);
                }
            }

            gui.Content(label);
        }

        return row;
    }

    static void RenderCheckboxMark(Gui gui, bool mixed)
    {
        var rect = gui.CurrentNode.Rect;
        var size = Math.Min(rect.W, rect.H);
        var color = gui.CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value;
        if (mixed)
        {
            gui.DrawRect(new Rect(rect.X + rect.W * 0.2f, rect.Y + rect.H * 0.45f, rect.W * 0.6f, rect.H * 0.1f), color);
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
