namespace Guinevere;

public static partial class ControlsExtensions
{
    private sealed class NumberFieldState
    {
        public InputState Buffer = new() { Text = "0", External = "0" };
        public bool Captured;
        public bool Scrubbing;
        public Vector2 PressPosition;
        public float PressValue;
    }

    /// <summary>
    /// A numeric input with Unity-style scrub editing: press and drag right or up to increase the value,
    /// left or down to decrease it. The drag starts from the value at the press, so it never "jumps".
    /// A plain click places the caret and opens keyboard editing, which commits on Enter or on focus
    /// loss. Invalid text reverts to the previous value, and everything stays clamped to
    /// [<paramref name="min"/>, <paramref name="max"/>].
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="value">The number the field edits; clamped into [<paramref name="min"/>, <paramref name="max"/>].</param>
    /// <param name="step">Units per pixel of horizontal drag; vertical drag uses the same step.</param>
    /// <param name="min">Lower bound on the value, applied to scrubbing and to committed text.</param>
    /// <param name="max">Upper bound on the value, applied to scrubbing and to committed text.</param>
    /// <param name="width">Node width.</param>
    /// <param name="height">Node height.</param>
    /// <param name="format">Format applied when echoing numbers back into the field (for example "0.##").</param>
    /// <param name="backgroundColor">The field fill; defaults to the palette surface.</param>
    /// <param name="borderColor">The field outline; defaults to the palette border, the accent when focused.</param>
    /// <param name="textColor">The text colour; defaults to the palette text.</param>
    /// <param name="cursorColor">The caret colour; defaults to the text colour.</param>
    /// <param name="fontSize">The font size.</param>
    /// <param name="padding">Inner padding.</param>
    /// <param name="dragSensitivity">Multiplier on the drag distance, letting one step span several pixels.</param>
    /// <param name="enabled">Whether the field responds to input.</param>
    /// <param name="id">A stable identifier; two numeric fields on the same frame must not share one.</param>
    [PublicAPI]
    public static void NumberField(this Gui gui, ref float value,
        float step = 1f, float min = float.MinValue, float max = float.MaxValue,
        float width = 200, float height = 32, string format = "0.##",
        Color? backgroundColor = null, Color? borderColor = null, Color? textColor = null,
        Color? cursorColor = null, float fontSize = 14, float padding = 8,
        float dragSensitivity = 1f, bool enabled = true, string id = "")
    {
        ArgumentNullException.ThrowIfNull(gui);
        if (max < min) (min, max) = (max, min);
        value = Clamp(value, min, max);

        var nodeId = string.IsNullOrEmpty(id) ? gui.NodeId("NumberField", 0) : id;
        var field = gui.ControlState(nodeId, () => new NumberFieldState());

        SyncNumberBuffer(field, value, format);

        using (gui.Node(width, height).Padding(FitPadding(height, padding)).ContentAlignY(0.5f).Enter())
        {
            var interactable = gui.GetInteractable();
            HandleNumberFieldInteraction(gui, field, ref value, interactable, step, min, max, format,
                dragSensitivity, fontSize, enabled);

            var cursorColorFinal = cursorColor ?? textColor ??
                gui.CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value;
            DrawInputBackground(gui, field.Buffer, backgroundColor, borderColor);
            DrawSelection(gui, field.Buffer, field.Buffer.Text, fontSize);
            DrawInputText(gui, field.Buffer.Text, "", fontSize, textColor, null);
            DrawCursor(gui, field.Buffer, field.Buffer.Text, fontSize, cursorColorFinal);
        }
    }

    private static void SyncNumberBuffer(NumberFieldState field, float value, string format)
    {
        var formatted = FormatNumber(value, format);
        var editing = field.Buffer.IsFocused || field.Scrubbing || field.Captured;

        if (!editing && !string.Equals(field.Buffer.External, formatted, StringComparison.Ordinal))
        {
            field.Buffer.Text = formatted;
            field.Buffer.External = formatted;
            field.Buffer.MoveTo(formatted.Length, extend: false);
        }
    }

    private static void HandleNumberFieldInteraction(Gui gui, NumberFieldState field, ref float value,
        InteractableElement interactable, float step, float min, float max, string format,
        float dragSensitivity, float fontSize, bool enabled)
    {
        if (gui.Pass != Pass.Pass2Render) return;

        if (!enabled)
        {
            field.Buffer.IsFocused = false;
            field.Captured = false;
            field.Scrubbing = false;
            value = Clamp(value, min, max);
            return;
        }

        gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
        var hasFocus = gui.HasFocus();
        var mouse = gui.Input.MousePosition;

        if (interactable.OnClick())
        {
            field.Captured = true;
            field.Scrubbing = false;
            field.PressPosition = mouse;
            field.PressValue = value;
            field.Buffer.External = field.Buffer.Text;
        }

        if (field.Captured)
        {
            var buttonDown = gui.Input.IsMouseButtonDown(MouseButton.Left);
            var dx = mouse.X - field.PressPosition.X;
            var dy = field.PressPosition.Y - mouse.Y; // up is positive
            var moved = MathF.Sqrt(dx * dx + dy * dy);

            if (buttonDown && (field.Scrubbing || moved >= ScrubThreshold))
            {
                field.Scrubbing = true;
                value = Clamp(field.PressValue + (dx + dy) * step * dragSensitivity, min, max);
                field.Buffer.Text = FormatNumber(value, format);
                field.Buffer.MoveTo(field.Buffer.Text.Length, extend: false);
            }
            else if (!buttonDown)
            {
                if (!field.Scrubbing)
                {
                    gui.RequestFocus(FocusReason.Mouse);
                    var at = GetCursorPositionFromClick(gui, mouse, gui.CurrentNode.InnerRect,
                        field.Buffer.Text, fontSize);
                    field.Buffer.MoveTo(at, extend: false);
                    field.Buffer.BlinkTimer = 0f;
                }
                field.Captured = false;
                field.Scrubbing = false;
            }
        }

        var wasEditing = field.Buffer.IsFocused;
        field.Buffer.IsFocused = hasFocus;

        if (field.Buffer.IsFocused)
        {
            if (gui.Input.IsKeyPressed(KeyboardKey.Enter))
            {
                value = CommitNumber(field, value, min, max, format);
                field.Buffer.IsFocused = false;
                gui.ClearFocus();
            }
            else
            {
                HandleKeyboardInput(field.Buffer, gui);
            }
        }
        else if (wasEditing)
        {
            value = CommitNumber(field, value, min, max, format);
        }
    }

    private static float CommitNumber(NumberFieldState field, float fallback, float min, float max,
        string format)
    {
        var parsed = float.TryParse(field.Buffer.Text.Trim(),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture,
            out var number)
            ? Clamp(number, min, max)
            : Clamp(fallback, min, max);

        field.Buffer.Text = FormatNumber(parsed, format);
        field.Buffer.External = field.Buffer.Text;
        field.Buffer.MoveTo(field.Buffer.Text.Length, extend: false);
        return parsed;
    }

    private static string FormatNumber(float value, string format) =>
        value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);

    private static float Clamp(float value, float min, float max) =>
        Math.Clamp(value, min, max);

    private const float ScrubThreshold = 4f;
}