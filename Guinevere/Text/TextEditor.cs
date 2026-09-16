namespace Guinevere;

/// <summary>
/// The behaviour half of a text field, with no appearance of its own. It owns the value, the caret,
/// the selection, clipboard handling and hit-testing; a control built on it owns the box, the colours
/// and the glyphs. Keeping the two apart is what lets one application skin its fields like iOS and
/// another like Windows without either re-implementing text editing.
/// </summary>
/// <remarks>
/// Call <see cref="State"/> once per frame to fetch the field's state, then <see cref="Process"/> to
/// apply this frame's input. The measurement helpers report where the caret and selection fall so the
/// skin can paint them.
/// </remarks>
public static class TextEditor
{
    /// <summary>Seconds the caret spends in each half of its blink.</summary>
    private const float BlinkInterval = 0.5f;

    /// <summary>
    /// The state for a field, created on first use. The caller stays the source of truth: when the
    /// value it passes changes underneath the control — an inspector moving to another node, say — the
    /// field adopts it instead of showing the old one.
    /// </summary>
    /// <param name="gui">The GUI holding the per-control state.</param>
    /// <param name="id">Stable id for the field, normally its node id.</param>
    /// <param name="text">The value the caller wants shown.</param>
    /// <returns>The field's editing state.</returns>
    public static TextEditState State(Gui gui, string id, string text)
    {
        ArgumentNullException.ThrowIfNull(gui);

        var state = gui.ControlState(id, () => new TextEditState { Text = text, External = text });

        if (!string.Equals(state.External, text, StringComparison.Ordinal))
        {
            state.Text = text;
            state.External = text;
            state.MoveTo(text.Length, extend: false);
        }

        return state;
    }

    /// <summary>
    /// Applies one frame of pointer and keyboard editing. Only does anything in the render pass, where
    /// the node's rectangle is resolved.
    /// </summary>
    /// <param name="gui">The GUI for this frame.</param>
    /// <param name="state">The field's state, mutated in place.</param>
    /// <param name="interactable">The field's interactable, used for clicks and drags.</param>
    /// <param name="fontSize">Font size the text is drawn at, which hit-testing measures with.</param>
    /// <param name="multiline">True for a text area: Enter inserts a line break and Up/Down move by line.</param>
    /// <param name="displayText">
    /// What is actually on screen when it differs from <see cref="TextEditState.Text"/> — a password
    /// field's mask. Hit-testing measures this; editing still applies to the real value.
    /// </param>
    public static void Process(Gui gui, TextEditState state, InteractableElement interactable,
        float fontSize, bool multiline = false, string? displayText = null)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(state);

        if (gui.Pass != Pass.Pass2Render) return;

        HandleFocusAndClick(gui, state, interactable, fontSize, multiline, displayText ?? state.Text);
        HandleKeyboard(gui, state, multiline);
    }

    /// <summary>
    /// Applies this frame's typing and editing keys without touching focus or the pointer, for a
    /// control that decides on its own when the field is being edited — a scrubbable number field.
    /// </summary>
    /// <param name="gui">The GUI for this frame.</param>
    /// <param name="state">The field's state, mutated in place.</param>
    /// <param name="multiline">True to let Enter insert a line break.</param>
    public static void ProcessKeyboard(Gui gui, TextEditState state, bool multiline = false)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(state);

        HandleKeyboard(gui, state, multiline);
    }

    // ── pointer ─────────────────────────────────────────────────────────────

    private static void HandleFocusAndClick(Gui gui, TextEditState state, InteractableElement interactable,
        float fontSize, bool multiline, string display)
    {
        gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
        var hasFocus = gui.HasFocus();
        var inner = gui.CurrentNode.InnerRect;

        if (interactable.OnClick(out var clicks))
        {
            gui.RequestFocus(FocusReason.Mouse);
            var at = PositionAt(gui, gui.Input.MousePosition, inner, display, fontSize, multiline);

            if (clicks >= 3)
            {
                state.SelectAll();
            }
            else if (clicks >= 2)
                state.SelectAll();
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
                state.MoveTo(PositionAt(gui, gui.Input.MousePosition, inner, display, fontSize, multiline),
                    extend: true);
            else
                state.IsSelecting = false;
        }

        // Tabbing into a field selects its value, so typing replaces it. Clicking does not — hover was
        // the wrong proxy, since moving the pointer away after a click also looked like a tab.
        if (hasFocus && !state.IsFocused && gui.FocusReason == FocusReason.Keyboard) state.SelectAll();

        state.IsFocused = hasFocus;
    }

    // ── keyboard ────────────────────────────────────────────────────────────

    private static void HandleKeyboard(Gui gui, TextEditState state, bool multiline)
    {
        if (!state.IsFocused) return;

        Blink(state, gui.Time.DeltaTime);

        foreach (var c in gui.Input.GetTypedCharacters())
        {
            if (c >= 32 && c != 127) state.Insert(c.ToString());
            else if (multiline && c is '\r' or '\n') state.Insert("\n");
            else continue;

            state.ShowCursor = true;
            state.BlinkTimer = 0f;
        }

        HandleSpecialKeys(gui, state);
    }

    /// <summary>Advances the caret blink.</summary>
    /// <param name="state">The field's state.</param>
    /// <param name="deltaTime">Seconds since the previous frame.</param>
    public static void Blink(TextEditState state, float deltaTime)
    {
        ArgumentNullException.ThrowIfNull(state);

        state.BlinkTimer += deltaTime;
        if (state.BlinkTimer < BlinkInterval) return;

        state.BlinkTimer = 0f;
        state.ShowCursor = !state.ShowCursor;
    }

    private static void HandleSpecialKeys(Gui gui, TextEditState state)
    {
        var input = gui.Input;
        var extend = input.IsKeyDown(KeyboardKey.LeftShift) || input.IsKeyDown(KeyboardKey.RightShift);
        var word = input.IsKeyDown(KeyboardKey.LeftControl) || input.IsKeyDown(KeyboardKey.RightControl);

        if (HandleClipboard(gui, state, word)) return;

        if (input.IsKeyPressed(KeyboardKey.Backspace)) Backspace(state, word);
        else if (input.IsKeyPressed(KeyboardKey.Delete)) ForwardDelete(state, word);
        else if (input.IsKeyPressed(KeyboardKey.Left))
            state.MoveTo(word ? WordBoundaryLeft(state) : Collapse(state, extend, forward: false), extend);
        else if (input.IsKeyPressed(KeyboardKey.Right))
            state.MoveTo(word ? WordBoundaryRight(state) : Collapse(state, extend, forward: true), extend);
        else if (input.IsKeyPressed(KeyboardKey.Up)) MoveByLine(state, -1, extend);
        else if (input.IsKeyPressed(KeyboardKey.Down)) MoveByLine(state, 1, extend);
        else if (input.IsKeyPressed(KeyboardKey.Home)) state.MoveTo(LineStart(state), extend);
        else if (input.IsKeyPressed(KeyboardKey.End)) state.MoveTo(LineEnd(state), extend);
        else if (input.IsKeyPressed(KeyboardKey.Escape)) state.IsFocused = false;
        else return;

        state.ShowCursor = true;
        state.BlinkTimer = 0f;
    }

    private static bool HandleClipboard(Gui gui, TextEditState state, bool control)
    {
        if (!control) return false;

        var input = gui.Input;

        if (input.IsKeyPressed(KeyboardKey.A))
        {
            state.SelectAll();
            return true;
        }

        if (input.IsKeyPressed(KeyboardKey.C))
        {
            input.SetClipboardText(state.HasSelection ? state.SelectedText : state.Text);
            return true;
        }

        if (input.IsKeyPressed(KeyboardKey.X))
        {
            input.SetClipboardText(state.HasSelection ? state.SelectedText : state.Text);
            if (!state.DeleteSelection()) state.Text = string.Empty;
            state.MoveTo(state.CursorPosition, extend: false);
            return true;
        }

        if (input.IsKeyPressed(KeyboardKey.V))
        {
            var clipboard = input.GetClipboardText();
            if (!string.IsNullOrEmpty(clipboard)) state.Insert(clipboard);
            return true;
        }

        return false;
    }

    /// <summary>
    /// A plain arrow key with a selection collapses to that edge rather than moving one character,
    /// which is what every text field does.
    /// </summary>
    private static int Collapse(TextEditState state, bool extend, bool forward)
    {
        if (!extend && state.HasSelection) return forward ? state.SelectionEnd : state.SelectionStart;

        return state.CursorPosition + (forward ? 1 : -1);
    }

    private static void Backspace(TextEditState state, bool word)
    {
        if (state.DeleteSelection()) return;
        if (state.CursorPosition <= 0) return;

        var from = word ? WordBoundaryLeft(state) : state.CursorPosition - 1;
        state.Text = state.Text.Remove(from, state.CursorPosition - from);
        state.MoveTo(from, extend: false);
    }

    private static void ForwardDelete(TextEditState state, bool word)
    {
        if (state.DeleteSelection()) return;
        if (state.CursorPosition >= state.Text.Length) return;

        var to = word ? WordBoundaryRight(state) : state.CursorPosition + 1;
        state.Text = state.Text.Remove(state.CursorPosition, to - state.CursorPosition);
        state.MoveTo(state.CursorPosition, extend: false);
    }

    /// <summary>Moves the caret a line up or down, keeping the column when the target line is long enough.</summary>
    private static void MoveByLine(TextEditState state, int direction, bool extend)
    {
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

    /// <summary>The start of the line the caret is on.</summary>
    private static int LineStart(TextEditState state)
    {
        var position = Math.Clamp(state.CursorPosition, 0, state.Text.Length);
        if (position <= 0) return 0;
        return state.Text.LastIndexOf('\n', position - 1) + 1;
    }

    /// <summary>The end of the line the caret is on.</summary>
    private static int LineEnd(TextEditState state)
    {
        var position = Math.Clamp(state.CursorPosition, 0, state.Text.Length);
        var newline = state.Text.IndexOf('\n', position);
        return newline < 0 ? state.Text.Length : newline;
    }

    /// <summary>The start of the word to the caret's left, skipping any run of spaces first.</summary>
    private static int WordBoundaryLeft(TextEditState state)
    {
        var i = Math.Clamp(state.CursorPosition, 0, state.Text.Length);
        while (i > 0 && char.IsWhiteSpace(state.Text[i - 1])) i--;
        while (i > 0 && !char.IsWhiteSpace(state.Text[i - 1])) i--;
        return i;
    }

    /// <summary>The end of the word to the caret's right, then any run of spaces after it.</summary>
    private static int WordBoundaryRight(TextEditState state)
    {
        var i = Math.Clamp(state.CursorPosition, 0, state.Text.Length);
        while (i < state.Text.Length && !char.IsWhiteSpace(state.Text[i])) i++;
        while (i < state.Text.Length && char.IsWhiteSpace(state.Text[i])) i++;
        return i;
    }

    /// <summary>The run of word characters containing an index, for double-click selection.</summary>
    /// <param name="text">The text to look in.</param>
    /// <param name="index">The offset the word must contain.</param>
    /// <returns>The word's start and end offsets.</returns>
    public static (int Start, int End) WordAt(string text, int index)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0) return (0, 0);

        var i = Math.Clamp(index, 0, text.Length - 1);
        var start = i;
        var end = i;

        while (start > 0 && !char.IsWhiteSpace(text[start - 1])) start--;
        while (end < text.Length && !char.IsWhiteSpace(text[end])) end++;

        return (start, end);
    }

    // ── measurement, for whoever paints the field ───────────────────────────

    /// <summary>
    /// A font measuring with the same typeface the text will be drawn with. Measuring against the
    /// default typeface instead drifts further from the glyphs the longer the line gets.
    /// </summary>
    /// <param name="gui">The GUI whose current node scope carries the font.</param>
    /// <param name="fontSize">Size to measure at.</param>
    /// <returns>A font for measuring.</returns>
    public static SKFont MeasuringFont(Gui gui, float fontSize)
    {
        ArgumentNullException.ThrowIfNull(gui);
        return new SKFont(gui.CurrentNodeScope.Get<LayoutNodeScopeTextFont>().Value.SkFont.Typeface, fontSize);
    }

    /// <summary>Width of a run of text.</summary>
    /// <param name="font">Font to measure with, from <see cref="MeasuringFont"/>.</param>
    /// <param name="text">The run to measure.</param>
    /// <returns>The width in pixels.</returns>
    public static float MeasureWidth(SKFont font, string text)
    {
        ArgumentNullException.ThrowIfNull(font);
        return string.IsNullOrEmpty(text) ? 0f : font.MeasureText(text);
    }

    /// <summary>
    /// Where the text actually begins inside the field, which is not the left edge once the content is
    /// aligned right or centred.
    /// </summary>
    /// <param name="gui">The GUI whose current node carries the alignment.</param>
    /// <param name="font">Font the text is measured with.</param>
    /// <param name="text">The text being laid out.</param>
    /// <param name="inner">The field's inner rectangle.</param>
    /// <returns>The x the first glyph starts at.</returns>
    public static float TextOriginX(Gui gui, SKFont font, string text, Rect inner)
    {
        ArgumentNullException.ThrowIfNull(gui);

        var align = gui.CurrentNode.Style.AlignContentHorizontal;
        if (align <= 0f) return inner.X;

        var slack = Math.Max(0f, inner.W - MeasureWidth(font, text));
        return inner.X + (slack * align);
    }

    /// <summary>The caret offset nearest a point.</summary>
    /// <param name="gui">The GUI for measuring.</param>
    /// <param name="point">The point, in screen space.</param>
    /// <param name="inner">The field's inner rectangle.</param>
    /// <param name="text">The text on screen.</param>
    /// <param name="fontSize">Size the text is drawn at.</param>
    /// <param name="multiline">True to resolve the line first, then the column.</param>
    /// <returns>An offset into <paramref name="text"/>.</returns>
    public static int PositionAt(Gui gui, Vector2 point, Rect inner, string text, float fontSize,
        bool multiline = false)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!multiline) return PositionInLine(gui, point, inner, text, fontSize);

        var lineHeight = fontSize * 1.2f;
        var lines = text.Split('\n');
        var targetLine = Math.Clamp((int)((point.Y - inner.Y) / lineHeight), 0, lines.Length - 1);
        var lineText = lines[targetLine];

        var lineRect = new Rect(inner.X, inner.Y + (targetLine * lineHeight), inner.W, lineHeight);
        return PositionOfLine(lines, targetLine) + PositionInLine(gui, point, lineRect, lineText, fontSize);
    }

    private static int PositionInLine(Gui gui, Vector2 point, Rect inner, string text, float fontSize)
    {
        var font = MeasuringFont(gui, fontSize);

        // Measured from where the text starts, which is not the left edge in an aligned field.
        var clickX = point.X - TextOriginX(gui, font, text, inner);

        return Enumerable.Range(0, text.Length + 1)
            .Select(i => new { Position = i, X = MeasureWidth(font, text[..i]) })
            .OrderBy(p => Math.Abs(clickX - p.X))
            .First().Position;
    }

    /// <summary>The (row, column) of an offset, splitting on line breaks.</summary>
    /// <param name="lines">The text split on <c>\n</c>.</param>
    /// <param name="position">The offset to locate.</param>
    /// <returns>The zero-based row and column.</returns>
    public static (int Row, int Column) LineAndColumn(string[] lines, int position)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var offset = 0;
        for (var row = 0; row < lines.Length; row++)
        {
            if (position <= offset + lines[row].Length) return (row, position - offset);
            offset += lines[row].Length + 1;
        }

        return (lines.Length - 1, lines.Length > 0 ? lines[^1].Length : 0);
    }

    /// <summary>The offset the given row starts at.</summary>
    /// <param name="lines">The text split on <c>\n</c>.</param>
    /// <param name="row">The zero-based row.</param>
    /// <returns>The offset of the row's first character.</returns>
    public static int PositionOfLine(string[] lines, int row)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return lines.Take(row).Sum(line => line.Length + 1);
    }
}
