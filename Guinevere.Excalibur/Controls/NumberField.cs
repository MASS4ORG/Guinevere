namespace Guinevere;

public static partial class ControlsExtensions
{
    private sealed class NumberFieldState
    {
        public TextEditState Buffer = new("0");
        public bool Captured;
    }

    /// <summary>
    /// A numeric text input. A click places the caret and opens keyboard editing, which commits on Enter or on focus
    /// loss. Invalid text reverts to the previous value, and everything stays clamped to
    /// [<paramref name="min"/>, <paramref name="max"/>]. Text is right-aligned when
    /// <paramref name="alignX"/> is 1 and clipped to the box, so a wide number never spills out of the field.
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
    /// <param name="textColor">The text color; defaults to the palette text.</param>
    /// <param name="cursorColor">The caret color; defaults to the text color.</param>
    /// <param name="fontSize">The font size.</param>
    /// <param name="padding">Inner padding.</param>
    /// <param name="dragSensitivity">Multiplier on the drag distance, letting one step span several pixels.</param>
    /// <param name="enabled">Whether the field responds to input.</param>
    /// <param name="id">A stable identifier; two numeric fields on the same frame must not share one.</param>
    /// <param name="alignX">Horizontal alignment of the text, 0 left to 1 right.</param>
    [PublicAPI]
    public static void NumberField(this Gui gui, ref double value,
        double step = 1.0, double min = double.MinValue, double max = double.MaxValue,
        float width = 200, float height = 32, string format = "0.##",
        Color? backgroundColor = null, Color? borderColor = null, Color? textColor = null,
        Color? cursorColor = null, float fontSize = 14, float padding = 8,
        double dragSensitivity = 1.0, bool enabled = true, string id = "", float alignX = 0f)
    {
        ArgumentNullException.ThrowIfNull(gui);
        if (max < min) (min, max) = (max, min);
        value = Clamp(value, min, max);

        var nodeId = string.IsNullOrEmpty(id) ? gui.NodeId("NumberField", 0) : id;
        var field = gui.ControlState(nodeId, () => new NumberFieldState());

        SyncNumberBuffer(field, value, format);

        using (gui.Node(width, height).Padding(FitPadding(height, padding))
                   .ContentAlignX(alignX).ContentAlignY(0.5f).Enter())
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

    /// <summary>Float wrapper over the double field, for callers whose values are floats.</summary>
    [PublicAPI]
    public static void NumberField(this Gui gui, ref float value,
        float step = 1f, float min = float.MinValue, float max = float.MaxValue,
        float width = 200, float height = 32, string format = "0.##",
        Color? backgroundColor = null, Color? borderColor = null, Color? textColor = null,
        Color? cursorColor = null, float fontSize = 14, float padding = 8,
        float dragSensitivity = 1f, bool enabled = true, string id = "", float alignX = 0f)
    {
        double d = value;
        gui.NumberField(ref d, step, min, max, width, height, format, backgroundColor, borderColor,
            textColor, cursorColor, fontSize, padding, dragSensitivity, enabled, id, alignX);
        value = (float)d;
    }

    private static void SyncNumberBuffer(NumberFieldState field, double value, string format)
    {
        var formatted = FormatNumber(value, format);
        var editing = field.Buffer.IsFocused || field.Captured;

        if (!editing && !string.Equals(field.Buffer.External, formatted, StringComparison.Ordinal))
        {
            field.Buffer.Text = formatted;
            field.Buffer.External = formatted;
            field.Buffer.MoveTo(formatted.Length, extend: false);
        }
    }

    private static void HandleNumberFieldInteraction(Gui gui, NumberFieldState field, ref double value,
        InteractableElement interactable, double step, double min, double max, string format,
        double dragSensitivity, float fontSize, bool enabled)
    {
        if (gui.Pass != Pass.Pass2Render) return;

        if (!enabled)
        {
            field.Buffer.IsFocused = false;
            field.Captured = false;
            value = Clamp(value, min, max);
            return;
        }

        gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
        var hasFocus = gui.HasFocus();
        var mouse = gui.Input.MousePosition;

        if (interactable.OnClick())
        {
            field.Captured = true;
            field.Buffer.External = field.Buffer.Text;
        }

        if (field.Captured)
        {
            var buttonDown = gui.Input.IsMouseButtonDown(MouseButton.Left);
            if (!buttonDown)
            {
                gui.RequestFocus(FocusReason.Mouse);
                var at = TextEditor.PositionAt(gui, mouse, gui.CurrentNode.InnerRect,
                    field.Buffer.Text, fontSize);
                field.Buffer.MoveTo(at, extend: false);
                field.Buffer.BlinkTimer = 0f;
                field.Captured = false;
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
                TextEditor.ProcessKeyboard(gui, field.Buffer);
            }
        }
        else if (wasEditing)
        {
            value = CommitNumber(field, value, min, max, format);
        }
    }

    private static double CommitNumber(NumberFieldState field, double fallback, double min, double max,
        string format)
    {
        var parsed = double.TryParse(field.Buffer.Text.Trim(),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture,
            out var number)
            ? Clamp(number, min, max)
            : Clamp(fallback, min, max);

        field.Buffer.SetValue(FormatNumber(parsed, format));
        return parsed;
    }

    private static string FormatNumber(double value, string format) =>
        value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);

    private static double Clamp(double value, double min, double max) =>
        Math.Clamp(value, min, max);

}
