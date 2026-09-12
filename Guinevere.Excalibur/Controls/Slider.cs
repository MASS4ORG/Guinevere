using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    private sealed class SliderState
    {
        public bool Dragging;
    }

    /// <summary>
    /// A numeric slider. The caller owns the value (like <c>Toggle</c>), so the control never fights an
    /// external databind. Clicking anywhere on the track jumps there, dragging keeps scrubbing even when
    /// the pointer leaves the widget, and the keyboard arrows push the value by one <paramref name="step"/>
    /// when the slider has focus. Pass <paramref name="step"/> larger than zero to snap to multiples.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="value">The current value; clamped into [<paramref name="min"/>, <paramref name="max"/>].</param>
    /// <param name="min">Lower bound.</param>
    /// <param name="max">Upper bound.</param>
    /// <param name="width">Node width. The value label, when shown, adds its own width after the track.</param>
    /// <param name="height">Node height.</param>
    /// <param name="step">Snap interval, or 0 to move continuously.</param>
    /// <param name="trackColor">The groove colour; defaults to the palette border.</param>
    /// <param name="fillColor">The filled portion colour; defaults to the palette accent.</param>
    /// <param name="thumbColor">The thumb colour; defaults to the palette knob.</param>
    /// <param name="showValue">Whether to draw the numeric value to the right of the track.</param>
    /// <param name="fontSize">Font size of the value label.</param>
    /// <param name="enabled">Whether the slider responds to input.</param>
    /// <param name="filePath">Captured by the compiler; makes this call site's slider a unique control.</param>
    /// <param name="lineNumber">Captured by the compiler; makes this call site's slider a unique control.</param>
    [PublicAPI]
    public static void Slider(this Gui gui, ref float value, float min, float max,
        float width = 200, float height = 24, float step = 0f,
        Color? trackColor = null, Color? fillColor = null, Color? thumbColor = null,
        bool showValue = false, float fontSize = 12, bool enabled = true,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        if (max < min) (min, max) = (max, min);
        value = Math.Clamp(value, min, max);

        var id = gui.NodeId(filePath, lineNumber);
        var state = gui.ControlState(id, () => new SliderState());

        using (gui.Node(width, height).Direction(Axis.Horizontal).Gap(8).ContentAlignY(0.5f).Enter())
        {
            using (gui.Node().Expand().Enter())
            {
                RenderSlider(gui, state, ref value, min, max, step, trackColor, fillColor, thumbColor, enabled);
            }

            if (showValue)
                gui.DrawText(FormatSliderValue(value, step), fontSize,
                    enabled ? gui.Controls.Text : DisabledText, centerInRect: false);
        }
    }

    private static void RenderSlider(Gui gui, SliderState state, ref float value, float min, float max,
        float step, Color? trackColor, Color? fillColor, Color? thumbColor, bool enabled)
    {
        if (gui.Pass != Pass.Pass2Render) return;

        var interactable = gui.GetInteractable();
        var rect = gui.CurrentNode.Rect;
        var trackHeight = Math.Min(6f, rect.H);
        var trackY = rect.Y + (rect.H - trackHeight) / 2f;
        var thumbRadius = MathF.Min(SliderThumbRadius, rect.H * 0.38f);

        var t = max > min ? (value - min) / (max - min) : 0f;
        var thumbX = rect.X + t * rect.W;
        var thumbCenter = new Vector2(thumbX, rect.Y + rect.H * 0.5f);

        gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);

        if (enabled)
        {
            var pressed = interactable.OnClick(out var clicks);
            if (clicks >= 2)
            {
                // A double click goes straight to a keyboard drag from wherever the pointer lands;
                // the thumb follows the mouse until it is released, no press needed.
                state.Dragging = true;
            }
            else if (pressed)
            {
                gui.RequestFocus(FocusReason.Mouse);
                state.Dragging = true;
            }

            if (state.Dragging)
            {
                if (gui.Input.IsMouseButtonDown(MouseButton.Left))
                    value = ValueFromPointerX(gui.Input.MousePosition.X, rect, min, max, step);
                else
                    state.Dragging = false;
            }

            if (gui.HasFocus())
            {
                var delta = step > 0 ? step : 1f;
                if (gui.Input.IsKeyPressed(KeyboardKey.Left)) value = Math.Clamp(value - delta, min, max);
                else if (gui.Input.IsKeyPressed(KeyboardKey.Right)) value = Math.Clamp(value + delta, min, max);
                else if (gui.Input.IsKeyPressed(KeyboardKey.Home)) value = min;
                else if (gui.Input.IsKeyPressed(KeyboardKey.End)) value = max;
            }
        }

        DrawSliderShape(gui, rect, trackY, trackHeight, thumbCenter, thumbRadius, value, min, max,
            trackColor, fillColor, thumbColor, enabled);
    }

    private static float ValueFromPointerX(float x, Rect rect, float min, float max, float step)
    {
        var fraction = (x - rect.X) / rect.W;
        var value = min + Math.Clamp(fraction, 0f, 1f) * (max - min);
        if (step > 0) value = min + MathF.Round((value - min) / step) * step;
        return Math.Clamp(value, min, max);
    }

    private static void DrawSliderShape(Gui gui, Rect rect, float trackY, float trackHeight,
        Vector2 thumbCenter, float thumbRadius, float value, float min, float max,
        Color? trackColor, Color? fillColor, Color? thumbColor, bool enabled)
    {
        var track = new Rect(rect.X, trackY, rect.W, trackHeight);
        var fillWidth = rect.X <= thumbCenter.X ? thumbCenter.X - rect.X : 0f;

        gui.DrawRect(track, enabled ? trackColor ?? gui.Controls.Border : DisabledBorder);
        gui.DrawRect(new Rect(rect.X, trackY, fillWidth, trackHeight),
            enabled ? fillColor ?? gui.Controls.Accent : DisabledText);

        var thumb = enabled ? thumbColor ?? gui.Controls.Knob : DisabledText;
        if (gui.HasFocus())
            gui.DrawCircleBorder(thumbCenter, thumbRadius + 3f, gui.Controls.Accent, 2f);

        gui.DrawCircleFilled(thumbCenter, thumbRadius, thumb);
        gui.DrawCircleBorder(thumbCenter, thumbRadius, Color.FromArgb(100, 0, 0, 0));
    }

    private static string FormatSliderValue(float value, float step) =>
        value.ToString(step >= 1f ? "N0" : "0.##", System.Globalization.CultureInfo.InvariantCulture);

    private const float SliderThumbRadius = 7f;
}