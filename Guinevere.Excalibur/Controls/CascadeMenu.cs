using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// A menu that pops up at a point and cascades submenus, drawn by the same renderer the menu bar
    /// uses so a right-click menu and a dropdown are the same widget. Supports submenus, separators,
    /// check items, disabled items and a shortcut column; a click outside or Escape dismisses it.
    /// A menu opened during the render pass appears on the next frame, fully laid out.
    /// </summary>
    /// <param name="gui">The GUI for this frame.</param>
    /// <param name="isOpen">Whether the menu is showing. Set false when it dismisses or an item runs.</param>
    /// <param name="position">Screen position of the menu's top-left corner. Keep it stable across
    /// frames — sample the pointer when the menu opens, not while it is open.</param>
    /// <param name="build">Fills the menu.</param>
    /// <param name="backgroundColor">Menu fill. Defaults to the control palette's popup color.</param>
    /// <param name="textColor">Item text. Defaults to the palette's text color.</param>
    /// <param name="hoverColor">Row highlight. Defaults to the palette's hover color.</param>
    /// <param name="fontSize">Item text size.</param>
    /// <param name="padding">Horizontal padding inside a row.</param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    public static void CascadeMenu(this Gui gui, ref bool isOpen, Vector2 position,
        Action<FlyoutBuilder> build,
        Color? backgroundColor = null,
        Color? textColor = null,
        Color? hoverColor = null,
        float fontSize = 12,
        float padding = 12,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(build);

        var id = gui.NodeId(filePath, lineNumber);
        var state = gui.ControlState(id, () => new MenuBarState());

        if (gui.Pass == Pass.Pass1Build)
        {
            state.PrevSubmenuRects = state.SubmenuRects;
            state.SubmenuRects = [];
            state.BeginFrame();
        }

        state.OpenIndex = isOpen ? 0 : -1;

        if (state.FrameOpenIndex < 0)
        {
            isOpen = state.OpenIndex >= 0;
            return;
        }

        var builder = new FlyoutBuilder();
        build(builder);

        if (builder.Items.Count == 0)
        {
            isOpen = false;
            return;
        }

        if (gui.Pass == Pass.Pass2Render) HandleMenuKeyboard(gui, state, builder.Items);

        RenderMenuGroup(gui, state, id, builder.Items, position, depth: 0,
            backgroundColor, textColor, hoverColor, fontSize, padding, CascadeMenuZIndex);

        if (gui.Pass == Pass.Pass2Render
            && (gui.Input.IsMouseButtonPressed(MouseButton.Left)
                || gui.Input.IsMouseButtonPressed(MouseButton.Right))
            && !state.SubmenuRects.Any(rect => IsMouseInRect(gui.Input.MousePosition, rect)))
            ResetMenuState(state);

        isOpen = state.OpenIndex >= 0;
    }
}
