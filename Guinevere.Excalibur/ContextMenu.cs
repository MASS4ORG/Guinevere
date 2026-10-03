using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    sealed class ContextMenuState
    {
        public bool WasOpen;
        public Vector2 Position;
        public Vector2 FramePosition;
    }

    /// <summary>Creates a context menu anchored where it opened, with nested menus and keyboard navigation.</summary>
    public static void ContextMenu(this Gui gui, ref bool isOpen, Action<ContextMenuBuilder> buildMenu,
        Vector2? position = null,
        Color? backgroundColor = null,
        Color? borderColor = null,
        Color? hoverColor = null,
        float itemHeight = 24,
        float minWidth = 120,
        float borderRadius = ControlMetrics.CornerRadius,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(buildMenu);
        var id = gui.NodeId(filePath, lineNumber);
        var state = gui.ControlState(id + "/anchor", () => new ContextMenuState());
        if (isOpen && !state.WasOpen) state.Position = position ?? gui.Input.MousePosition;
        state.WasOpen = isOpen;
        if (gui.Pass == Pass.Pass1Build) state.FramePosition = position ?? state.Position;
        gui.Flyout(ref isOpen, state.FramePosition, menu =>
        {
            var builder = new ContextMenuBuilder();
            buildMenu(builder);
            menu.Items.AddRange(builder.Items);
        }, minWidth, itemHeight, backgroundColor, borderColor, hoverColor: hoverColor,
            padding: 8, borderRadius: borderRadius, filePath: filePath, lineNumber: lineNumber);
        state.WasOpen = isOpen;
    }
}
