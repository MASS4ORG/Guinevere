using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    private static readonly Dictionary<string, DropdownState> DropdownStates = new();

    private class DropdownState
    {
        public bool IsOpen { get; set; }
        public int SelectedIndex { get; set; } = -1;
        public int HoveredIndex { get; set; } = -1;
    }

    /// <summary>
    /// Creates a dropdown/combobox that allows selection from a list of options with internal state management
    /// </summary>
    public static void Dropdown(this Gui gui, string[] options, ref int selectedIndex,
        float width = 200,
        float height = 32,
        string placeholder = "Select an option...",
        Color? backgroundColor = null,
        Color? borderColor = null,
        Color? textColor = null,
        Color? placeholderColor = null,
        Color? dropdownColor = null,
        Color? hoverColor = null,
        Color? selectedColor = null,
        float fontSize = 14,
        float padding = 8,
        float borderRadius = 4,
        int maxVisibleItems = 6,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        DropdownCore(gui, options, ref selectedIndex, width, height, placeholder, backgroundColor,
            borderColor, textColor, placeholderColor, dropdownColor, hoverColor, selectedColor,
            fontSize, padding, borderRadius, maxVisibleItems, filePath, lineNumber);
    }

    /// <summary>
    /// Creates a dropdown that returns the selected index without modifying the input
    /// </summary>
    public static int Dropdown(this Gui gui, string[] options, int selectedIndex = -1,
        float width = 200,
        float height = 32,
        string placeholder = "Select an option...",
        Color? backgroundColor = null,
        Color? borderColor = null,
        Color? textColor = null,
        Color? placeholderColor = null,
        Color? dropdownColor = null,
        Color? hoverColor = null,
        Color? selectedColor = null,
        float fontSize = 14,
        float padding = 8,
        float borderRadius = 4,
        int maxVisibleItems = 6,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var temp = selectedIndex;
        DropdownCore(gui, options, ref temp, width, height, placeholder, backgroundColor,
            borderColor, textColor, placeholderColor, dropdownColor, hoverColor, selectedColor,
            fontSize, padding, borderRadius, maxVisibleItems, filePath, lineNumber);
        return temp;
    }

    private static void DropdownCore(Gui gui, string[] options, ref int selectedIndex,
        float width, float height, string placeholder, Color? backgroundColor,
        Color? borderColor, Color? textColor, Color? placeholderColor, Color? dropdownColor,
        Color? hoverColor, Color? selectedColor, float fontSize, float padding, float borderRadius,
        int maxVisibleItems, string filePath, int lineNumber)
    {
        var id = gui.NodeId(filePath, lineNumber);
        var state = GetOrCreateDropdownState(id, selectedIndex);

        var visibleItems = Math.Min(maxVisibleItems, options.Length);
        var totalHeight = height + (state.IsOpen && options.Length > 0 ? visibleItems * height + 4 : 0);

        using (gui.Node(width, totalHeight, filePath: filePath, lineNumber: lineNumber).Enter())
        {
            // Main dropdown button
            RenderDropdownButton(gui, state, options, selectedIndex, placeholder, width, height,
                backgroundColor, borderColor, textColor, placeholderColor, fontSize, padding, borderRadius);

            // Always create dropdown list node for consistency
            if (options.Length > 0)
                RenderDropdownList(gui, state, options, width, height, visibleItems,
                    dropdownColor, borderColor, hoverColor, selectedColor, textColor,
                    fontSize, padding, borderRadius);

            selectedIndex = state.SelectedIndex;
        }
    }

    private static DropdownState GetOrCreateDropdownState(string id, int initialIndex)
    {
        return DropdownStates.TryGetValue(id, out var state)
            ? state
            : DropdownStates[id] = new DropdownState { SelectedIndex = initialIndex };
    }

    private static void RenderDropdownButton(Gui gui, DropdownState state, string[] options,
        int selectedIndex, string placeholder, float width, float height,
        Color? backgroundColor, Color? borderColor, Color? textColor, Color? placeholderColor,
        float fontSize, float padding, float borderRadius)
    {
        using (gui.Node(width, height).Padding(padding).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                // Register as focusable for keyboard navigation
                gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
                var buttonInteractable = gui.GetInteractable();
                var isHovered = buttonInteractable.OnHover();
                var isClicked = buttonInteractable.OnClick();
                var rect = gui.CurrentNode.Rect;

                // Draw focus indicator if focused
                var hasFocus = gui.HasFocus();
                if (hasFocus)
                {
                    var focusRect = new Rect(rect.X - 2, rect.Y - 2, rect.W + 4, rect.H + 4);
                    gui.DrawRectBorder(focusRect, Color.FromArgb(255, 100, 149, 237), 2f, borderRadius + 2);
                }

                // If dropdown is open and focus is lost, close it
                if (state.IsOpen && !hasFocus && !gui.Input.IsAnyKeyDown)
                {
                    state.IsOpen = false;
                }

                // Handle button click or keyboard activation
                var activated = isClicked;
                if (hasFocus && (gui.Input.IsKeyPressed(KeyboardKey.Space) || gui.Input.IsKeyPressed(KeyboardKey.Enter)))
                {
                    activated = true;
                }
                if (activated) {
                    state.IsOpen = !state.IsOpen;
                    if (state.IsOpen) state.HoveredIndex = selectedIndex >= 0 ? selectedIndex : 0;
                }

                var bgColor = backgroundColor ?? (isHovered ? Color.FromArgb(255, 248, 248, 248) : Color.White);
                var borderColorFinal = borderColor ??
                                       (isHovered
                                           ? Color.FromArgb(255, 170, 170, 170)
                                           : Color.FromArgb(255, 200, 200, 200));

                gui.DrawBackgroundRect(bgColor, borderRadius);
                gui.DrawRectBorder(rect, borderColorFinal, 1f, borderRadius);

                // Draw dropdown arrow
                var arrowX = rect.X + rect.W - padding - 8;
                var arrowY = rect.Y + rect.H * 0.5f;
                var arrowSize = 4f;
                var arrowColor = textColor ?? Color.Black;
                var arrowTop = new Vector2(arrowX - arrowSize, arrowY - arrowSize * 0.5f);
                var arrowBottom = new Vector2(arrowX + arrowSize, arrowY - arrowSize * 0.5f);
                var arrowPoint = new Vector2(arrowX, arrowY + arrowSize * 0.5f);

                gui.DrawTriangle(arrowTop, arrowBottom, arrowPoint, arrowColor, arrowColor, arrowColor);
            }

            var displayText = selectedIndex >= 0 && selectedIndex < options.Length
                ? options[selectedIndex]
                : placeholder;
            var displayColor = selectedIndex >= 0 ? textColor ?? Color.Black : placeholderColor ?? Color.Gray;

            gui.DrawText(displayText, fontSize, displayColor, centerInRect: false);
        }
    }

    private static void RenderDropdownList(Gui gui, DropdownState state, string[] options,
        float width, float height, int visibleItems, Color? dropdownColor, Color? borderColor,
        Color? hoverColor, Color? selectedColor, Color? textColor, float fontSize, float padding,
        float borderRadius)
    {
        var dropdownHeight = visibleItems * height;

        using (gui.Node(width, dropdownHeight).Top(height + 2).Padding(0).Enter())
        {
            if (gui.Pass == Pass.Pass2Render && state.IsOpen)
            {
                var rect = gui.CurrentNode.Rect;
                var dropdownBgColor = dropdownColor ?? Color.White;
                var borderColorFinal = borderColor ?? Color.FromArgb(255, 200, 200, 200);

                gui.DrawBackgroundRect(dropdownBgColor, borderRadius);
                gui.DrawRectBorder(rect, borderColorFinal, 1f, borderRadius);

                // Handle mouse interaction with dropdown items
                var mousePos = gui.Input.MousePosition;
                state.HoveredIndex = -1;

                if (IsMouseInRect(mousePos, rect))
                {
                    var relativeY = mousePos.Y - rect.Y;
                    state.HoveredIndex = Math.Max(0, Math.Min((int)(relativeY / height), options.Length - 1));

                    if (gui.Input.IsMouseButtonPressed(MouseButton.Left))
                    {
                        state.SelectedIndex = state.HoveredIndex;
                        state.IsOpen = false;
                    }
                }

                // --- Keyboard navigation for dropdown list ---
                if (gui.Input.IsKeyPressed(KeyboardKey.Escape))
                {
                    state.IsOpen = false;
                }
                else if (gui.Input.IsKeyPressed(KeyboardKey.Down))
                {
                    state.HoveredIndex = Math.Min((state.HoveredIndex < 0 ? 0 : state.HoveredIndex + 1), options.Length - 1);
                }
                else if (gui.Input.IsKeyPressed(KeyboardKey.Up))
                {
                    state.HoveredIndex = Math.Max((state.HoveredIndex < 0 ? 0 : state.HoveredIndex - 1), 0);
                }
                else if ((gui.Input.IsKeyPressed(KeyboardKey.Enter) || gui.Input.IsKeyPressed(KeyboardKey.Space)) && state.HoveredIndex >= 0)
                {
                    state.SelectedIndex = state.HoveredIndex;
                    state.IsOpen = false;
                }
                // Tab/Shift+Tab: close and move focus out
                else if (gui.Input.IsKeyPressed(KeyboardKey.Tab))
                {
                    state.IsOpen = false;
                }
            }

            // Always create item nodes for consistency, but only render when open
            for (var i = 0; i < visibleItems; i++)
                using (gui.Node(width, height).Padding(padding).Enter())
                {
                    if (gui.Pass == Pass.Pass2Render && state.IsOpen)
                    {
                        if (i == state.HoveredIndex)
                        {
                            var hoverColorFinal = hoverColor ?? Color.FromArgb(255, 230, 230, 230);
                            gui.DrawBackgroundRect(hoverColorFinal);
                        }

                        if (i == state.SelectedIndex)
                        {
                            var selectedColorFinal = selectedColor ?? Color.FromArgb(255, 100, 149, 237);
                            gui.DrawBackgroundRect(selectedColorFinal);
                        }

                        var itemTextColor = i == state.SelectedIndex ? Color.White : textColor ?? Color.Black;
                        gui.DrawText(options[i], fontSize, itemTextColor, centerInRect: false);
                    }
                    else if (gui.Pass != Pass.Pass2Render)
                    {
                        // Always create text nodes during build pass for consistency
                        gui.DrawText(options[i], fontSize, Color.Transparent, centerInRect: false);
                    }
                }

            // Handle click outside to close
            if (gui.Pass == Pass.Pass2Render && state.IsOpen && gui.Input.IsMouseButtonPressed(MouseButton.Left))
            {
                var mousePos = gui.Input.MousePosition;
                var mainRect = gui.CurrentNode.Parent?.Rect ?? new Rect();
                if (!IsMouseInRect(mousePos, mainRect)) state.IsOpen = false;
            }
        }
    }


    private static bool IsMouseInRect(Vector2 mousePos, Rect rect)
    {
        return mousePos.X >= rect.X && mousePos.X <= rect.X + rect.W &&
               mousePos.Y >= rect.Y && mousePos.Y <= rect.Y + rect.H;
    }

    /// <summary>
    /// Creates a searchable dropdown/combobox (simplified version)
    /// </summary>
    public static int SearchableDropdown(this Gui gui, string[] options, int selectedIndex = -1,
        float width = 200,
        float height = 32,
        string placeholder = "Search and select...",
        Color? backgroundColor = null,
        Color? borderColor = null,
        Color? textColor = null,
        Color? placeholderColor = null,
        Color? dropdownColor = null,
        Color? hoverColor = null,
        Color? selectedColor = null,
        float fontSize = 14,
        float padding = 8,
        float borderRadius = 4,
        int maxVisibleItems = 6,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        return gui.Dropdown(options, selectedIndex, width, height, placeholder, backgroundColor,
            borderColor, textColor, placeholderColor, dropdownColor, hoverColor, selectedColor,
            fontSize, padding, borderRadius, maxVisibleItems, filePath, lineNumber);
    }

    /// <summary>
    /// Clears all dropdown states (useful for cleanup)
    /// </summary>
    public static void ClearDropdownStates(this Gui gui)
    {
        DropdownStates.Clear();
    }
}
