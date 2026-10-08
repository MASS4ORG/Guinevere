using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    static readonly string[] PasswordClass = ["password"];
    static readonly string[] AreaClass = ["area"];

    /// <summary>
    /// Shrinks the padding so it never eats the whole field. A short input keeps a little breathing
    /// room instead of squeezing its text out of the box.
    /// </summary>
    static float FitPadding(float height, float padding) =>
        height <= 0 ? padding : Math.Min(padding, Math.Max(2f, (height - 4f) / 2f));

    /// <summary>
    /// The box of a text field: an <c>input</c> node with its variant and caller classes, styled from the GUI's
    /// sheets (<c>:focus</c>, <c>:disabled</c>), sized like <c>gui.Node(width, height)</c>.
    /// </summary>
    static LayoutNode FieldNode(Gui gui, string[]? variant, IReadOnlyList<string>? classes, string? id, bool enabled,
        float width, float height, float padding)
    {
        ExcaliburStyles.Ensure(gui);
        var all = classes is null ? variant : variant is null ? classes : [.. variant, .. classes];
        var node = gui.StyledNode("input", all, string.IsNullOrEmpty(id) ? null : id, disabled: !enabled);
        return Sized(node, width, height).Padding(FitPadding(height, padding));
    }

    /// <summary>Sizes a node the way <c>gui.Node(width, height)</c> does: 0 expands, a negative value fits the content.</summary>
    static LayoutNode Sized(LayoutNode node, float width, float height)
    {
        if (width == 0) node.ExpandWidth();
        else if (width > 0) node.Width(width);
        if (height == 0) node.ExpandHeight();
        else if (height > 0) node.Height(height);
        return node;
    }

    /// <summary>
    /// A color declared for a part the control draws itself, such as <c>input selection { background-color = …; }</c>,
    /// matched as a child of the current node.
    /// </summary>
    static Color PartColor(Gui gui, string type, string property, StyleState state = StyleState.None) =>
        gui.ResolvePart(type, state).GetColor(property) ?? Color.Transparent;

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
            PartColor(gui, "selection", "background-color"));
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
        var color = PartColor(gui, "selection", "background-color");

        for (var row = startRow; row <= endRow && row < lines.Length; row++)
        {
            var colStart = row == startRow ? startCol : 0;
            var colEnd = row == endRow ? endCol : lines[row].Length;
            if (colEnd <= colStart) continue;

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
        if (string.IsNullOrEmpty(shown)) return;
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
        if (!state.IsFocused || !state.ShowCursor || gui.Pass != Pass.Pass2Render) return;

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
        if (!state.IsFocused || !state.ShowCursor || gui.Pass != Pass.Pass2Render) return;

        var (line, column, lineText) = CaretLineAndColumn(text, state.CursorPosition);
        var lineHeight = fontSize * 1.2f;
        var inner = gui.CurrentNode.InnerRect;
        var cursorY = line * lineHeight + 2;
        if (cursorY < 0 || cursorY >= inner.Height) return;

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
                return (line, Math.Clamp(position - lineStart, 0, lineText.Length), lineText);

            lineStart = lineEnd + 1;
            line++;
        }
    }

    /// <summary>
    /// A single-line text field styled by the <c>input</c> rules of the GUI's sheets (<c>:focus</c>,
    /// <c>:disabled</c>); the placeholder is a <c>placeholder</c> child and the selection reads the
    /// <c>selection</c> rule. The caret uses the field's text color.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="text">The edited text.</param>
    /// <param name="width">Field width; 0 expands.</param>
    /// <param name="height">Field height; 0 expands.</param>
    /// <param name="placeholder">Text shown while the field is empty.</param>
    /// <param name="fontSize">Text size.</param>
    /// <param name="padding">Inner padding, shrunk to fit short fields.</param>
    /// <param name="enabled">When false the field matches <c>:disabled</c> and ignores input.</param>
    /// <param name="id">Stable identifier of the edit state, also the element id for <c>input#id</c> rules.</param>
    /// <param name="alignX">Horizontal alignment of the text, 0 left to 1 right.</param>
    /// <param name="grabFocus">Keeps the field focused without a click, for one that appears already
    /// being edited — an inline rename. Its value is selected when focus first lands.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    public static void TextInput(this Gui gui, ref string text,
        float width = ControlMetrics.FieldWidth, float height = ControlMetrics.FieldHeight, string placeholder = "",
        float fontSize = ControlMetrics.FontSize, float padding = ControlMetrics.Spacing, bool enabled = true,
        string id = "", float alignX = 0f, bool grabFocus = false, IReadOnlyList<string>? classes = null)
    {
        width = gui.ControlStyle.FieldWidthOr(width);
        height = gui.ControlStyle.FieldHeightOr(height);
        fontSize = gui.ControlStyle.FontSizeOr(fontSize);
        padding = gui.ControlStyle.SpacingOr(padding);

        var nodeId = string.IsNullOrEmpty(id) ? gui.NodeId("TextInput", 0) : id;
        gui.Focus.RegisterTextInput(nodeId);

        using (FieldNode(gui, null, classes, id, enabled, width, height, padding)
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

            DrawSelection(gui, state, state.Text, fontSize);
            DrawInputText(gui, state.Text, placeholder, fontSize, enabled);
            DrawCursor(gui, state, state.Text, fontSize);

            text = state.Text;
        }
    }

    /// <summary>A single-line text field that returns the edited text instead of changing a field.</summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="text">The current text.</param>
    /// <param name="width">Field width; 0 expands.</param>
    /// <param name="height">Field height; 0 expands.</param>
    /// <param name="placeholder">Text shown while the field is empty.</param>
    /// <param name="fontSize">Text size.</param>
    /// <param name="padding">Inner padding, shrunk to fit short fields.</param>
    /// <param name="enabled">When false the field matches <c>:disabled</c> and ignores input.</param>
    /// <param name="id">Stable identifier of the edit state, also the element id for <c>input#id</c> rules.</param>
    /// <param name="alignX">Horizontal alignment of the text, 0 left to 1 right.</param>
    /// <param name="grabFocus">Keeps the field focused without a click, for one that appears already
    /// being edited — an inline rename. Its value is selected when focus first lands.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <returns>The text after this frame's edits.</returns>
    public static string TextInput(this Gui gui, string text,
        float width = ControlMetrics.FieldWidth, float height = ControlMetrics.FieldHeight, string placeholder = "",
        float fontSize = ControlMetrics.FontSize, float padding = ControlMetrics.Spacing, bool enabled = true,
        string id = "", float alignX = 0f, bool grabFocus = false, IReadOnlyList<string>? classes = null)
    {
        gui.TextInput(ref text, width, height, placeholder, fontSize, padding, enabled, id, alignX, grabFocus, classes);
        return text;
    }

    /// <summary>
    /// A text field that shows <paramref name="maskChar"/> for every character, styled as <c>input.password</c>.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="text">The edited secret.</param>
    /// <param name="width">Field width; 0 expands.</param>
    /// <param name="height">Field height; 0 expands.</param>
    /// <param name="maskChar">Character shown in place of each typed one.</param>
    /// <param name="placeholder">Text shown while the field is empty.</param>
    /// <param name="fontSize">Text size.</param>
    /// <param name="padding">Inner padding, shrunk to fit short fields.</param>
    /// <param name="enabled">When false the field matches <c>:disabled</c> and ignores input.</param>
    /// <param name="id">Stable identifier of the edit state, also the element id for <c>input#id</c> rules.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    public static void PasswordInput(this Gui gui, ref string text,
        float width = ControlMetrics.FieldWidth, float height = ControlMetrics.FieldHeight, char maskChar = '*',
        string placeholder = "", float fontSize = ControlMetrics.FontSize, float padding = ControlMetrics.Spacing,
        bool enabled = true, string id = "", IReadOnlyList<string>? classes = null)
    {
        width = gui.ControlStyle.FieldWidthOr(width);
        height = gui.ControlStyle.FieldHeightOr(height);
        fontSize = gui.ControlStyle.FontSizeOr(fontSize);
        padding = gui.ControlStyle.SpacingOr(padding);

        var nodeId = string.IsNullOrEmpty(id) ? gui.NodeId("PasswordInput", 0) : id;
        gui.Focus.RegisterTextInput(nodeId);

        using (FieldNode(gui, PasswordClass, classes, id, enabled, width, height, padding).ContentAlignY(0.5f).Enter())
        {
            gui.ClipContent();
            var state = TextEditor.State(gui, nodeId, text);

            if (enabled)
                TextEditor.Process(gui, state, gui.GetInteractable(), fontSize,
                    displayText: new string(maskChar, state.Text.Length));
            else state.IsFocused = false;

            var maskedText = new string(maskChar, state.Text.Length);
            DrawInputText(gui, maskedText, placeholder, fontSize, enabled);
            DrawCursor(gui, state, maskedText, fontSize);

            text = state.Text;
        }
    }

    /// <summary>A masked text field that returns the edited secret instead of changing a field.</summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="text">The current secret.</param>
    /// <param name="width">Field width; 0 expands.</param>
    /// <param name="height">Field height; 0 expands.</param>
    /// <param name="maskChar">Character shown in place of each typed one.</param>
    /// <param name="placeholder">Text shown while the field is empty.</param>
    /// <param name="fontSize">Text size.</param>
    /// <param name="padding">Inner padding, shrunk to fit short fields.</param>
    /// <param name="enabled">When false the field matches <c>:disabled</c> and ignores input.</param>
    /// <param name="id">Stable identifier of the edit state, also the element id for <c>input#id</c> rules.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <returns>The secret after this frame's edits.</returns>
    public static string PasswordInput(this Gui gui, string text,
        float width = ControlMetrics.FieldWidth, float height = ControlMetrics.FieldHeight, char maskChar = '*',
        string placeholder = "", float fontSize = ControlMetrics.FontSize, float padding = ControlMetrics.Spacing,
        bool enabled = true, string id = "", IReadOnlyList<string>? classes = null)
    {
        gui.PasswordInput(ref text, width, height, maskChar, placeholder, fontSize, padding, enabled, id, classes);
        return text;
    }

    /// <summary>A multi-line text field, styled as <c>input.area</c>.</summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="text">The edited text.</param>
    /// <param name="width">Field width; 0 expands.</param>
    /// <param name="height">Field height; 0 expands.</param>
    /// <param name="placeholder">Text shown while the field is empty.</param>
    /// <param name="fontSize">Text size.</param>
    /// <param name="padding">Inner padding, shrunk to fit short fields.</param>
    /// <param name="enabled">When false the field matches <c>:disabled</c> and ignores input.</param>
    /// <param name="id">Stable identifier of the edit state, also the element id for <c>input#id</c> rules.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    public static void TextArea(this Gui gui, ref string text,
        float width = 300, float height = 100,
        string placeholder = "",
        float fontSize = ControlMetrics.FontSize,
        float padding = ControlMetrics.Spacing,
        bool enabled = true,
        string id = "",
        IReadOnlyList<string>? classes = null)
    {
        fontSize = gui.ControlStyle.FontSizeOr(fontSize);
        padding = gui.ControlStyle.SpacingOr(padding);

        var nodeId = string.IsNullOrEmpty(id) ? gui.NodeId("TextArea", 0) : id;
        gui.Focus.RegisterTextInput(nodeId);

        using (FieldNode(gui, AreaClass, classes, id, enabled, width, height, padding).ContentAlignY(0.5f).Enter())
        {
            gui.ClipContent();
            var state = TextEditor.State(gui, nodeId, text);

            if (enabled) TextEditor.Process(gui, state, gui.GetInteractable(), fontSize, multiline: true);
            else state.IsFocused = false;

            DrawSelectionMultiline(gui, state, state.Text, fontSize);
            DrawInputText(gui, state.Text, placeholder, fontSize, enabled);
            if (enabled) DrawCursorMultiline(gui, state, state.Text, fontSize);

            text = state.Text;
        }
    }

    /// <summary>A multi-line text field that returns the edited text instead of changing a field.</summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="text">The current text.</param>
    /// <param name="width">Field width; 0 expands.</param>
    /// <param name="height">Field height; 0 expands.</param>
    /// <param name="placeholder">Text shown while the field is empty.</param>
    /// <param name="fontSize">Text size.</param>
    /// <param name="padding">Inner padding, shrunk to fit short fields.</param>
    /// <param name="enabled">When false the field matches <c>:disabled</c> and ignores input.</param>
    /// <param name="id">Stable identifier of the edit state, also the element id for <c>input#id</c> rules.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <returns>The text after this frame's edits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string TextArea(this Gui gui, string text,
        float width = 300, float height = 100,
        string placeholder = "",
        float fontSize = ControlMetrics.FontSize,
        float padding = ControlMetrics.Spacing,
        bool enabled = true,
        string id = "",
        IReadOnlyList<string>? classes = null)
    {
        gui.TextArea(ref text, width, height, placeholder, fontSize, padding, enabled, id, classes);
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
