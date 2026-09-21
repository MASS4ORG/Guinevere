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
        float spacing = 8,
        bool enabled = true)
    {
        ToggleCore(gui, ref isOn, label, width, height, onColor, offColor,
            thumbColor, labelColor, fontSize, spacing, enabled);
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
        float spacing = 8,
        bool enabled = true)
    {
        var temp = isOn;
        ToggleCore(gui, ref temp, label, width, height, onColor, offColor,
            thumbColor, labelColor, fontSize, spacing, enabled);
        return temp;
    }

    static void ToggleCore(Gui gui, ref bool isOn, string label, float width, float height,
        Color? onColor, Color? offColor, Color? thumbColor, Color? labelColor,
        float fontSize, float spacing, bool enabled)
    {
        var totalWidth = CalculateToggleWidth(label, width, fontSize, spacing);
        var totalHeight = Math.Max(height, fontSize + 4);

        using (gui.Node(totalWidth, totalHeight)
                   .Direction(Axis.Horizontal)
                   .Gap(spacing)
                   .Enter())
        {
            HandleToggleInteraction(gui, ref isOn, enabled);
            RenderToggleSwitch(gui, isOn, width, height, onColor, offColor, thumbColor, enabled);
            RenderToggleLabel(gui, label, fontSize, labelColor, enabled);
        }
    }

    static float CalculateToggleWidth(string label, float width, float fontSize, float spacing)
    {
        return string.IsNullOrEmpty(label)
            ? width
            : width + spacing + MeasureTextWidth(new SKFont { Size = fontSize }, label);
    }

    static void HandleToggleInteraction(Gui gui, ref bool isOn, bool enabled)
    {
        if (gui.Pass != Pass.Pass2Render || !enabled) return;

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

    static void RenderToggleSwitch(Gui gui, bool isOn, float width, float height,
        Color? onColor, Color? offColor, Color? thumbColor, bool enabled)
    {
        using (gui.Node(width, height).Enter())
        {
            if (gui.Pass != Pass.Pass2Render) return;

            var rect = gui.CurrentNode.Rect;
            var trackColor = enabled ? GetToggleTrackColor(gui, isOn, onColor, offColor) : gui.Controls.Border;

            gui.DrawBackgroundRect(trackColor, height * 0.5f);

            // Draw stronger focus indicator if this toggle has focus
            if (enabled && gui.HasFocus())
            {
                var focusRect = new Rect(rect.X - 3, rect.Y - 3, rect.W + 6, rect.H + 6);
                gui.DrawRectBorder(focusRect, Color.FromArgb(128, gui.Controls.Accent), 4f, (height * 0.5f) + 4);
                gui.DrawRectBorder(rect, gui.Controls.Accent, 2f, (height * 0.5f) + 2);
            }

            var thumbProps = CalculateThumbProperties(rect, width, height, isOn);
            DrawToggleThumb(gui, thumbProps, enabled ? thumbColor ?? gui.Controls.TextOnAccent : gui.Controls.TextDisabled);
        }
    }

    static void RenderToggleLabel(Gui gui, string label, float fontSize, Color? labelColor,
        bool enabled)
    {
        if (!string.IsNullOrEmpty(label))
        {
            var labelColorFinal = enabled ? labelColor ?? gui.Controls.Text : gui.Controls.TextDisabled;
            gui.DrawText(label, fontSize, labelColorFinal, centerInRect: false);
        }
    }

    static Color GetToggleTrackColor(Gui gui, bool isOn, Color? onColor, Color? offColor)
    {
        var interactable = gui.GetInteractable();
        var isHovered = interactable.OnHover();

        var on = onColor ?? gui.Controls.Selected;
        var off = offColor ?? gui.Controls.Border;

        return (isOn, isHovered) switch
        {
            (true, true) => Lighten(on),
            (true, false) => on,
            (false, true) => Lighten(off),
            (false, false) => off
        };
    }

    static (Vector2 position, float radius) CalculateThumbProperties(
        Rect rect, float width, float height, bool isOn)
    {
        var thumbRadius = height * 0.4f;
        var thumbY = rect.Y + height * 0.5f;
        var thumbX = isOn
            ? rect.X + width - thumbRadius - 2 // Right side when on
            : rect.X + thumbRadius + 2; // Left side when off

        return (new Vector2(thumbX, thumbY), thumbRadius);
    }

    static Color Lighten(Color color) => Color.FromArgb(
        color.A,
        Math.Min(255, color.R + 24),
        Math.Min(255, color.G + 24),
        Math.Min(255, color.B + 24));

    static void DrawToggleThumb(Gui gui, (Vector2 position, float radius) thumbProps, Color thumbColor)
    {
        gui.DrawCircleFilled(thumbProps.position, thumbProps.radius, thumbColor);
        gui.DrawCircleBorder(thumbProps.position, thumbProps.radius, Color.FromArgb(100, 0, 0, 0));
    }
}
