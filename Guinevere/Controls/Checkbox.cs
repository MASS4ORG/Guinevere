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
        float spacing = 8)
    {
        CheckboxCore(gui, ref isChecked, label, size, backgroundColor, checkColor,
            borderColor, labelColor, fontSize, spacing);
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
        float spacing = 8)
    {
        var temp = isChecked;
        CheckboxCore(gui, ref temp, label, size, backgroundColor, checkColor,
            borderColor, labelColor, fontSize, spacing);
        return temp;
    }

    private static void CheckboxCore(Gui gui, ref bool isChecked, string label, float size,
        Color? backgroundColor, Color? checkColor, Color? borderColor, Color? labelColor,
        float fontSize, float spacing)
    {
        var totalWidth = CalculateCheckboxWidth(label, size, fontSize, spacing);
        var totalHeight = Math.Max(size, fontSize + 4);

        using (gui.Node(totalWidth, totalHeight)
                   .Direction(Axis.Horizontal)
                   .Gap(spacing)
                   .Enter())
        {
            HandleCheckboxInteraction(gui, ref isChecked);
            RenderCheckboxSquare(gui, isChecked, size, backgroundColor, checkColor, borderColor);
            RenderCheckboxLabel(gui, label, fontSize, labelColor);
        }
    }

    private static float CalculateCheckboxWidth(string label, float size, float fontSize, float spacing)
    {
        return string.IsNullOrEmpty(label)
            ? size
            : size + spacing + MeasureTextWidth(new SKFont { Size = fontSize }, label);
    }

    private static void HandleCheckboxInteraction(Gui gui, ref bool isChecked)
    {
        if (gui.Pass == Pass.Pass2Render)
        {
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
    }

    private static void RenderCheckboxSquare(Gui gui, bool isChecked, float size,
        Color? backgroundColor, Color? checkColor, Color? borderColor)
    {
        using (gui.Node(size, size).Enter())
        {
            if (gui.Pass != Pass.Pass2Render) return;

            var rect = gui.CurrentNode.Rect;
            var bgColor = GetCheckboxBackgroundColor(isChecked, backgroundColor);
            var borderColorFinal = borderColor ?? Color.Gray;

            // Use stronger border and subtle glow if focused
            if (gui.HasFocus())
            {
                var focusRect = new Rect(rect.X - 3, rect.Y - 3, rect.W + 6, rect.H + 6);
                gui.DrawRectBorder(focusRect, Color.FromArgb(128, 100, 149, 237), 4f, 4); // subtle glow
                gui.DrawBackgroundRect(bgColor, 2);
                gui.DrawRectBorder(rect, Color.FromArgb(255, 100, 149, 237), 2f, 2); // strong blue border
            }
            else
            {
                gui.DrawBackgroundRect(bgColor, 2);
                gui.DrawRectBorder(rect, borderColorFinal, 1f, 2);
            }

            if (isChecked)
                DrawCheckmark(gui, rect, size, checkColor ?? Color.White);
        }
    }

    private static void RenderCheckboxLabel(Gui gui, string label, float fontSize, Color? labelColor)
    {
        if (!string.IsNullOrEmpty(label))
        {
            var labelColorFinal = labelColor ?? Color.Black;
            gui.DrawText(label, fontSize, labelColorFinal, centerInRect: false);
        }
    }

    private static Color GetCheckboxBackgroundColor(bool isChecked, Color? backgroundColor)
    {
        return backgroundColor ?? (isChecked ? Color.FromArgb(255, 100, 149, 237) : Color.White);
    }

    private static void DrawCheckmark(Gui gui, Rect rect, float size, Color checkColor)
    {
        var (centerX, centerY) = (rect.X + rect.W * 0.5f, rect.Y + rect.H * 0.5f);
        var checkSize = size * 0.3f;

        var points = CalculateCheckmarkPoints(centerX, centerY, checkSize);

        gui.DrawLine(points.p1, points.p2, checkColor, 2f);
        gui.DrawLine(points.p2, points.p3, checkColor, 2f);
    }

    private static (Vector2 p1, Vector2 p2, Vector2 p3) CalculateCheckmarkPoints(
        float centerX, float centerY, float checkSize)
    {
        return (
            new Vector2(centerX - checkSize * 0.5f, centerY),
            new Vector2(centerX - checkSize * 0.1f, centerY + checkSize * 0.4f),
            new Vector2(centerX + checkSize * 0.6f, centerY - checkSize * 0.4f)
        );
    }
}
