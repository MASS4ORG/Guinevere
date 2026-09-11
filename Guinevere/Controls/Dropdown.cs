using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    private class DropdownState
    {
        public bool IsOpen { get; set; }
        public int SelectedIndex { get; set; } = -1;
        public int HoveredIndex { get; set; } = -1;

        /// <summary>The button's rect as last measured, which is where the list anchors.</summary>
        public Rect ButtonRect { get; set; } = new(0, 0, 0, 0);

        /// <summary>
        /// The anchor both passes of the current frame use. A node's rect only resolves in the render
        /// pass, so reading it directly would place the list differently in each pass.
        /// </summary>
        public Rect Anchor { get; set; } = new(0, 0, 0, 0);
    }

    /// <summary>
    /// A dropdown that opens a list of options over the rest of the frame.
    /// </summary>
    /// <param name="gui">The GUI instance.</param>
    /// <param name="options">The choices.</param>
    /// <param name="selectedIndex">The chosen index, updated on selection. Negative shows the placeholder.</param>
    /// <param name="width">Button width. Zero fills the parent.</param>
    /// <param name="height">Button height.</param>
    /// <param name="placeholder">Shown when nothing is selected.</param>
    /// <param name="backgroundColor">Button fill. Defaults to the palette's surface.</param>
    /// <param name="borderColor">Button outline. Defaults to the palette's border.</param>
    /// <param name="textColor">Label colour. Defaults to the palette's text.</param>
    /// <param name="placeholderColor">Placeholder colour. Defaults to the palette's dim text.</param>
    /// <param name="dropdownColor">List fill. Defaults to the palette's popup.</param>
    /// <param name="hoverColor">Fill of the option under the pointer.</param>
    /// <param name="selectedColor">Fill of the chosen option.</param>
    /// <param name="fontSize">Label size.</param>
    /// <param name="padding">Horizontal padding inside the button and the options.</param>
    /// <param name="borderRadius">Corner radius.</param>
    /// <param name="maxVisibleItems">How many options the list shows before it scrolls.</param>
    /// <param name="filePath">Call site, supplied by the compiler. Pass an id to separate two dropdowns sharing one.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
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
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(options);

        var id = gui.NodeId(filePath, lineNumber);
        var state = DropdownStateFor(gui, id);
        var palette = gui.Controls;

        if (gui.Pass == Pass.Pass1Build) state.Anchor = state.ButtonRect;

        DrawButton(gui, id, options, selectedIndex, width, height, placeholder,
            backgroundColor ?? palette.Surface, borderColor ?? palette.Border,
            textColor ?? palette.Text, placeholderColor ?? palette.TextDim,
            fontSize, padding, borderRadius, state);

        if (!state.IsOpen || state.Anchor.W <= 0) return;

        DrawList(gui, id, options, ref selectedIndex, state.Anchor, height,
            dropdownColor ?? palette.Popup, borderColor ?? palette.Border,
            textColor ?? palette.Text, hoverColor ?? palette.SurfaceHover,
            selectedColor ?? palette.Selected, fontSize, padding, borderRadius, maxVisibleItems, state);
    }

    /// <summary>
    /// A dropdown that returns the chosen index instead of taking it by reference.
    /// </summary>
    /// <param name="gui">The GUI instance.</param>
    /// <param name="options">The choices.</param>
    /// <param name="selectedIndex">The currently chosen index.</param>
    /// <param name="width">Button width. Zero fills the parent.</param>
    /// <param name="height">Button height.</param>
    /// <param name="placeholder">Shown when nothing is selected.</param>
    /// <param name="backgroundColor">Button fill.</param>
    /// <param name="borderColor">Button outline.</param>
    /// <param name="textColor">Label colour.</param>
    /// <param name="placeholderColor">Placeholder colour.</param>
    /// <param name="dropdownColor">List fill.</param>
    /// <param name="hoverColor">Fill of the option under the pointer.</param>
    /// <param name="selectedColor">Fill of the chosen option.</param>
    /// <param name="fontSize">Label size.</param>
    /// <param name="padding">Horizontal padding.</param>
    /// <param name="borderRadius">Corner radius.</param>
    /// <param name="maxVisibleItems">How many options the list shows before it scrolls.</param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    /// <returns>The chosen index after this frame.</returns>
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
        var index = selectedIndex;
        Dropdown(gui, options, ref index, width, height, placeholder, backgroundColor, borderColor,
            textColor, placeholderColor, dropdownColor, hoverColor, selectedColor, fontSize, padding,
            borderRadius, maxVisibleItems, filePath, lineNumber);
        return index;
    }

    /// <summary>Closes every open dropdown and forgets their state.</summary>
    /// <param name="gui">The GUI instance.</param>
    public static void ClearDropdownStates(this Gui gui)
    {
        ArgumentNullException.ThrowIfNull(gui);
        gui.ClearControlStates<DropdownState>();
    }

    private static DropdownState DropdownStateFor(Gui gui, string id) =>
        gui.ControlState(id, () => new DropdownState());

    private static void DrawButton(Gui gui, string id, string[] options, int selectedIndex,
        float width, float height, string placeholder, Color background, Color border, Color text,
        Color placeholderText, float fontSize, float padding, float borderRadius, DropdownState state)
    {
        using (gui.Node(width, height, $"{id}/button").Direction(Axis.Horizontal)
                   .Padding(padding, 0).ContentAlignY(0.5f).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                var rect = gui.CurrentNode.Rect;
                state.ButtonRect = rect;
                if (state.Anchor.W <= 0) state.Anchor = rect;

                var interactable = gui.GetInteractable();

                gui.DrawBackgroundRect(background, borderRadius);
                gui.DrawRectBorder(rect, state.IsOpen ? gui.Controls.Accent : border,
                    state.IsOpen ? 2f : 1f, borderRadius);

                if (interactable.OnClick()) state.IsOpen = !state.IsOpen;

                DrawArrow(gui, rect, padding, text);
            }

            var label = selectedIndex >= 0 && selectedIndex < options.Length ? options[selectedIndex] : placeholder;
            gui.DrawText(label, fontSize, selectedIndex >= 0 ? text : placeholderText, centerInRect: false);
        }
    }

    private static void DrawArrow(Gui gui, Rect rect, float padding, Color color)
    {
        const float size = 4f;
        var x = rect.X + rect.W - padding - size;
        var y = rect.Y + (rect.H / 2f);

        gui.DrawTriangleFilled(
            new Vector2(x - size, y - (size / 2f)),
            new Vector2(x + size, y - (size / 2f)),
            new Vector2(x, y + (size / 2f)), color);
    }

    /// <summary>
    /// The option list, positioned over the frame rather than inside the flow, so it is not clipped by
    /// whatever panel the dropdown sits in.
    /// </summary>
    private static void DrawList(Gui gui, string id, string[] options, ref int selectedIndex,
        Rect buttonRect, float rowHeight, Color background, Color border, Color text, Color hover,
        Color selected, float fontSize, float padding, float borderRadius, int maxVisibleItems,
        DropdownState state)
    {
        var visible = Math.Min(options.Length, Math.Max(1, maxVisibleItems));
        var listHeight = visible * rowHeight;
        var top = buttonRect.Y + buttonRect.H + 2;

        if (top + listHeight > gui.ScreenRect.H) top = Math.Max(0, buttonRect.Y - listHeight - 2);

        var chosen = -1;

        using (gui.Node(buttonRect.W, listHeight, $"{id}/list")
                   .AbsoluteScreen(buttonRect.X, top)
                   .BlockInput()
                   .Direction(Axis.Vertical)
                   .Enter())
        {
            gui.SetZIndex(ListZIndex);

            if (gui.Pass == Pass.Pass2Render)
            {
                gui.DrawBackgroundRect(background, borderRadius);
                gui.DrawRectBorder(gui.CurrentNode.Rect, border, 1f, borderRadius);
            }

            for (var i = 0; i < options.Length; i++)
            {
                using (gui.Node(-1, rowHeight, $"{id}/list/{i}").ExpandWidth()
                           .Padding(padding, 0).ContentAlignY(0.5f).Enter())
                {
                    if (gui.Pass == Pass.Pass2Render)
                    {
                        var interactable = gui.GetInteractable();

                        if (i == selectedIndex) gui.DrawBackgroundRect(selected, borderRadius);
                        else if (interactable.OnHover()) gui.DrawBackgroundRect(hover, borderRadius);

                        if (interactable.OnClick()) chosen = i;
                    }

                    gui.DrawText(options[i], fontSize, text, centerInRect: false);
                }
            }
        }

        if (gui.Pass != Pass.Pass2Render) return;

        if (chosen >= 0)
        {
            selectedIndex = chosen;
            state.SelectedIndex = chosen;
            state.IsOpen = false;
            return;
        }

        if (gui.Input.IsKeyPressed(KeyboardKey.Escape)) state.IsOpen = false;

        // A press that reached neither the button nor the list dismisses it. The list blocks input, so
        // a press inside it never gets here.
        if (gui.Input.IsMouseButtonPressed(MouseButton.Left)
            && !buttonRect.Contains(gui.Input.MousePosition)
            && !gui.IsPointerOverBlocker)
            state.IsOpen = false;
    }

    /// <summary>Where an open option list draws, above ordinary content but below a drag ghost.</summary>
    private const int ListZIndex = 5_000;

    private static bool IsMouseInRect(Vector2 mousePos, Rect rect) =>
        mousePos.X >= rect.X && mousePos.X <= rect.X + rect.W &&
        mousePos.Y >= rect.Y && mousePos.Y <= rect.Y + rect.H;
}
