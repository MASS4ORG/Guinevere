namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// Creates a checkbox that can be toggled on/off with internal state management
    /// </summary>
    public static void Checkbox(this Gui gui, ref bool isChecked, string label = "",
        float size = 20,
        Color? backgroundColor = null,
        Color? checkColor = null,
        Color? borderColor = null,
        Color? labelColor = null,
        float fontSize = 14,
        float spacing = 8,
        bool enabled = true)
    {
        CheckboxCore(gui, ref isChecked, label, size, backgroundColor, checkColor,
            borderColor, labelColor, fontSize, spacing, enabled);
    }

    /// <summary>
    /// Creates a checkbox that returns the toggled state without modifying the input
    /// </summary>
    public static bool Checkbox(this Gui gui, bool isChecked, string label = "",
        float size = 20,
        Color? backgroundColor = null,
        Color? checkColor = null,
        Color? borderColor = null,
        Color? labelColor = null,
        float fontSize = 14,
        float spacing = 8,
        bool enabled = true)
    {
        var temp = isChecked;
        CheckboxCore(gui, ref temp, label, size, backgroundColor, checkColor,
            borderColor, labelColor, fontSize, spacing, enabled);
        return temp;
    }

    static void CheckboxCore(Gui gui, ref bool isChecked, string label, float size,
        Color? backgroundColor, Color? checkColor, Color? borderColor, Color? labelColor,
        float fontSize, float spacing, bool enabled)
    {
        var totalWidth = CalculateCheckboxWidth(label, size, fontSize, spacing);
        var totalHeight = Math.Max(size, fontSize + 4);

        using (gui.Node(totalWidth, totalHeight)
                   .Direction(Axis.Horizontal)
                   .Gap(spacing)
                   .Enter())
        {
            HandleCheckboxInteraction(gui, ref isChecked, enabled);
            RenderCheckboxSquare(gui, isChecked, size, backgroundColor, checkColor, borderColor, enabled);
            RenderCheckboxLabel(gui, label, fontSize, labelColor, enabled);
        }
    }

    static float CalculateCheckboxWidth(string label, float size, float fontSize, float spacing)
    {
        return string.IsNullOrEmpty(label)
            ? size
            : size + spacing + MeasureTextWidth(new SKFont { Size = fontSize }, label);
    }

    static void HandleCheckboxInteraction(Gui gui, ref bool isChecked, bool enabled)
    {
        if (gui.Pass != Pass.Pass2Render || !enabled) return;

        // Register this checkbox as focusable
        gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);

        var interactable = gui.GetInteractable();

        // Handle mouse click for focus and toggle
        if (interactable.OnClick())
        {
            gui.RequestFocus(FocusReason.Mouse);
            isChecked = !isChecked;
        }

        // Handle keyboard interaction for focused checkbox
        if (gui.HasFocus() && gui.Input.IsKeyPressed(KeyboardKey.Space))
        {
            isChecked = !isChecked;
        }
    }

    static void RenderCheckboxSquare(Gui gui, bool isChecked, float size,
        Color? backgroundColor, Color? checkColor, Color? borderColor, bool enabled)
    {
        using (gui.Node(size, size).Enter())
        {
            if (gui.Pass != Pass.Pass2Render) return;

            var rect = gui.CurrentNode.Rect;
            var bgColor = GetCheckboxBackgroundColor(gui, isChecked, backgroundColor);
            var borderColorFinal = enabled ? borderColor ?? gui.Controls.Border : DisabledBorder;

            if (enabled && gui.HasFocus())
            {
                var focusRect = new Rect(rect.X - 3, rect.Y - 3, rect.W + 6, rect.H + 6);
                gui.DrawRectBorder(focusRect, Color.FromArgb(128, gui.Controls.Accent), 4f, 4);
                gui.DrawBackgroundRect(bgColor, 2);
                gui.DrawRectBorder(rect, gui.Controls.Accent, 2f, 2);
            }
            else
            {
                gui.DrawBackgroundRect(enabled ? bgColor : DisabledFill, 2);
                gui.DrawRectBorder(rect, borderColorFinal, 1f, 2);
            }

            if (isChecked)
                DrawCheckmark(gui, rect, size, enabled ? checkColor ?? gui.Controls.Knob : DisabledText);
        }
    }

    static void RenderCheckboxLabel(Gui gui, string label, float fontSize, Color? labelColor,
        bool enabled)
    {
        if (!string.IsNullOrEmpty(label))
        {
            var labelColorFinal = enabled ? labelColor ?? gui.Controls.Text : DisabledText;
            gui.DrawText(label, fontSize, labelColorFinal, centerInRect: false);
        }
    }

    static Color GetCheckboxBackgroundColor(Gui gui, bool isChecked, Color? backgroundColor)
    {
        return backgroundColor ?? (isChecked ? gui.Controls.Selected : gui.Controls.Surface);
    }

    static void DrawCheckmark(Gui gui, Rect rect, float size, Color checkColor)
    {
        var (centerX, centerY) = (rect.X + rect.W * 0.5f, rect.Y + rect.H * 0.5f);
        var checkSize = size * 0.3f;

        var points = CalculateCheckmarkPoints(centerX, centerY, checkSize);

        gui.DrawLine(points.p1, points.p2, checkColor, 2f);
        gui.DrawLine(points.p2, points.p3, checkColor, 2f);
    }

    static (Vector2 p1, Vector2 p2, Vector2 p3) CalculateCheckmarkPoints(
        float centerX, float centerY, float checkSize)
    {
        return (
            new Vector2(centerX - checkSize * 0.5f, centerY),
            new Vector2(centerX - checkSize * 0.1f, centerY + checkSize * 0.4f),
            new Vector2(centerX + checkSize * 0.6f, centerY - checkSize * 0.4f)
        );
    }
}
