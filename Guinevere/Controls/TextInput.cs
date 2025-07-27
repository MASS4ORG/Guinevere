using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    private static readonly Dictionary<string, InputState> InputStates = new();

    private class InputState
    {
        public string Text = "";
        public int CursorPosition;
        public bool IsFocused;
        public float BlinkTimer;
        public bool ShowCursor = true;
    }

    private static InputState GetOrCreateState(string nodeId, string initialText)
    {
        return InputStates.TryGetValue(nodeId, out var state)
            ? state
            : InputStates[nodeId] = new InputState { Text = initialText };
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

    private static int GetCursorPositionFromClick(Vector2 mousePos, Rect innerRect, string text, float fontSize)
    {
        var clickX = mousePos.X - innerRect.X;
        var font = new SKFont { Size = fontSize };

        return Enumerable.Range(0, text.Length + 1)
            .Select(i => new { Position = i, X = MeasureTextWidth(font, text.Substring(0, i)) })
            .OrderBy(p => Math.Abs(clickX - p.X))
            .First().Position;
    }

    private static int CalculateCursorPositionFromClickMultiline(Vector2 mousePos, Rect innerRect, string text,
        float fontSize)
    {
        var clickY = mousePos.Y - innerRect.Y;
        var lineHeight = fontSize * 1.2f;
        var lines = text.Split('\n');
        var targetLine = Math.Max(0, Math.Min((int)(clickY / lineHeight), lines.Length - 1));

        var positionBeforeTargetLine = lines.Take(targetLine).Sum(line => line.Length + 1); // +1 for \n
        var targetLineText = targetLine < lines.Length ? lines[targetLine] : "";
        var positionInLine = GetCursorPositionFromClick(mousePos,
            innerRect with { Y = innerRect.Y + targetLine * lineHeight }, targetLineText, fontSize);

        return Math.Min(positionBeforeTargetLine + positionInLine, text.Length);
    }

    private static float MeasureTextWidth(SKFont font, string text)
    {
        font.MeasureText(text, out var bounds);
        return bounds.Width;
    }

    private static InputState HandleFocusAndClick(InputState state, InteractableElement interactable, Gui gui,
        Func<Vector2, Rect, string, float, int> calculateCursorPosition, string text, float fontSize)
    {
        if (gui.Pass != Pass.Pass2Render) return state;

        // Register this text input as focusable
        gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);

        // Check if this control has focus from the focus manager
        var hasFocus = gui.HasFocus();

        // Handle mouse click for focus
        if (interactable.OnClick())
        {
            gui.RequestFocus(FocusReason.Mouse);
            state.CursorPosition =
                calculateCursorPosition(gui.Input.MousePosition, gui.CurrentNode.InnerRect, text, fontSize);
            state.ShowCursor = true; // Show the cursor immediately when clicked
            state.BlinkTimer = 0f; // Reset blink timer
        }

        // Update the local focus state based on the global focus manager
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
                s.Text = s.Text.Insert(s.CursorPosition, c.ToString());
                s.CursorPosition++;
                s.ShowCursor = true; // Show cursor when typing
                s.BlinkTimer = 0f; // Reset blink timer
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
        var keyActions = new Dictionary<KeyboardKey, Action<InputState>>
        {
            [KeyboardKey.Backspace] = s =>
            {
                if (s.Text.Length > 0 && s.CursorPosition > 0)
                {
                    s.Text = s.Text.Remove(s.CursorPosition - 1, 1);
                    s.CursorPosition = Math.Max(0, s.CursorPosition - 1);
                }
            },
            [KeyboardKey.Delete] = s =>
            {
                if (s.CursorPosition < s.Text.Length)
                    s.Text = s.Text.Remove(s.CursorPosition, 1);
            },
            [KeyboardKey.Left] = s => s.CursorPosition = Math.Max(0, s.CursorPosition - 1),
            [KeyboardKey.Right] = s => s.CursorPosition = Math.Min(s.Text.Length, s.CursorPosition + 1),
            [KeyboardKey.Home] = s => s.CursorPosition = 0,
            [KeyboardKey.End] = s => s.CursorPosition = s.Text.Length,
            [KeyboardKey.Escape] = s => s.IsFocused = false
        };

        foreach (var (_, action) in keyActions.Where(kv => gui.Input.IsKeyPressed(kv.Key)))
        {
            action(state);
            state.ShowCursor = true; // Show cursor when navigating
            state.BlinkTimer = 0f; // Reset blink timer
        }

        // Handle clipboard operations
        if (gui.Input.IsKeyDown(KeyboardKey.LeftControl))
        {
            if (gui.Input.IsKeyPressed(KeyboardKey.V))
            {
                var clipboardText = gui.Input.GetClipboardText();
                if (!string.IsNullOrEmpty(clipboardText))
                {
                    state.Text = state.Text.Insert(state.CursorPosition, clipboardText);
                    state.CursorPosition += clipboardText.Length;
                    state.ShowCursor = true;
                    state.BlinkTimer = 0f;
                }
            }
            else if (gui.Input.IsKeyPressed(KeyboardKey.C))
            {
                gui.Input.SetClipboardText(state.Text);
            }
        }

        return state;
    }

    private static void DrawInputBackground(Gui gui, InputState state, Color? backgroundColor, Color? borderColor)
    {
        var finalBgColor = backgroundColor ?? Color.White;
        var finalBorderColor = borderColor ?? Color.Gray;
        var borderWidth = 1f;

        // Use focus-aware styling
        if (state.IsFocused)
        {
            finalBorderColor = Color.FromArgb(255, 100, 149, 237); // Focus blue
            borderWidth = 2f;
        }

        gui.DrawBackgroundRect(finalBgColor);
        gui.DrawRectBorder(gui.CurrentNode.Rect, finalBorderColor, borderWidth);
    }

    private static void DrawInputText(Gui gui, string displayText, string placeholder, float fontSize,
        Color? textColor, Color? placeholderColor)
    {
        var finalDisplayText = string.IsNullOrEmpty(displayText) ? placeholder : displayText;
        var finalColor = string.IsNullOrEmpty(displayText)
            ? placeholderColor ?? Color.Gray
            : textColor ?? Color.Black;

        if (!string.IsNullOrEmpty(finalDisplayText))
            gui.DrawText(finalDisplayText, fontSize, finalColor, centerInRect: false);
    }

    private static void DrawCursor(Gui gui, InputState state, string text, float fontSize, Color cursorColor)
    {
        if (!state.IsFocused || !state.ShowCursor || gui.Pass != Pass.Pass2Render) return;

        var font = new SKFont { Size = fontSize };
        var textBeforeCursor = text.Substring(0, Math.Min(state.CursorPosition, text.Length));
        var textWidth = MeasureTextWidth(font, textBeforeCursor);

        var innerRect = gui.CurrentNode.InnerRect;
        var cursorX = innerRect.X + textWidth;
        var cursorY1 = innerRect.Y + 2;
        var cursorY2 = innerRect.Y + innerRect.H - 2;

        gui.DrawLine(new Vector2(cursorX, cursorY1), new Vector2(cursorX, cursorY2), cursorColor, 2);
    }

    private static void DrawCursorMultiline(Gui gui, InputState state, string text, float fontSize, Color? cursorColor)
    {
        if (!state.IsFocused || !state.ShowCursor || gui.Pass != Pass.Pass2Render) return;

        var font = new SKFont { Size = fontSize };
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
        float padding = 8, bool enabled = true, string id = "")
    {
        var nodeId = string.IsNullOrEmpty(id) ? gui.NodeId("TextInput", 0) : id;

        var cursorColorFinal = cursorColor ?? textColor ?? gui.CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value;
        using (gui.Node(width, height).Padding(padding).Enter())
        {
            var state = GetOrCreateState(nodeId, text);
            var interactable = gui.GetInteractable();

            state = HandleFocusAndClick(state, interactable, gui, GetCursorPositionFromClick, state.Text, fontSize);
            state = HandleKeyboardInput(state, gui);

            // Rendering
            DrawInputBackground(gui, state, backgroundColor, borderColor);
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
    /// <returns>The updated value of the text in the input field.</returns>
    public static string TextInput(this Gui gui, string text,
        float width = 200, float height = 32, string placeholder = "",
        Color? backgroundColor = null, Color? borderColor = null, Color? textColor = null,
        Color? placeholderColor = null, Color? cursorColor = null, float fontSize = 14,
        float padding = 8, bool enabled = true, string id = "")
    {
        gui.TextInput(ref text, width, height, placeholder, backgroundColor, borderColor,
            textColor, placeholderColor, cursorColor, fontSize, padding, enabled, id);
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
        using (gui.Node(width, height).Padding(padding).Enter())
        {
            var state = GetOrCreateState(nodeId, text);
            var interactable = gui.GetInteractable();

            var stateTemp = state;
            state = HandleFocusAndClick(state, interactable, gui,
                (mousePos, rect, _, textFontSize) => GetCursorPositionFromClick(mousePos, rect,
                    new string(maskChar, stateTemp.Text.Length), textFontSize),
                state.Text, fontSize);
            state = HandleKeyboardInput(state, gui);

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

        using (gui.Node(width, height).Padding(padding).Enter())
        {
            var state = GetOrCreateState(nodeId, text);
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
        InputStates.Clear();
    }
}
