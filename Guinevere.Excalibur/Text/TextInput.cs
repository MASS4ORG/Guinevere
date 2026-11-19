using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    static readonly string[] PasswordClass = ["password"];
    static readonly string[] AreaClass = ["area"];

    /// <summary>Default width of a text area; a text area is taller than a field to hold several lines.</summary>
    const float AreaWidth = 300f;

    /// <summary>Default height of a text area.</summary>
    const float AreaHeight = 100f;

    /// <summary>
    /// The box of a text field: an <c>input</c> control with its variant and caller classes, styled from the GUI's
    /// sheets (<c>:focus</c>, <c>:disabled</c>). It takes the scope's field size and spacing as padding unless a
    /// sheet sets them, and the control font size unless the sheet sets one.
    /// </summary>
    static ControlNode FieldNode(Gui gui, string[]? variant, IReadOnlyList<string>? classes, string? id, bool enabled,
        Size width, Size height, string filePath, int lineNumber)
    {
        ExcaliburStyles.Ensure(gui);
        var all = classes is null ? variant : variant is null ? classes : [.. variant, .. classes];
        var node = gui.StyledControl("input", all, id, disabled: !enabled, filePath: filePath, lineNumber: lineNumber);
        UseControlFontSize(gui, node);
        SizeUnlessStyled(node, width, height);
        var spacing = gui.ControlStyle.Spacing;
        PadUnlessStyled(node, spacing, spacing);
        return node;
    }

    /// <summary>The text size a field draws and measures with, inside its node.</summary>
    static float FieldFontSize(Gui gui) => gui.CurrentNodeScope.Get<LayoutNodeScopeTextSize>().Value;

    /// <summary>
    /// Raises <see cref="NodeSignals.Changed"/> on <paramref name="node"/> when the edit state's text differs from
    /// what the caller passed in, and <see cref="NodeSignals.Submitted"/> when <paramref name="submitted"/>.
    /// Render pass only.
    /// </summary>
    static void RaiseFieldSignals(Gui gui, LayoutNode node, string original, string edited, bool submitted)
    {
        if (gui.Pass != Pass.Pass2Render)
        {
            return;
        }

        if (!string.Equals(original, edited, StringComparison.Ordinal))
        {
            node.Raise(NodeSignals.Changed);
        }

        if (submitted)
        {
            node.Raise(NodeSignals.Submitted);
        }
    }

    /// <summary>Whether the focused single-line field was submitted with Enter this frame.</summary>
    static bool EnterSubmits(Gui gui, TextEditState state) =>
        gui.Pass == Pass.Pass2Render && state.IsFocused && gui.Input.IsKeyPressed(KeyboardKey.Enter);

    /// <summary>
    /// A color declared for a part the control draws itself, such as <c>input selection { background-color = …; }</c>,
    /// matched as a child of the current node.
    /// </summary>
    static Color PartColor(Gui gui, string type, string property, StyleState state = StyleState.None) =>
        gui.ResolvePart(type, state).GetColor(property) ?? Color.Transparent;

    /// <summary>Paints the selected run behind the glyphs, so the text stays readable over it.</summary>
    static void DrawSelection(Gui gui, TextEditState state, string text, float fontSize)
    {
        if (!state.IsFocused || !state.HasSelection || gui.Pass != Pass.Pass2Render)
        {
            return;
        }

        var font = TextEditor.MeasuringFont(gui, fontSize);
        var inner = gui.CurrentNode.InnerRect;

        var start = Math.Clamp(state.SelectionStart, 0, text.Length);
        var end = Math.Clamp(state.SelectionEnd, 0, text.Length);

        var origin = TextEditor.TextOriginX(gui, font, text, inner);
        var x1 = origin + TextEditor.MeasureWidth(font, text[..start]);
        var x2 = origin + TextEditor.MeasureWidth(font, text[..end]);

        gui.DrawRect(new Rect(x1, inner.Y, Math.Max(1f, x2 - x1), inner.H),
            PartColor(gui, "selection", "background-color"));
    }

    /// <summary>Paints the selected run per line in a multi-line field, so text areas get the same highlight as inputs.</summary>
    static void DrawSelectionMultiline(Gui gui, TextEditState state, string text, float fontSize)
    {
        if (!state.IsFocused || !state.HasSelection || gui.Pass != Pass.Pass2Render)
        {
            return;
        }

        var font = TextEditor.MeasuringFont(gui, fontSize);
        var lineHeight = fontSize * 1.2f;
        var inner = gui.CurrentNode.InnerRect;

        var start = Math.Clamp(state.SelectionStart, 0, text.Length);
        var end = Math.Clamp(state.SelectionEnd, 0, text.Length);
        if (start == end)
        {
            return;
        }

        var lines = text.Split('\n');
        var (startRow, startCol) = TextEditor.LineAndColumn(lines, start);
        var (endRow, endCol) = TextEditor.LineAndColumn(lines, end);
        var color = PartColor(gui, "selection", "background-color");

        for (var row = startRow; row <= endRow && row < lines.Length; row++)
        {
            var colStart = row == startRow ? startCol : 0;
            var colEnd = row == endRow ? endCol : lines[row].Length;
            if (colEnd <= colStart)
            {
                continue;
            }

            var x1 = inner.X + TextEditor.MeasureWidth(font, lines[row][..colStart]);
            var x2 = inner.X + TextEditor.MeasureWidth(font, lines[row][..colEnd]);
            var y = inner.Y + row * lineHeight;

            gui.DrawRect(new Rect(x1, y, Math.Max(1f, x2 - x1), lineHeight), color);
        }
    }

    /// <summary>
    /// Draws the value in the field's text color, or the placeholder in the color of the <c>placeholder</c> rule
    /// (<c>:disabled</c> when the field is).
    /// </summary>
    static void DrawInputText(Gui gui, string displayText, string placeholder, float fontSize, bool enabled,
        bool clip = true)
    {
        var empty = string.IsNullOrEmpty(displayText);
        var shown = empty ? placeholder : displayText;
        if (string.IsNullOrEmpty(shown))
        {
            return;
        }

        Color? color = empty ? PlaceholderColor(gui, enabled) : null;
        gui.DrawText(shown, fontSize, color, centerInRect: false, clip: clip);
    }

    /// <summary>The color of the current node's <c>placeholder</c> part (<c>:disabled</c> when the control is).</summary>
    static Color PlaceholderColor(Gui gui, bool enabled) =>
        gui.ResolvePart("placeholder", enabled ? StyleState.None : StyleState.Disabled).GetColor("color")
        ?? gui.CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value;

    /// <summary>Draws the caret in the field's text color.</summary>
    static void DrawCursor(Gui gui, TextEditState state, string text, float fontSize)
    {
        if (!state.IsFocused || !state.ShowCursor || gui.Pass != Pass.Pass2Render)
        {
            return;
        }

        var font = TextEditor.MeasuringFont(gui, fontSize);
        var textBeforeCursor = text.Substring(0, Math.Min(state.CursorPosition, text.Length));
        var textWidth = TextEditor.MeasureWidth(font, textBeforeCursor);

        var innerRect = gui.CurrentNode.InnerRect;
        var cursorX = TextEditor.TextOriginX(gui, font, text, innerRect) + textWidth;
        var cursorY1 = innerRect.Y;
        var cursorY2 = innerRect.Y + innerRect.H;

        gui.DrawRect(new Rect(cursorX, cursorY1, 1.5f, cursorY2 - cursorY1),
            gui.CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value);
    }

    static void DrawCursorMultiline(Gui gui, TextEditState state, string text, float fontSize)
    {
        if (!state.IsFocused || !state.ShowCursor || gui.Pass != Pass.Pass2Render)
        {
            return;
        }

        var (line, column, lineText) = CaretLineAndColumn(text, state.CursorPosition);
        var lineHeight = fontSize * 1.2f;
        var inner = gui.CurrentNode.InnerRect;
        var cursorY = line * lineHeight + 2;
        if (cursorY < 0 || cursorY >= inner.Height)
        {
            return;
        }

        // Drawn straight into the area rather than into a caret node: a node built only in the render pass never
        // takes part in layout, so it had no size and the caret never showed.
        var textWidth = TextEditor.MeasureWidth(TextEditor.MeasuringFont(gui, fontSize), lineText[..column]);
        gui.DrawRect(new Rect(inner.X + textWidth, inner.Y + cursorY, 2, Math.Max(2, lineHeight - 4)),
            gui.CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value);
    }

    /// <summary>
    /// The line and column a caret offset falls on in newline-separated text, clamped to the text; the column
    /// is at most the line's length.
    /// </summary>
    internal static (int Line, int Column, string LineText) CaretLineAndColumn(string text, int position)
    {
        var lineStart = 0;
        var line = 0;
        while (true)
        {
            var lineEnd = text.IndexOf('\n', lineStart);
            var isLast = lineEnd < 0;
            var lineText = isLast ? text[lineStart..] : text[lineStart..lineEnd];
            if (isLast || position <= lineEnd)
            {
                return (line, Math.Clamp(position - lineStart, 0, lineText.Length), lineText);
            }

            lineStart = lineEnd + 1;
            line++;
        }
    }

    /// <summary>
    /// A single-line text field styled by the <c>input</c> rules of the GUI's sheets (<c>:focus</c>,
    /// <c>:disabled</c>); the placeholder is a <c>placeholder</c> child and the selection reads the
    /// <c>selection</c> rule. The caret uses the field's text color. The field is the scope's field width and height
    /// unless a sheet or the returned node says otherwise (<c>.Width(Size.Expand())</c> to fill a row).
    /// Text is left-aligned unless the sheet's <c>align-items</c> or the returned node's <c>AlignContentX</c> says otherwise.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="text">The edited text.</param>
    /// <param name="placeholder">Text shown while the field is empty.</param>
    /// <param name="grabFocus">Keeps the field focused without a click, for one that appears already
    /// being edited — an inline rename. Its value is selected when focus first lands.</param>
    /// <param name="enabled">When false the field matches <c>:disabled</c> and ignores input.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Optional id of the edit state and for <c>input#id</c> rules; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>
    /// The field's node: <see cref="LayoutNode.Changed"/> on a frame the text changed, <see cref="LayoutNode.Submitted"/>
    /// on Enter while focused; as a <c>bool</c> either one.
    /// </returns>
    public static ControlNode TextInput(this Gui gui, ref string text, string placeholder = "",
        bool grabFocus = false, bool enabled = true, IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        var node = FieldNode(gui, null, classes, id, enabled, gui.ControlStyle.FieldWidth,
            gui.ControlStyle.FieldHeight, filePath, lineNumber);
        var original = text;
        using (node.AlignContentY(0.5f).Enter())
        {
            var fontSize = FieldFontSize(gui);
            gui.ClipContent();
            var nodeId = gui.CurrentNode.Id;
            gui.Focus.RegisterTextInput(nodeId);
            var state = TextEditor.State(gui, nodeId, text);

            if (enabled)
            {
                TextEditor.Process(gui, state, gui.GetInteractable(), fontSize);
            }
            else
            {
                state.IsFocused = false;
            }

            if (grabFocus && enabled && gui.Pass == Pass.Pass2Render && !state.IsFocused)
            {
                gui.RequestFocus();
                state.SelectAll();
            }

            DrawSelection(gui, state, state.Text, fontSize);
            DrawInputText(gui, state.Text, placeholder, fontSize, enabled);
            DrawCursor(gui, state, state.Text, fontSize);

            text = state.Text;
            RaiseFieldSignals(gui, node, original, text, EnterSubmits(gui, state));
        }

        return node;
    }

    /// <summary>
    /// A text field that shows <paramref name="maskChar"/> for every character, styled as <c>input.password</c>.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="text">The edited secret.</param>
    /// <param name="maskChar">Character shown in place of each typed one.</param>
    /// <param name="placeholder">Text shown while the field is empty.</param>
    /// <param name="enabled">When false the field matches <c>:disabled</c> and ignores input.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Optional id of the edit state and for <c>input#id</c> rules; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>
    /// The field's node: <see cref="LayoutNode.Changed"/> on a frame the secret changed, <see cref="LayoutNode.Submitted"/>
    /// on Enter while focused; as a <c>bool</c> either one.
    /// </returns>
    public static ControlNode PasswordInput(this Gui gui, ref string text, char maskChar = '*',
        string placeholder = "", bool enabled = true, IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        var node = FieldNode(gui, PasswordClass, classes, id, enabled, gui.ControlStyle.FieldWidth,
            gui.ControlStyle.FieldHeight, filePath, lineNumber);
        var original = text;
        using (node.AlignContentY(0.5f).Enter())
        {
            var fontSize = FieldFontSize(gui);
            gui.ClipContent();
            var nodeId = gui.CurrentNode.Id;
            gui.Focus.RegisterTextInput(nodeId);
            var state = TextEditor.State(gui, nodeId, text);

            if (enabled)
            {
                TextEditor.Process(gui, state, gui.GetInteractable(), fontSize,
                    displayText: new string(maskChar, state.Text.Length));
            }
            else
            {
                state.IsFocused = false;
            }

            var maskedText = new string(maskChar, state.Text.Length);
            DrawInputText(gui, maskedText, placeholder, fontSize, enabled);
            DrawCursor(gui, state, maskedText, fontSize);

            text = state.Text;
            RaiseFieldSignals(gui, node, original, text, EnterSubmits(gui, state));
        }

        return node;
    }

    /// <summary>A multi-line text field, styled as <c>input.area</c>; Enter inserts a line break.</summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="text">The edited text.</param>
    /// <param name="placeholder">Text shown while the field is empty.</param>
    /// <param name="enabled">When false the field matches <c>:disabled</c> and ignores input.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Optional id of the edit state and for <c>input#id</c> rules; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>The area's node: <see cref="LayoutNode.Changed"/> on a frame the text changed; as a <c>bool</c> the same.</returns>
    public static ControlNode TextArea(this Gui gui, ref string text, string placeholder = "", bool enabled = true,
        IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        var node = FieldNode(gui, AreaClass, classes, id, enabled, AreaWidth, AreaHeight, filePath, lineNumber);
        var original = text;
        using (node.AlignContentY(0.5f).Enter())
        {
            var fontSize = FieldFontSize(gui);
            gui.ClipContent();
            var nodeId = gui.CurrentNode.Id;
            gui.Focus.RegisterTextInput(nodeId);
            var state = TextEditor.State(gui, nodeId, text);

            if (enabled)
            {
                TextEditor.Process(gui, state, gui.GetInteractable(), fontSize, multiline: true);
            }
            else
            {
                state.IsFocused = false;
            }

            DrawSelectionMultiline(gui, state, state.Text, fontSize);
            DrawInputText(gui, state.Text, placeholder, fontSize, enabled);
            if (enabled)
            {
                DrawCursorMultiline(gui, state, state.Text, fontSize);
            }

            text = state.Text;
            RaiseFieldSignals(gui, node, original, text, submitted: false);
        }

        return node;
    }

    /// <summary>
    /// Clears all input states - useful for cleanup
    /// </summary>
    public static void ClearInputStates(this Gui gui)
    {
        gui.ClearControlStates<TextEditState>();
    }
}
