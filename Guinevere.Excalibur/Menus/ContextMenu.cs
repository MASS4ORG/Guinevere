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

    /// <summary>
    /// Creates a context menu anchored where it opened, with nested menus and keyboard navigation. Rows are the compact
    /// control height and the menu at least 60% of a field wide.
    /// </summary>
    public static void ContextMenu(this Gui gui, ref bool isOpen, Action<ContextMenuBuilder> buildMenu,
        Vector2? position = null,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(buildMenu);
        var explicitId = id;
        id ??= gui.AutomaticId(filePath, lineNumber);
        ExcaliburStyles.Ensure(gui);
        var state = gui.ControlState(id + "/anchor", () => new ContextMenuState());
        if (isOpen && !state.WasOpen)
        {
            state.Position = position ?? gui.LocalMousePosition;
        }

        state.WasOpen = isOpen;
        if (gui.Pass == Pass.Pass1Build)
        {
            state.FramePosition = position ?? state.Position;
        }

        var itemHeight = gui.ControlStyle.CompactHeight;
        PopupFlyout(gui, ref isOpen, state.FramePosition, menu =>
        {
            var builder = new ContextMenuBuilder();
            buildMenu(builder);
            menu.Items.AddRange(builder.Items);
        }, new MenuAppearance(itemHeight, itemHeight, gui.ControlStyle.FieldWidth * 0.6f),
            gui.ControlStyle.Spacing, classes, explicitId, filePath, lineNumber);
        state.WasOpen = isOpen;
    }
}
