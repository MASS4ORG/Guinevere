using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    sealed class SliderState
    {
        public bool Dragging;
    }

    /// <summary>
    /// A numeric slider. The caller owns the value (like <c>Toggle</c>), so the control never fights an
    /// external databind. Clicking anywhere on the track jumps there, dragging keeps scrubbing even when
    /// the pointer leaves the widget, and the keyboard arrows push the value by one <paramref name="step"/>
    /// when the slider has focus. Pass <paramref name="step"/> larger than zero to snap to multiples. Styled by the
    /// <c>slider</c> rules (<c>:disabled</c>; <c>color</c> for the value label) and its drawn parts <c>track</c>,
    /// <c>fill</c> and <c>thumb</c> (<c>:focus</c> while the slider has focus).
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="value">The current value; clamped into [<paramref name="min"/>, <paramref name="max"/>].</param>
    /// <param name="min">Lower bound.</param>
    /// <param name="max">Upper bound.</param>
    /// <param name="step">Snap interval, or 0 to move continuously.</param>
    /// <param name="showValue">Whether to draw the numeric value to the right of the track.</param>
    /// <param name="enabled">Whether the slider responds to input.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Optional id for stylesheet selectors and persistent control state; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>
    /// The slider's node, <see cref="LayoutNode.Changed"/> on a frame the value moved (as a <c>bool</c> too). It is the
    /// scope's field width by compact height unless a sheet or the node says otherwise; the value label, when shown,
    /// uses the inherited text size.
    /// </returns>
    [PublicAPI]
    public static ControlNode Slider(this Gui gui, ref float value, float min, float max, float step = 0f,
        bool showValue = false, bool enabled = true,
        IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        if (max < min)
        {
            (min, max) = (max, min);
        }

        value = Math.Clamp(value, min, max);
        ExcaliburStyles.Ensure(gui);

        var node = gui.StyledControl("slider", classes, id, disabled: !enabled,
            filePath: filePath, lineNumber: lineNumber);
        SizeUnlessStyled(node, gui.ControlStyle.FieldWidth, gui.ControlStyle.CompactHeight);

        var original = value;
        var state = gui.ControlState(node.Id, static () => new SliderState());
        using (node.Enter())
        {
            var viewport = gui.Node().Expand();
            using (viewport.Enter())
            {
            }
            RenderSlider(gui, state, viewport.Rect, ref value, min, max, step, enabled);
            if (showValue)
            {
                gui.DrawText(FormatSliderValue(value, step),
                    gui.CurrentNodeScope.Get<LayoutNodeScopeTextSize>().Value, centerInRect: false);
            }
        }

        if (gui.Pass == Pass.Pass2Render && !value.Equals(original))
        {
            node.Raise(NodeSignals.Changed);
        }

        return node;
    }

    static void RenderSlider(Gui gui, SliderState state, Rect rect, ref float value, float min, float max,
        float step, bool enabled)
    {
        if (gui.Pass != Pass.Pass2Render)
        {
            return;
        }

        if (enabled)
        {
            gui.RegisterFocusable(claimsArrowKeys: true);
            HandleSliderPointer(gui, state, rect, ref value, min, max, step);
            HandleSliderKeys(gui, ref value, min, max, step);
        }
        else
        {
            state.Dragging = false;
        }

        DrawSliderShape(gui, rect, value, min, max);
    }

    static void HandleSliderPointer(Gui gui, SliderState state, Rect rect, ref float value,
        float min, float max, float step)
    {
        if (gui.GetInteractable().OnClick())
        {
            gui.RequestFocus(FocusReason.Mouse);
            state.Dragging = true;
        }
        if (!gui.Input.IsMouseButtonDown(MouseButton.Left))
        {
            state.Dragging = false;
        }

        if (state.Dragging && rect.W > 0)
        {
            value = ValueFromPointerX(gui.LocalMousePosition.X, rect, min, max, step);
        }
    }

    static void HandleSliderKeys(Gui gui, ref float value, float min, float max, float step)
    {
        if (!gui.HasFocus())
        {
            return;
        }

        var delta = step > 0 ? step : 1f;
        if (gui.Input.IsKeyPressed(KeyboardKey.Left))
        {
            value = Math.Clamp(value - delta, min, max);
        }
        else if (gui.Input.IsKeyPressed(KeyboardKey.Right))
        {
            value = Math.Clamp(value + delta, min, max);
        }
        else if (gui.Input.IsKeyPressed(KeyboardKey.Home))
        {
            value = min;
        }
        else if (gui.Input.IsKeyPressed(KeyboardKey.End))
        {
            value = max;
        }
    }

    static float ValueFromPointerX(float x, Rect rect, float min, float max, float step)
    {
        var fraction = (x - rect.X) / rect.W;
        var value = min + Math.Clamp(fraction, 0f, 1f) * (max - min);
        if (step > 0)
        {
            value = min + MathF.Round((value - min) / step) * step;
        }

        return Math.Clamp(value, min, max);
    }

    /// <summary>Draws the slider's parts after input so the fill and thumb match the current value.</summary>
    static void DrawSliderShape(Gui gui, Rect rect, float value, float min, float max)
    {
        var trackStyle = gui.ResolvePart("track");
        var trackHeight = Math.Clamp(PartLength(trackStyle, "height", rect.H), 0f, Math.Max(0f, rect.H));
        var trackY = rect.Y + (rect.H - trackHeight) / 2f;
        var track = new Rect(rect.X, trackY, rect.W, trackHeight);
        var fraction = max > min ? (value - min) / (max - min) : 0f;
        var fillWidth = fraction * rect.W;
        gui.DrawStyledBox(trackStyle, track);
        gui.DrawStyledBox(gui.ResolvePart("fill"), new Rect(rect.X, trackY, fillWidth, trackHeight));

        var thumbStyle = gui.ResolvePart("thumb", gui.HasFocus() ? StyleState.Focus : StyleState.None);
        var thumbWidth = Math.Max(0f, Math.Min(PartLength(thumbStyle, "width", rect.H),
            PartLength(thumbStyle, "max-width", rect.H, float.PositiveInfinity)));
        var thumbHeight = Math.Max(0f, Math.Min(PartLength(thumbStyle, "height", rect.H),
            PartLength(thumbStyle, "max-height", rect.H, float.PositiveInfinity)));
        gui.DrawStyledBox(thumbStyle, new Rect(rect.X + fillWidth - thumbWidth / 2f,
            rect.Y + (rect.H - thumbHeight) / 2f, thumbWidth, thumbHeight));
    }

    static float PartLength(ResolvedStyle style, string property, float basis, float fallback = 0f) =>
        StyleValue.TryLength(style.Get(property), out var length, out var percent)
            ? percent ? length * basis : length
            : fallback;

    static string FormatSliderValue(float value, float step) =>
        value.ToString(step >= 1f ? "N0" : "0.##", System.Globalization.CultureInfo.InvariantCulture);
}
