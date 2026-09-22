using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{

    /// <summary>
    /// Shrinks the padding so it never eats the whole field. A short input keeps a little breathing
    /// room instead of squeezing its text out of the box.
    /// </summary>
    static float FitPadding(float height, float padding) =>
        height <= 0 ? padding : Math.Min(padding, Math.Max(2f, (height - 4f) / 2f));

    static void DrawInputBackground(Gui gui, TextEditState state, Color? backgroundColor, Color? borderColor,
        bool enabled)
    {
        var fill = backgroundColor ?? gui.ControlStyle.Surface;
        var outline = borderColor ?? gui.ControlStyle.Border;
        var borderWidth = 1f;

        if (!enabled)
        {
            fill = Color.Lerp(fill, gui.ControlStyle.BaseBackground, 0.45f);
            outline = gui.ControlStyle.Divider;
        }
        else if (state.IsFocused)
        {
            outline = gui.ControlStyle.Accent;
            borderWidth = 2f;
        }

        gui.DrawBackgroundRect(fill);
        gui.DrawRectBorder(gui.CurrentNode.Rect, outline, borderWidth);
    }

    /// <summary>Paints the selected run behind the glyphs, so the text stays readable over it.</summary>
    static void DrawSelection(Gui gui, TextEditState state, string text, float fontSize)
    {
        if (!state.IsFocused || !state.HasSelection || gui.Pass != Pass.Pass2Render) return;

        var font = TextEditor.MeasuringFont(gui, fontSize);
        var inner = gui.CurrentNode.InnerRect;

        var start = Math.Clamp(state.SelectionStart, 0, text.Length);
        var end = Math.Clamp(state.SelectionEnd, 0, text.Length);

        var origin = TextEditor.TextOriginX(gui, font, text, inner);
        var x1 = origin + TextEditor.MeasureWidth(font, text[..start]);
        var x2 = origin + TextEditor.MeasureWidth(font, text[..end]);

        gui.DrawRect(new Rect(x1, inner.Y, Math.Max(1f, x2 - x1), inner.H),
            gui.ControlStyle.TextSelection);
    }

    /// <summary>Paints the selected run per line in a multi-line field, so text areas get the same highlight as inputs.</summary>
    static void DrawSelectionMultiline(Gui gui, TextEditState state, string text, float fontSize)
    {
        if (!state.IsFocused || !state.HasSelection || gui.Pass != Pass.Pass2Render) return;

        var font = TextEditor.MeasuringFont(gui, fontSize);
        var lineHeight = fontSize * 1.2f;
        var inner = gui.CurrentNode.InnerRect;

        var start = Math.Clamp(state.SelectionStart, 0, text.Length);
        var end = Math.Clamp(state.SelectionEnd, 0, text.Length);
        if (start == end) return;

        var lines = text.Split('\n');
        var (startRow, startCol) = TextEditor.LineAndColumn(lines, start);
        var (endRow, endCol) = TextEditor.LineAndColumn(lines, end);

        for (var row = startRow; row <= endRow && row < lines.Length; row++)
        {
            var colStart = row == startRow ? startCol : 0;
            var colEnd = row == endRow ? endCol : lines[row].Length;
            if (colEnd <= colStart) continue;

            var x1 = inner.X + TextEditor.MeasureWidth(font, lines[row][..colStart]);
            var x2 = inner.X + TextEditor.MeasureWidth(font, lines[row][..colEnd]);
            var y = inner.Y + row * lineHeight;

            gui.DrawRect(new Rect(x1, y, Math.Max(1f, x2 - x1), lineHeight),
                gui.ControlStyle.TextSelection);
        }
    }

    static void DrawInputText(Gui gui, string displayText, string placeholder, float fontSize,
        Color? textColor, Color? placeholderColor, bool enabled)
    {
        var finalDisplayText = string.IsNullOrEmpty(displayText) ? placeholder : displayText;
        var finalColor = !enabled
            ? gui.ControlStyle.TextDisabled
            : string.IsNullOrEmpty(displayText)
            ? placeholderColor ?? gui.ControlStyle.TextDim
            : textColor ?? gui.ControlStyle.Text;

        if (!string.IsNullOrEmpty(finalDisplayText))
            gui.DrawText(finalDisplayText, fontSize, finalColor, centerInRect: false);
    }

    static void DrawCursor(Gui gui, TextEditState state, string text, float fontSize, Color cursorColor)
    {
        if (!state.IsFocused || !state.ShowCursor || gui.Pass != Pass.Pass2Render) return;

        var font = TextEditor.MeasuringFont(gui, fontSize);
        var textBeforeCursor = text.Substring(0, Math.Min(state.CursorPosition, text.Length));
        var textWidth = TextEditor.MeasureWidth(font, textBeforeCursor);

        var innerRect = gui.CurrentNode.InnerRect;
        var cursorX = TextEditor.TextOriginX(gui, font, text, innerRect) + textWidth;
        var cursorY1 = innerRect.Y;
        var cursorY2 = innerRect.Y + innerRect.H;

        gui.DrawRect(new Rect(cursorX, cursorY1, 1.5f, cursorY2 - cursorY1), cursorColor);
    }

    static void DrawCursorMultiline(Gui gui, TextEditState state, string text, float fontSize, Color? cursorColor)
    {
        if (!state.IsFocused || !state.ShowCursor || gui.Pass != Pass.Pass2Render) return;

        var font = TextEditor.MeasuringFont(gui, fontSize);
        var lineHeight = fontSize * 1.2f;
        var lines = text.Split('\n');
        var innerRect = gui.CurrentNode.InnerRect;

        // Bounds checking for empty text
        if (lines.Length == 0) return;

        // Find the cursor line and column with better bounds checking
        var cursorInfo = lines
            .Select((line, index) => new { Line = line, Index = index, Length = line.Length + 1 })
            .Aggregate((Position: 0, Line: 0, Column: 0), (acc, line) =>
                acc.Position + line.Length > state.CursorPosition
                    ? (acc.Position, line.Index, state.CursorPosition - acc.Position)
                    : (acc.Position + line.Length, line.Index, 0));

        var cursorLine = Math.Min(cursorInfo.Line, lines.Length - 1);
        var cursorColumn = cursorInfo.Column;

        // Ensure cursor line and column are valid
        if (cursorLine < 0 || cursorLine >= lines.Length) return;
        if (cursorColumn < 0) cursorColumn = 0;
        if (cursorColumn > lines[cursorLine].Length) cursorColumn = lines[cursorLine].Length;

        // Calculate cursor position - use relative positioning to avoid clipping issues
        var textBeforeCursor = cursorColumn > 0 && cursorLine < lines.Length
            ? lines[cursorLine].Substring(0, cursorColumn)
            : "";

        var textWidth = TextEditor.MeasureWidth(font, textBeforeCursor);
        var cursorY = cursorLine * lineHeight + 2;
        var cursorHeight = Math.Max(2, lineHeight - 4);

        // Ensure cursor is within the visible area
        if (cursorY >= 0 && cursorY < innerRect.Height)
            // Use a nested node for cursor positioning to avoid coordinate transformation issues
            using (gui.Node(2, cursorHeight).Margin(textWidth, cursorY, 0, 0).Enter())
            {
                gui.DrawRect(gui.CurrentNode.Rect, cursorColor ?? gui.ControlStyle.Text);
            }
    }

    /// <summary>
    /// Creates a text input field with ref parameter
    /// </summary>
    public static void TextInput(this Gui gui, ref string text,
        float width = ControlMetrics.FieldWidth, float height = ControlMetrics.FieldHeight, string placeholder = "",
        Color? backgroundColor = null, Color? borderColor = null, Color? textColor = null,
        Color? placeholderColor = null, Color? cursorColor = null, float fontSize = ControlMetrics.FontSize,
        float padding = ControlMetrics.Spacing, bool enabled = true, string id = "", float alignX = 0f,
        bool grabFocus = false)
    {
        width = gui.ControlStyle.FieldWidthOr(width);
        height = gui.ControlStyle.FieldHeightOr(height);
        fontSize = gui.ControlStyle.FontSizeOr(fontSize);
        padding = gui.ControlStyle.SpacingOr(padding);

        var nodeId = string.IsNullOrEmpty(id) ? gui.NodeId("TextInput", 0) : id;
        gui.Focus.RegisterTextInput(nodeId);

        var cursorColorFinal = cursorColor ?? textColor ?? gui.CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value;
        using (gui.Node(width, height).Padding(FitPadding(height, padding))
                   .ContentAlignX(alignX).ContentAlignY(0.5f).Enter())
        {
            gui.ClipContent();
            var state = TextEditor.State(gui, nodeId, text);

            if (enabled) TextEditor.Process(gui, state, gui.GetInteractable(), fontSize);
            else state.IsFocused = false;

            if (grabFocus && enabled && gui.Pass == Pass.Pass2Render && !state.IsFocused)
            {
                gui.RequestFocus();
                state.SelectAll();
            }

            // Rendering
            DrawInputBackground(gui, state, backgroundColor, borderColor, enabled);
            DrawSelection(gui, state, state.Text, fontSize);
            DrawInputText(gui, state.Text, placeholder, fontSize, textColor, placeholderColor, enabled);
            DrawCursor(gui, state, state.Text, fontSize, cursorColorFinal);

            text = state.Text;
        }
    }

    /// <summary>
    /// Renders a text input control within the given GUI context.
    /// </summary>
    /// <param name="gui">The GUI context in which the text input is rendered.</param>
    /// <param name="text">The reference to the text content displayed or inputted in the text input field.</param>
    /// <param name="width">The width of the text input field. Default is 200.</param>
    /// <param name="height">The height of the text input field. Default is 32.</param>
    /// <param name="placeholder">The placeholder text displayed when the text input is empty. Default is an empty string.</param>
    /// <param name="backgroundColor">The background color of the text input field. Default is null.</param>
    /// <param name="borderColor">The border color of the text input field. Default is null.</param>
    /// <param name="textColor">The color of the text entered in the text input field. Default is null.</param>
    /// <param name="placeholderColor">The color of the placeholder text. Default is null.</param>
    /// <param name="cursorColor">The color of the cursor in the text input field. Default is null.</param>
    /// <param name="fontSize">The font size of the text in the input field. Default is 14.</param>
    /// <param name="padding">The padding inside the text input field. Default is 8.</param>
    /// <param name="enabled">Indicates whether the text input field is enabled. Default is true.</param>
    /// <param name="id">The unique identifier for the text input control. Default is an empty string.</param>
    /// <param name="alignX">Horizontal alignment of the text, 0 left to 1 right.</param>
    /// <param name="grabFocus">Keeps the field focused without a click, for one that appears already
    /// being edited — an inline rename. Its value is selected when focus first lands.</param>
    /// <returns>The updated value of the text in the input field.</returns>
    public static string TextInput(this Gui gui, string text,
        float width = ControlMetrics.FieldWidth, float height = ControlMetrics.FieldHeight, string placeholder = "",
        Color? backgroundColor = null, Color? borderColor = null, Color? textColor = null,
        Color? placeholderColor = null, Color? cursorColor = null, float fontSize = ControlMetrics.FontSize,
        float padding = ControlMetrics.Spacing, bool enabled = true, string id = "", float alignX = 0f,
        bool grabFocus = false)
    {
        width = gui.ControlStyle.FieldWidthOr(width);
        height = gui.ControlStyle.FieldHeightOr(height);
        fontSize = gui.ControlStyle.FontSizeOr(fontSize);
        padding = gui.ControlStyle.SpacingOr(padding);

        gui.TextInput(ref text, width, height, placeholder, backgroundColor, borderColor,
            textColor, placeholderColor, cursorColor, fontSize, padding, enabled, id, alignX, grabFocus);
        return text;
    }

    /// <summary>
    /// Password input field with masked text (ref parameter)
    /// </summary>
    public static void PasswordInput(this Gui gui, ref string text,
        float width = ControlMetrics.FieldWidth, float height = ControlMetrics.FieldHeight, char maskChar = '*', string placeholder = "",
        Color? backgroundColor = null, Color? borderColor = null, Color? textColor = null,
        Color? placeholderColor = null, Color? cursorColor = null, float fontSize = ControlMetrics.FontSize,
        float padding = ControlMetrics.Spacing, bool enabled = true, string id = "")
    {
        width = gui.ControlStyle.FieldWidthOr(width);
        height = gui.ControlStyle.FieldHeightOr(height);
        fontSize = gui.ControlStyle.FontSizeOr(fontSize);
        padding = gui.ControlStyle.SpacingOr(padding);

        var nodeId = string.IsNullOrEmpty(id) ? gui.NodeId("PasswordInput", 0) : id;
        gui.Focus.RegisterTextInput(nodeId);

        var cursorColorFinal = cursorColor ?? textColor ?? gui.CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value;
        using (gui.Node(width, height).Padding(FitPadding(height, padding)).ContentAlignY(0.5f).Enter())
        {
            gui.ClipContent();
            var state = TextEditor.State(gui, nodeId, text);

            if (enabled)
                TextEditor.Process(gui, state, gui.GetInteractable(), fontSize,
                    displayText: new string(maskChar, state.Text.Length));
            else state.IsFocused = false;

            // Rendering with masked text
            var maskedText = new string(maskChar, state.Text.Length);
            DrawInputBackground(gui, state, backgroundColor, borderColor, enabled);
            DrawInputText(gui, maskedText, placeholder, fontSize, textColor, placeholderColor, enabled);
            DrawCursor(gui, state, maskedText, fontSize, cursorColorFinal);

            text = state.Text;
        }
    }

    /// <summary>
    /// Creates a password input field with the specified parameters, supporting masked characters.
    /// </summary>
    /// <param name="gui">The GUI instance used to render the password input field.</param>
    /// <param name="text">The reference to the string variable where the entered password will be stored.</param>
    /// <param name="width">The width of the password input field. Default is 200.</param>
    /// <param name="height">The height of the password input field. Default is 32.</param>
    /// <param name="maskChar">The character used to mask the password input. Default is '*'.</param>
    /// <param name="placeholder">The placeholder text displayed when the input is empty. Default is an empty string.</param>
    /// <param name="backgroundColor">The background color of the input field. Default is null.</param>
    /// <param name="borderColor">The border color of the input field. Default is null.</param>
    /// <param name="textColor">The text color for the input field. Default is null.</param>
    /// <param name="placeholderColor">The color of the placeholder text. Default is null.</param>
    /// <param name="cursorColor">The color of the cursor within the input field. Default is null.</param>
    /// <param name="fontSize">The font size of the input text. Default is 14.</param>
    /// <param name="padding">The padding inside the input field. Default is 8.</param>
    /// <param name="enabled">Indicates whether the input field is interactive. Default is true.</param>
    /// <param name="id">The unique identifier for the input field. Default is an empty string.</param>
    /// <returns>Returns the updated text entered in the password input field.</returns>
    public static string PasswordInput(this Gui gui, string text,
        float width = ControlMetrics.FieldWidth, float height = ControlMetrics.FieldHeight, char maskChar = '*', string placeholder = "",
        Color? backgroundColor = null, Color? borderColor = null, Color? textColor = null,
        Color? placeholderColor = null, Color? cursorColor = null, float fontSize = ControlMetrics.FontSize,
        float padding = ControlMetrics.Spacing, bool enabled = true, string id = "")
    {
        width = gui.ControlStyle.FieldWidthOr(width);
        height = gui.ControlStyle.FieldHeightOr(height);
        fontSize = gui.ControlStyle.FontSizeOr(fontSize);
        padding = gui.ControlStyle.SpacingOr(padding);

        gui.PasswordInput(ref text, width, height, maskChar, placeholder, backgroundColor, borderColor,
            textColor, placeholderColor, cursorColor, fontSize, padding, enabled, id);
        return text;
    }

    /// <summary>
    /// Creates a text area input field with ref parameter.
    /// </summary>
    /// <param name="gui">The GUI context in which the text area is drawn.</param>
    /// <param name="text">The text content of the text area, passed by reference.</param>
    /// <param name="width">The width of the text area in pixels. Default is 300.</param>
    /// <param name="height">The height of the text area in pixels. Default is 100.</param>
    /// <param name="placeholder">The placeholder text displayed when the text area is empty. Default is an empty string.</param>
    /// <param name="backgroundColor">The background color of the text area. Default is null, which uses the default color.</param>
    /// <param name="borderColor">The border color of the text area. Default is null, which uses the default color.</param>
    /// <param name="textColor">The color of the text in the text area. Default is null, which uses the default color.</param>
    /// <param name="placeholderColor">The color of the placeholder text. Default is null, which uses the default color.</param>
    /// <param name="cursorColor">The color of the cursor in the text area. Default is null, which uses the default color.</param>
    /// <param name="fontSize">The font size of the text. Default is 14.</param>
    /// <param name="padding">The padding inside the text area. Default is 8.</param>
    /// <param name="enabled">Specifies whether the text area is enabled for input. Default is true.</param>
    /// <param name="id">An optional identifier for the text area. Default is an empty string.</param>
    public static void TextArea(this Gui gui, ref string text,
        float width = 300, float height = 100,
        string placeholder = "",
        Color? backgroundColor = null,
        Color? borderColor = null,
        Color? textColor = null,
        Color? placeholderColor = null,
        Color? cursorColor = null,
        float fontSize = ControlMetrics.FontSize,
        float padding = ControlMetrics.Spacing,
        bool enabled = true,
        string id = "")
    {
        fontSize = gui.ControlStyle.FontSizeOr(fontSize);
        padding = gui.ControlStyle.SpacingOr(padding);

        var nodeId = string.IsNullOrEmpty(id) ? gui.NodeId("TextArea", 0) : id;
        gui.Focus.RegisterTextInput(nodeId);

        using (gui.Node(width, height).Padding(FitPadding(height, padding)).ContentAlignY(0.5f).Enter())
        {
            gui.ClipContent();
            var state = TextEditor.State(gui, nodeId, text);

            if (enabled) TextEditor.Process(gui, state, gui.GetInteractable(), fontSize, multiline: true);
            else state.IsFocused = false;

            // Rendering - let the parent handle clipping/scrolling to avoid nested contexts
            DrawInputBackground(gui, state, backgroundColor, borderColor, enabled);
            DrawSelectionMultiline(gui, state, state.Text, fontSize);
            DrawInputText(gui, state.Text, placeholder, fontSize, textColor, placeholderColor, enabled);

            // Only draw cursor if enabled
            if (enabled) DrawCursorMultiline(gui, state, state.Text, fontSize, cursorColor);

            text = state.Text;
        }
    }

    /// <summary>
    /// Creates a multi-line text area for user input.
    /// </summary>
    /// <param name="gui">The GUI context where the text area will be drawn.</param>
    /// <param name="text">The text content of the text area, passed by reference.</param>
    /// <param name="width">The width of the text area in pixels. Default is 300.</param>
    /// <param name="height">The height of the text area in pixels. Default is 100.</param>
    /// <param name="placeholder">The placeholder text shown when the text area is empty. Default is an empty string.</param>
    /// <param name="backgroundColor">The background color of the text area. Default is null.</param>
    /// <param name="borderColor">The border color of the text area. Default is null.</param>
    /// <param name="textColor">The text color used inside the text area. Default is null.</param>
    /// <param name="placeholderColor">The color of the placeholder text. Default is null.</param>
    /// <param name="cursorColor">The color of the cursor in the text area. Default is null.</param>
    /// <param name="fontSize">The font size of the text. Default is 14.</param>
    /// <param name="padding">The padding inside the text area. Default is 8.</param>
    /// <param name="enabled">Indicates whether the text area is active and editable. Default is true.</param>
    /// <param name="id">An optional identifier for the text area. Default is an empty string.</param>
    /// <returns>The updated text content of the text area.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string TextArea(this Gui gui, string text,
        float width = 300, float height = 100,
        string placeholder = "",
        Color? backgroundColor = null,
        Color? borderColor = null,
        Color? textColor = null,
        Color? placeholderColor = null,
        Color? cursorColor = null,
        float fontSize = ControlMetrics.FontSize,
        float padding = ControlMetrics.Spacing,
        bool enabled = true,
        string id = "")
    {
        fontSize = gui.ControlStyle.FontSizeOr(fontSize);
        padding = gui.ControlStyle.SpacingOr(padding);

        gui.TextArea(ref text, width, height, placeholder, backgroundColor, borderColor,
            textColor, placeholderColor, cursorColor, fontSize, padding, enabled, id);
        return text;
    }

    /// <summary>
    /// Clears all input states - useful for cleanup
    /// </summary>
    public static void ClearInputStates(this Gui gui)
    {
        gui.ClearControlStates<TextEditState>();
    }
}
