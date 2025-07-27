namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// Creates a toggle switch that can be turned on/off with internal state management
    /// </summary>
    public static void Toggle(this Gui gui, ref bool isOn, string label = "",
        float width = 50,
        float height = 24,
        Color? onColor = null,
        Color? offColor = null,
        Color? thumbColor = null,
        Color? labelColor = null,
        float fontSize = 14,
        float spacing = 8)
    {
        ToggleCore(gui, ref isOn, label, width, height, onColor, offColor,
            thumbColor, labelColor, fontSize, spacing);
    }

    /// <summary>
    /// Creates a toggle switch that returns the toggled state without modifying the input
    /// </summary>
    public static bool Toggle(this Gui gui, bool isOn, string label = "",
        float width = 50,
        float height = 24,
        Color? onColor = null,
        Color? offColor = null,
        Color? thumbColor = null,
        Color? labelColor = null,
        float fontSize = 14,
        float spacing = 8)
    {
        var temp = isOn;
        ToggleCore(gui, ref temp, label, width, height, onColor, offColor,
            thumbColor, labelColor, fontSize, spacing);
        return temp;
    }

    private static void ToggleCore(Gui gui, ref bool isOn, string label, float width, float height,
        Color? onColor, Color? offColor, Color? thumbColor, Color? labelColor,
        float fontSize, float spacing)
    {
        var totalWidth = CalculateToggleWidth(label, width, fontSize, spacing);
        var totalHeight = Math.Max(height, fontSize + 4);

        using (gui.Node(totalWidth, totalHeight)
                   .Direction(Axis.Horizontal)
                   .Gap(spacing)
                   .Enter())
        {
            HandleToggleInteraction(gui, ref isOn);
            RenderToggleSwitch(gui, isOn, width, height, onColor, offColor, thumbColor);
            RenderToggleLabel(gui, label, fontSize, labelColor);
        }
    }

    private static float CalculateToggleWidth(string label, float width, float fontSize, float spacing)
    {
        return string.IsNullOrEmpty(label)
            ? width
            : width + spacing + MeasureTextWidth(new SKFont { Size = fontSize }, label);
    }

    private static void HandleToggleInteraction(Gui gui, ref bool isOn)
    {
        if (gui.Pass == Pass.Pass2Render)
        {
            // Register this toggle as focusable
            gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);

            var interactable = gui.GetInteractable();

            // Handle mouse click for focus and toggle
            if (interactable.OnClick())
            {
                gui.RequestFocus(FocusReason.Mouse);
                isOn = !isOn;
            }

            // Handle keyboard interaction for focused toggle
            if (gui.HasFocus() && gui.Input.IsKeyPressed(KeyboardKey.Space))
            {
                isOn = !isOn;
            }
        }
    }

    private static void RenderToggleSwitch(Gui gui, bool isOn, float width, float height,
        Color? onColor, Color? offColor, Color? thumbColor)
    {
        using (gui.Node(width, height).Enter())
        {
            if (gui.Pass != Pass.Pass2Render) return;

            var rect = gui.CurrentNode.Rect;
            var trackColor = GetToggleTrackColor(gui, isOn, onColor, offColor);

            gui.DrawBackgroundRect(trackColor, height * 0.5f);

            // Draw stronger focus indicator if this toggle has focus
            if (gui.HasFocus())
            {
                var focusRect = new Rect(rect.X - 3, rect.Y - 3, rect.W + 6, rect.H + 6);
                gui.DrawRectBorder(focusRect, Color.FromArgb(128, 100, 149, 237), 4f, height * 0.5f + 4); // subtle glow
                gui.DrawRectBorder(rect, Color.FromArgb(255, 100, 149, 237), 2f, height * 0.5f + 2); // strong blue border
            }

            var thumbProps = CalculateThumbProperties(rect, width, height, isOn);
            DrawToggleThumb(gui, thumbProps, thumbColor ?? Color.White);
        }
    }

    private static void RenderToggleLabel(Gui gui, string label, float fontSize, Color? labelColor)
    {
        if (!string.IsNullOrEmpty(label))
        {
            var labelColorFinal = labelColor ?? Color.Black;
            gui.DrawText(label, fontSize, labelColorFinal, centerInRect: false);
        }
    }

    private static Color GetToggleTrackColor(Gui gui, bool isOn, Color? onColor, Color? offColor)
    {
        var interactable = gui.GetInteractable();
        var isHovered = interactable.OnHover();

        return (isOn, isHovered) switch
        {
            (true, true) => Color.FromArgb(255, 102, 187, 106),
            (true, false) => onColor ?? Color.FromArgb(255, 76, 175, 80),
            (false, true) => Color.FromArgb(255, 189, 189, 189),
            (false, false) => offColor ?? Color.FromArgb(255, 158, 158, 158)
        };
    }

    private static (Vector2 position, float radius) CalculateThumbProperties(
        Rect rect, float width, float height, bool isOn)
    {
        var thumbRadius = height * 0.4f;
        var thumbY = rect.Y + height * 0.5f;
        var thumbX = isOn
            ? rect.X + width - thumbRadius - 2 // Right side when on
            : rect.X + thumbRadius + 2; // Left side when off

        return (new Vector2(thumbX, thumbY), thumbRadius);
    }

    private static void DrawToggleThumb(Gui gui, (Vector2 position, float radius) thumbProps, Color thumbColor)
    {
        gui.DrawCircleFilled(thumbProps.position, thumbProps.radius, thumbColor);
        gui.DrawCircleBorder(thumbProps.position, thumbProps.radius, Color.FromArgb(100, 0, 0, 0));
    }
}
