using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// Creates a flyout menu at the specified position
    /// </summary>
    public static void Flyout(this Gui gui, ref bool isOpen, Vector2 position, Action<FlyoutBuilder> buildMenu,
        float minWidth = 150,
        float itemHeight = 32,
        float fontSize = ControlMetrics.CompactFontSize,
        float padding = ControlMetrics.Spacing,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(buildMenu);
        fontSize = gui.ControlStyle.CompactFontSizeOr(fontSize);
        padding = gui.ControlStyle.SpacingOr(padding);
        id ??= gui.NodeId(filePath, lineNumber);
        ExcaliburStyles.Ensure(gui);
        var state = gui.ControlState(id, () => new MenuBarState());
        state.Classes = classes;
        state.StyleId = id;
        state.PopupId = id;
        PreparePopupMenuFrame(gui, state, isOpen,
            new MenuAppearance(itemHeight, itemHeight, minWidth));
        if (state.FrameOpenIndex < 0) return;

        var builder = new FlyoutBuilder();
        buildMenu(builder);
        if (builder.Items.Count == 0) return;
        if (gui.Pass == Pass.Pass2Render) HandleMenuKeyboard(gui, state, builder.Items);
        using (var focusScope = gui.EnterFocusNavigationScope($"{id}/focus"))
        {
            focusScope.SetActive();
            RenderMenuGroup(gui, state, id, builder.Items, position, 0,
                fontSize, padding, CascadeMenuZIndex);
        }

        if (gui.Pass == Pass.Pass2Render)
        {
            DismissPopupMenuOutside(gui, state, rightClick: false);
            isOpen = state.OpenIndex >= 0;
        }
    }

    static float CalculateFlyoutWidth(List<FlyoutItem> items, float fontSize, float padding, float minWidth,
        bool hasCheckColumn = false)
    {
        using var font = new SKFont { Size = fontSize };
        var maxWidth = minWidth;
        var checkColumnWidth = hasCheckColumn ? 18f : 0f;

        foreach (var item in items.Where(i => !i.IsSeparator))
        {
            font.MeasureText(item.Text, out var textBounds);
            var itemWidth = textBounds.Width + padding * 2 + checkColumnWidth;

            if (!string.IsNullOrEmpty(item.Shortcut))
            {
                font.MeasureText(item.Shortcut, out var shortcutBounds);
                itemWidth += shortcutBounds.Width + padding;
            }

            if (item.HasSubmenu) itemWidth += 20; // Space for arrow

            maxWidth = Math.Max(maxWidth, itemWidth);
        }

        return maxWidth;
    }
}
