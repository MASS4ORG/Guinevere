using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    private class InputState
    {
        public string Text = "";
        public int CursorPosition;
        public bool IsFocused;
        public float BlinkTimer;
        public bool ShowCursor = true;

        /// <summary>Where the current selection was started. Equal to the cursor when nothing is selected.</summary>
        public int SelectionAnchor;

        /// <summary>Set while the pointer is dragging out a selection.</summary>
        public bool IsSelecting;

        /// <summary>The last value the caller supplied, so an external change can be told from an edit.</summary>
        public string External = "";

        public int SelectionStart => Math.Min(SelectionAnchor, CursorPosition);
        public int SelectionEnd => Math.Max(SelectionAnchor, CursorPosition);
        public bool HasSelection => SelectionStart != SelectionEnd;
        public string SelectedText => HasSelection
            ? Text[Math.Clamp(SelectionStart, 0, Text.Length)..Math.Clamp(SelectionEnd, 0, Text.Length)]
            : string.Empty;

        /// <summary>Moves the cursor, extending the selection when <paramref name="extend"/> is set.</summary>
        public void MoveTo(int position, bool extend)
        {
            CursorPosition = Math.Clamp(position, 0, Text.Length);
            if (!extend) SelectionAnchor = CursorPosition;

            ShowCursor = true;
            BlinkTimer = 0f;
        }

        /// <summary>Removes the selected run and leaves the cursor where it was.</summary>
        public bool DeleteSelection()
        {
            if (!HasSelection) return false;

            var start = Math.Clamp(SelectionStart, 0, Text.Length);
            var end = Math.Clamp(SelectionEnd, 0, Text.Length);

            Text = Text.Remove(start, end - start);
            CursorPosition = start;
            SelectionAnchor = start;
            return true;
        }

        /// <summary>Replaces the selection, or inserts at the cursor when there is none.</summary>
        public void Insert(string value)
        {
            DeleteSelection();
            Text = Text.Insert(Math.Clamp(CursorPosition, 0, Text.Length), value);
            MoveTo(CursorPosition + value.Length, extend: false);
        }

        public void SelectAll()
        {
            SelectionAnchor = 0;
            CursorPosition = Text.Length;
        }
    }

    private static InputState GetOrCreateState(Gui gui, string nodeId, string initialText)
    {
        var state = gui.ControlState(nodeId, () => new InputState { Text = initialText, External = initialText });

        // The caller is the source of truth: when the value it passes changes underneath the control —
        // the inspector moving to another node, say — the field adopts it instead of showing the old one.
        if (!string.Equals(state.External, initialText, StringComparison.Ordinal))
        {
            state.Text = initialText;
            state.External = initialText;
            state.MoveTo(initialText.Length, extend: false);
        }

        return state;
    }

    private static void UpdateCursorBlink(InputState state, float deltaTime)
    {
        state.BlinkTimer += deltaTime;
        if (state.BlinkTimer >= 0.5f) // Faster blinking - 0.5 seconds
        {
            state.BlinkTimer = 0f;
            state.ShowCursor = !state.ShowCursor;
        }
    }

    private static int GetCursorPositionFromClick(Gui gui, Vector2 mousePos, Rect innerRect, string text,
        float fontSize)
    {
        var font = MeasuringFont(gui, fontSize);

        // Measured from where the text starts, which is not the left edge in an aligned field.
        var clickX = mousePos.X - TextOriginX(gui, font, text, innerRect);

        return Enumerable.Range(0, text.Length + 1)
            .Select(i => new { Position = i, X = MeasureTextWidth(font, text.Substring(0, i)) })
            .OrderBy(p => Math.Abs(clickX - p.X))
            .First().Position;
    }

    private static int CalculateCursorPositionFromClickMultiline(Gui gui, Vector2 mousePos, Rect innerRect,
        string text, float fontSize)
    {
        var clickY = mousePos.Y - innerRect.Y;
        var lineHeight = fontSize * 1.2f;
        var lines = text.Split('\n');
        var targetLine = Math.Max(0, Math.Min((int)(clickY / lineHeight), lines.Length - 1));

        var positionBeforeTargetLine = lines.Take(targetLine).Sum(line => line.Length + 1); // +1 for \n
        var targetLineText = targetLine < lines.Length ? lines[targetLine] : "";
        var positionInLine = GetCursorPositionFromClick(gui, mousePos,
            innerRect with { Y = innerRect.Y + targetLine * lineHeight }, targetLineText, fontSize);

        return Math.Min(positionBeforeTargetLine + positionInLine, text.Length);
    }

    /// <summary>
    /// A font that measures exactly what <see cref="Gui.DrawText"/> will draw. Measuring with the
    /// default typeface instead drifts further from the glyphs the longer the line gets.
    /// </summary>
    private static SKFont MeasuringFont(Gui gui, float fontSize) =>
        new(gui.CurrentNodeScope.Get<LayoutNodeScopeTextFont>().Value.SkFont.Typeface, fontSize);

    private static float MeasureTextWidth(SKFont font, string text) =>
        string.IsNullOrEmpty(text) ? 0f : font.MeasureText(text);

    private static InputState HandleFocusAndClick(InputState state, InteractableElement interactable, Gui gui,
        Func<Gui, Vector2, Rect, string, float, int> calculateCursorPosition, string text, float fontSize)
    {
        if (gui.Pass != Pass.Pass2Render) return state;

        // Register this text input as focusable
        gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);

        // Check if this control has focus from the focus manager
        var hasFocus = gui.HasFocus();

        var inner = gui.CurrentNode.InnerRect;

        if (interactable.OnClick(out var clicks))
        {
            gui.RequestFocus(FocusReason.Mouse);
            var at = calculateCursorPosition(gui, gui.Input.MousePosition, inner, text, fontSize);

            if (clicks >= 3)
            {
                state.SelectAll();
            }
            else if (clicks == 2)
            {
                var (wordStart, wordEnd) = WordAt(text, at);
                state.SelectionAnchor = wordStart;
                state.CursorPosition = wordEnd;
            }
            else
            {
                state.MoveTo(at, extend: gui.Input.IsKeyDown(KeyboardKey.LeftShift));
                state.IsSelecting = true;
            }

            state.ShowCursor = true;
            state.BlinkTimer = 0f;
        }

        // Dragging after a press extends the selection to wherever the pointer is.
        if (state.IsSelecting)
        {
            if (gui.Input.IsMouseButtonDown(MouseButton.Left))
                state.MoveTo(calculateCursorPosition(gui, gui.Input.MousePosition, inner, text, fontSize), extend: true);
            else
                state.IsSelecting = false;
        }

        // Tabbing into a field selects its value, so typing replaces it. Clicking does not — hover was
        // the wrong proxy, since moving the pointer away after a click also looked like a tab.
        if (hasFocus && !state.IsFocused && gui.FocusReason == FocusReason.Keyboard) state.SelectAll();

        state.IsFocused = hasFocus;

        return state;
    }

    private static InputState HandleKeyboardInput(InputState state, Gui gui)
    {
        if (!state.IsFocused || gui.Pass != Pass.Pass2Render) return state;

        // Update blink timer
        UpdateCursorBlink(state, gui.Time.DeltaTime);

        // Handle typed characters
        gui.Input.GetTypedCharacters()
            .Where(c => c >= 32 && c != 127) // Printable characters only
            .Aggregate(state, (s, c) =>
            {
                // Insert replaces the selection, the way typing over selected text does everywhere.
                s.Insert(c.ToString());
                return s;
            });

        // Handle special keys
        return HandleSpecialKeys(state, gui);
    }

    private static InputState HandleKeyboardInputMultiline(InputState state, Gui gui)
    {
        if (!state.IsFocused || gui.Pass != Pass.Pass2Render) return state;

        // Update blink timer
        UpdateCursorBlink(state, gui.Time.DeltaTime);

        // Handle typed characters including newlines
        gui.Input.GetTypedCharacters()
            .Aggregate(state, (s, c) =>
            {
                if (c >= 32 && c != 127) // Printable characters
                {
                    s.Text = s.Text.Insert(s.CursorPosition, c.ToString());
                    s.CursorPosition++;
                }
                else if (c is '\r' or '\n') // Handle Enter for new lines
                {
                    s.Text = s.Text.Insert(s.CursorPosition, "\n");
                    s.CursorPosition++;
                }

                s.ShowCursor = true; // Show cursor when typing
                s.BlinkTimer = 0f; // Reset blink timer
                return s;
            });

        return HandleSpecialKeys(state, gui);
    }

    private static InputState HandleSpecialKeys(InputState state, Gui gui)
    {
        var extend = gui.Input.IsKeyDown(KeyboardKey.LeftShift) || gui.Input.IsKeyDown(KeyboardKey.RightShift);
        var word = gui.Input.IsKeyDown(KeyboardKey.LeftControl) || gui.Input.IsKeyDown(KeyboardKey.RightControl);

        if (HandleClipboard(state, gui, word)) return state;

        if (gui.Input.IsKeyPressed(KeyboardKey.Backspace)) Backspace(state, word);
        else if (gui.Input.IsKeyPressed(KeyboardKey.Delete)) ForwardDelete(state, word);
        else if (gui.Input.IsKeyPressed(KeyboardKey.Left))
            state.MoveTo(word ? WordBoundaryLeft(state) : Collapse(state, extend, forward: false), extend);
        else if (gui.Input.IsKeyPressed(KeyboardKey.Right))
            state.MoveTo(word ? WordBoundaryRight(state) : Collapse(state, extend, forward: true), extend);
        else if (gui.Input.IsKeyPressed(KeyboardKey.Up)) MoveToLine(state, gui, -1);
        else if (gui.Input.IsKeyPressed(KeyboardKey.Down)) MoveToLine(state, gui, 1);
        else if (gui.Input.IsKeyPressed(KeyboardKey.Home)) state.MoveTo(LineStart(state), extend);
        else if (gui.Input.IsKeyPressed(KeyboardKey.End)) state.MoveTo(LineEnd(state), extend);
        else if (gui.Input.IsKeyPressed(KeyboardKey.Escape)) state.IsFocused = false;
        else return state;

        state.ShowCursor = true;
        state.BlinkTimer = 0f;
        return state;
    }

    /// <summary>
    /// A plain arrow key with a selection collapses to that edge rather than moving one character,
    /// which is what every text field does.
    /// </summary>
    private static int Collapse(InputState state, bool extend, bool forward)
    {
        if (!extend && state.HasSelection) return forward ? state.SelectionEnd : state.SelectionStart;

        return state.CursorPosition + (forward ? 1 : -1);
    }

    /// <summary>Returns the (row, column) of a position, splitting on line breaks.</summary>
    private static (int Row, int Column) LineAndColumn(string[] lines, int position)
    {
        var offset = 0;
        for (var row = 0; row < lines.Length; row++)
        {
            if (position <= offset + lines[row].Length) return (row, position - offset);
            offset += lines[row].Length + 1;
        }
        return (lines.Length - 1, lines.Length > 0 ? lines[^1].Length : 0);
    }

    private static int PositionOfLine(string[] lines, int row) =>
        lines.Take(row).Sum(line => line.Length + 1);

    /// <summary>Moves the cursor a line up or down, keeping the column when the target line is long enough.</summary>
    private static void MoveToLine(InputState state, Gui gui, int direction)
    {
        var extend = gui.Input.IsKeyDown(KeyboardKey.LeftShift) || gui.Input.IsKeyDown(KeyboardKey.RightShift);
        var lines = state.Text.Split('\n');
        var (row, column) = LineAndColumn(lines, state.CursorPosition);
        var targetRow = Math.Clamp(row + direction, 0, lines.Length - 1);

        if (targetRow == row)
        {
            state.MoveTo(direction < 0 ? 0 : state.Text.Length, extend);
            return;
        }

        state.MoveTo(PositionOfLine(lines, targetRow) + Math.Min(column, lines[targetRow].Length), extend);
    }

    /// <summary>The start of the line the cursor is on.</summary>
    private static int LineStart(InputState state)
    {
        var position = Math.Clamp(state.CursorPosition, 0, state.Text.Length);
        if (position <= 0) return 0;
        return state.Text.LastIndexOf('\n', position - 1) + 1;
    }

    /// <summary>The end of the line the cursor is on.</summary>
    private static int LineEnd(InputState state)
    {
        var position = Math.Clamp(state.CursorPosition, 0, state.Text.Length);
        var newline = state.Text.IndexOf('\n', position);
        return newline < 0 ? state.Text.Length : newline;
    }

    private static void Backspace(InputState state, bool word)
    {
        if (state.DeleteSelection()) return;
        if (state.CursorPosition <= 0) return;

        var from = word ? WordBoundaryLeft(state) : state.CursorPosition - 1;
        state.Text = state.Text.Remove(from, state.CursorPosition - from);
        state.MoveTo(from, extend: false);
    }

    private static void ForwardDelete(InputState state, bool word)
    {
        if (state.DeleteSelection()) return;
        if (state.CursorPosition >= state.Text.Length) return;

        var to = word ? WordBoundaryRight(state) : state.CursorPosition + 1;
        state.Text = state.Text.Remove(state.CursorPosition, to - state.CursorPosition);
        state.MoveTo(state.CursorPosition, extend: false);
    }

    private static bool HandleClipboard(InputState state, Gui gui, bool control)
    {
        if (!control) return false;

        if (gui.Input.IsKeyPressed(KeyboardKey.A))
        {
            state.SelectAll();
            return true;
        }

        if (gui.Input.IsKeyPressed(KeyboardKey.C))
        {
            gui.Input.SetClipboardText(state.HasSelection ? state.SelectedText : state.Text);
            return true;
        }

        if (gui.Input.IsKeyPressed(KeyboardKey.X))
        {
            gui.Input.SetClipboardText(state.HasSelection ? state.SelectedText : state.Text);
            if (!state.DeleteSelection()) state.Text = string.Empty;
            state.MoveTo(state.CursorPosition, extend: false);
            return true;
        }

        if (gui.Input.IsKeyPressed(KeyboardKey.V))
        {
            var clipboard = gui.Input.GetClipboardText();
            if (!string.IsNullOrEmpty(clipboard)) state.Insert(clipboard);
            return true;
        }

        return false;
    }

    /// <summary>The start of the word to the cursor's left, skipping any run of spaces first.</summary>
    private static int WordBoundaryLeft(InputState state)
    {
        var i = Math.Clamp(state.CursorPosition, 0, state.Text.Length);
        while (i > 0 && char.IsWhiteSpace(state.Text[i - 1])) i--;
        while (i > 0 && !char.IsWhiteSpace(state.Text[i - 1])) i--;
        return i;
    }

    /// <summary>The end of the word to the cursor's right, then any run of spaces after it.</summary>
    private static int WordBoundaryRight(InputState state)
    {
        var i = Math.Clamp(state.CursorPosition, 0, state.Text.Length);
        while (i < state.Text.Length && !char.IsWhiteSpace(state.Text[i])) i++;
        while (i < state.Text.Length && char.IsWhiteSpace(state.Text[i])) i++;
        return i;
    }

    /// <summary>The run of word characters containing an index, for double-click selection.</summary>
    private static (int Start, int End) WordAt(string text, int index)
    {
        if (text.Length == 0) return (0, 0);

        var i = Math.Clamp(index, 0, text.Length - 1);
        var start = i;
        var end = i;

        while (start > 0 && !char.IsWhiteSpace(text[start - 1])) start--;
        while (end < text.Length && !char.IsWhiteSpace(text[end])) end++;

        return (start, end);
    }

    /// <summary>
    /// Shrinks the padding so it never eats the whole field. A short input keeps a little breathing
    /// room instead of squeezing its text out of the box.
    /// </summary>
    private static float FitPadding(float height, float padding) =>
        height <= 0 ? padding : Math.Min(padding, Math.Max(2f, (height - 4f) / 2f));

    private static void DrawInputBackground(Gui gui, InputState state, Color? backgroundColor, Color? borderColor)
    {
        var fill = backgroundColor ?? gui.Controls.Surface;
        var outline = borderColor ?? gui.Controls.Border;
        var borderWidth = 1f;

        if (state.IsFocused)
        {
            outline = gui.Controls.Accent;
            borderWidth = 2f;
        }

        gui.DrawBackgroundRect(fill);
        gui.DrawRectBorder(gui.CurrentNode.Rect, outline, borderWidth);
    }

    /// <summary>
    /// Where the text actually begins inside the field, which is not the left edge once the content is
    /// aligned right or centred.
    /// </summary>
    private static float TextOriginX(Gui gui, SKFont font, string text, Rect innerRect)
    {
        var align = gui.CurrentNode.Style.AlignContentHorizontal;
        if (align <= 0f) return innerRect.X;

        var slack = Math.Max(0f, innerRect.W - MeasureTextWidth(font, text));
        return innerRect.X + (slack * align);
    }

    /// <summary>Paints the selected run behind the glyphs, so the text stays readable over it.</summary>
    private static void DrawSelection(Gui gui, InputState state, string text, float fontSize)
    {
        if (!state.IsFocused || !state.HasSelection || gui.Pass != Pass.Pass2Render) return;

        var font = MeasuringFont(gui, fontSize);
        var inner = gui.CurrentNode.InnerRect;

        var start = Math.Clamp(state.SelectionStart, 0, text.Length);
        var end = Math.Clamp(state.SelectionEnd, 0, text.Length);

        var origin = TextOriginX(gui, font, text, inner);
        var x1 = origin + MeasureTextWidth(font, text[..start]);
        var x2 = origin + MeasureTextWidth(font, text[..end]);

        gui.DrawRect(new Rect(x1, inner.Y, Math.Max(1f, x2 - x1), inner.H),
            Color.FromArgb(110, gui.Controls.Accent));
    }

    /// <summary>Paints the selected run per line in a multi-line field, so text areas get the same highlight as inputs.</summary>
    private static void DrawSelectionMultiline(Gui gui, InputState state, string text, float fontSize)
    {
        if (!state.IsFocused || !state.HasSelection || gui.Pass != Pass.Pass2Render) return;

        var font = MeasuringFont(gui, fontSize);
        var lineHeight = fontSize * 1.2f;
        var inner = gui.CurrentNode.InnerRect;

        var start = Math.Clamp(state.SelectionStart, 0, text.Length);
        var end = Math.Clamp(state.SelectionEnd, 0, text.Length);
        if (start == end) return;

        var lines = text.Split('\n');
        var (startRow, startCol) = LineAndColumn(lines, start);
        var (endRow, endCol) = LineAndColumn(lines, end);

        for (var row = startRow; row <= endRow && row < lines.Length; row++)
        {
            var colStart = row == startRow ? startCol : 0;
            var colEnd = row == endRow ? endCol : lines[row].Length;
            if (colEnd <= colStart) continue;

            var x1 = inner.X + MeasureTextWidth(font, lines[row][..colStart]);
            var x2 = inner.X + MeasureTextWidth(font, lines[row][..colEnd]);
            var y = inner.Y + row * lineHeight;

            gui.DrawRect(new Rect(x1, y, Math.Max(1f, x2 - x1), lineHeight),
                Color.FromArgb(110, gui.Controls.Accent));
        }
    }

    private static void DrawInputText(Gui gui, string displayText, string placeholder, float fontSize,
        Color? textColor, Color? placeholderColor)
    {
        var finalDisplayText = string.IsNullOrEmpty(displayText) ? placeholder : displayText;
        var finalColor = string.IsNullOrEmpty(displayText)
            ? placeholderColor ?? gui.Controls.TextDim
            : textColor ?? gui.Controls.Text;

        if (!string.IsNullOrEmpty(finalDisplayText))
            gui.DrawText(finalDisplayText, fontSize, finalColor, centerInRect: false);
    }

    private static void DrawCursor(Gui gui, InputState state, string text, float fontSize, Color cursorColor)
    {
        if (!state.IsFocused || !state.ShowCursor || gui.Pass != Pass.Pass2Render) return;

        var font = MeasuringFont(gui, fontSize);
        var textBeforeCursor = text.Substring(0, Math.Min(state.CursorPosition, text.Length));
        var textWidth = MeasureTextWidth(font, textBeforeCursor);

        var innerRect = gui.CurrentNode.InnerRect;
        var cursorX = TextOriginX(gui, font, text, innerRect) + textWidth;
        var cursorY1 = innerRect.Y;
        var cursorY2 = innerRect.Y + innerRect.H;

        gui.DrawRect(new Rect(cursorX, cursorY1, 1.5f, cursorY2 - cursorY1), cursorColor);
    }

    private static void DrawCursorMultiline(Gui gui, InputState state, string text, float fontSize, Color? cursorColor)
    {
        if (!state.IsFocused || !state.ShowCursor || gui.Pass != Pass.Pass2Render) return;

        var font = MeasuringFont(gui, fontSize);
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

        var textWidth = MeasureTextWidth(font, textBeforeCursor);
        var cursorY = cursorLine * lineHeight + 2;
        var cursorHeight = Math.Max(2, lineHeight - 4);

        // Ensure cursor is within the visible area
        if (cursorY >= 0 && cursorY < innerRect.Height)
            // Use a nested node for cursor positioning to avoid coordinate transformation issues
            using (gui.Node(2, cursorHeight).Margin(textWidth, cursorY, 0, 0).Enter())
            {
                gui.DrawRect(gui.CurrentNode.Rect, cursorColor ?? Color.White);
            }
    }

    /// <summary>
    /// Creates a text input field with ref parameter
    /// </summary>
    public static void TextInput(this Gui gui, ref string text,
        float width = 200, float height = 32, string placeholder = "",
        Color? backgroundColor = null, Color? borderColor = null, Color? textColor = null,
        Color? placeholderColor = null, Color? cursorColor = null, float fontSize = 14,
        float padding = 8, bool enabled = true, string id = "", float alignX = 0f)
    {
        var nodeId = string.IsNullOrEmpty(id) ? gui.NodeId("TextInput", 0) : id;

        var cursorColorFinal = cursorColor ?? textColor ?? gui.CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value;
        using (gui.Node(width, height).Padding(FitPadding(height, padding))
                   .ContentAlignX(alignX).ContentAlignY(0.5f).Enter())
        {
            var state = GetOrCreateState(gui, nodeId, text);
            var interactable = gui.GetInteractable();

            if (enabled)
            {
                state = HandleFocusAndClick(state, interactable, gui, GetCursorPositionFromClick, state.Text, fontSize);
                state = HandleKeyboardInput(state, gui);
            }
            else
            {
                state.IsFocused = false;
            }

            // Rendering
            DrawInputBackground(gui, state, backgroundColor, borderColor);
            DrawSelection(gui, state, state.Text, fontSize);
            DrawInputText(gui, state.Text, placeholder, fontSize, textColor, placeholderColor);
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
    /// <returns>The updated value of the text in the input field.</returns>
    public static string TextInput(this Gui gui, string text,
        float width = 200, float height = 32, string placeholder = "",
        Color? backgroundColor = null, Color? borderColor = null, Color? textColor = null,
        Color? placeholderColor = null, Color? cursorColor = null, float fontSize = 14,
        float padding = 8, bool enabled = true, string id = "", float alignX = 0f)
    {
        gui.TextInput(ref text, width, height, placeholder, backgroundColor, borderColor,
            textColor, placeholderColor, cursorColor, fontSize, padding, enabled, id, alignX);
        return text;
    }

    /// <summary>
    /// Password input field with masked text (ref parameter)
    /// </summary>
    public static void PasswordInput(this Gui gui, ref string text,
        float width = 200, float height = 32, char maskChar = '*', string placeholder = "",
        Color? backgroundColor = null, Color? borderColor = null, Color? textColor = null,
        Color? placeholderColor = null, Color? cursorColor = null, float fontSize = 14,
        float padding = 8, bool enabled = true, string id = "")
    {
        var nodeId = string.IsNullOrEmpty(id) ? gui.NodeId("PasswordInput", 0) : id;

        var cursorColorFinal = cursorColor ?? textColor ?? gui.CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value;
        using (gui.Node(width, height).Padding(FitPadding(height, padding)).ContentAlignY(0.5f).Enter())
        {
            var state = GetOrCreateState(gui, nodeId, text);
            var interactable = gui.GetInteractable();

            if (enabled)
            {
                var stateTemp = state;
                state = HandleFocusAndClick(state, interactable, gui,
                    (g, mousePos, rect, _, textFontSize) => GetCursorPositionFromClick(g, mousePos, rect,
                        new string(maskChar, stateTemp.Text.Length), textFontSize),
                    state.Text, fontSize);
                state = HandleKeyboardInput(state, gui);
            }
            else
            {
                state.IsFocused = false;
            }

            // Rendering with masked text
            var maskedText = new string(maskChar, state.Text.Length);
            DrawInputBackground(gui, state, backgroundColor, borderColor);
            DrawInputText(gui, maskedText, placeholder, fontSize, textColor, placeholderColor);
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
        float width = 200, float height = 32, char maskChar = '*', string placeholder = "",
        Color? backgroundColor = null, Color? borderColor = null, Color? textColor = null,
        Color? placeholderColor = null, Color? cursorColor = null, float fontSize = 14,
        float padding = 8, bool enabled = true, string id = "")
    {
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
        float fontSize = 14,
        float padding = 8,
        bool enabled = true,
        string id = "")
    {
        var nodeId = string.IsNullOrEmpty(id) ? gui.NodeId("TextArea", 0) : id;

        using (gui.Node(width, height).Padding(FitPadding(height, padding)).ContentAlignY(0.5f).Enter())
        {
            var state = GetOrCreateState(gui, nodeId, text);
            var interactable = gui.GetInteractable();

            // Only process input if enabled
            if (enabled)
            {
                state = HandleFocusAndClick(state, interactable, gui, CalculateCursorPositionFromClickMultiline,
                    state.Text, fontSize);
                state = HandleKeyboardInputMultiline(state, gui);
            }

            // Rendering - let the parent handle clipping/scrolling to avoid nested contexts
            DrawInputBackground(gui, state, backgroundColor, borderColor);
            DrawSelectionMultiline(gui, state, state.Text, fontSize);
            DrawInputText(gui, state.Text, placeholder, fontSize, textColor, placeholderColor);

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
        float fontSize = 14,
        float padding = 8,
        bool enabled = true,
        string id = "")
    {
        gui.TextArea(ref text, width, height, placeholder, backgroundColor, borderColor,
            textColor, placeholderColor, cursorColor, fontSize, padding, enabled, id);
        return text;
    }

    /// <summary>
    /// Clears all input states - useful for cleanup
    /// </summary>
    public static void ClearInputStates(this Gui gui)
    {
        gui.ClearControlStates<InputState>();
    }
}
