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
    /// frames — example the pointer when the menu opens, not while it is open.</param>
    /// <param name="build">Fills the menu.</param>
    /// <param name="fontSize">Item text size.</param>
    /// <param name="padding">Horizontal padding inside a row.</param>
    /// <param name="classes">Stylesheet classes for this control.</param>
    /// <param name="id">Stable control and stylesheet identity; when given, also the top menu's node id.</param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    public static void CascadeMenu(this Gui gui, ref bool isOpen, Vector2 position,
        Action<FlyoutBuilder> build,
        float fontSize = ControlMetrics.CompactFontSize,
        float padding = ControlMetrics.ComfortableSpacing,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        fontSize = gui.ControlStyle.CompactFontSizeOr(fontSize);
        padding = gui.ControlStyle.ComfortableSpacingOr(padding);

        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(build);

        var explicitId = id;
        id ??= gui.NodeId(filePath, lineNumber);
        ExcaliburStyles.Ensure(gui);
        var state = gui.ControlState(id, () => new MenuBarState());
        state.Classes = classes;
        state.StyleId = id;
        state.PopupId = explicitId;

        state.OpenIndex = isOpen ? 0 : -1;
        PreparePopupMenuFrame(gui, state, isOpen);

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

        using (var focusScope = gui.EnterFocusNavigationScope($"{id}/focus"))
        {
            focusScope.SetActive();
            RenderMenuGroup(gui, state, id, builder.Items, position, depth: 0,
                fontSize, padding, CascadeMenuZIndex);
        }

        if (gui.Pass == Pass.Pass2Render) DismissPopupMenuOutside(gui, state, rightClick: true);

        isOpen = state.OpenIndex >= 0;
    }
}
