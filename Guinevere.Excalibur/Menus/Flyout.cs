using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// Creates a flyout menu at the specified position, with rows the height of a field and at least three quarters of
    /// a field wide. Text is the compact control size.
    /// </summary>
    /// <param name="gui">The GUI for this frame.</param>
    /// <param name="isOpen">Whether the menu is showing. Set false when it dismisses or an item runs.</param>
    /// <param name="position">Screen position of the menu's top-left corner.</param>
    /// <param name="buildMenu">Fills the menu.</param>
    /// <param name="classes">Stylesheet classes for the menu.</param>
    /// <param name="id">Optional stable control and stylesheet identity; also the top menu's node id when given.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    public static void Flyout(this Gui gui, ref bool isOpen, Vector2 position, Action<FlyoutBuilder> buildMenu,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        var height = gui.ControlStyle.FieldHeight;
        PopupFlyout(gui, ref isOpen, position, buildMenu,
            new MenuAppearance(height, height, gui.ControlStyle.FieldWidth * 0.75f),
            gui.ControlStyle.Spacing, classes, id, filePath, lineNumber);
    }

    /// <summary>The flyout every popup menu shares, sized by <paramref name="appearance"/> and <paramref name="padding"/>.</summary>
    static void PopupFlyout(Gui gui, ref bool isOpen, Vector2 position, Action<FlyoutBuilder> buildMenu,
        MenuAppearance appearance, float padding, IReadOnlyList<string>? classes, string? id, string filePath,
        int lineNumber)
    {
        ArgumentNullException.ThrowIfNull(buildMenu);
        var fontSize = gui.ControlStyle.CompactFontSize;
        var explicitId = id;
        id ??= gui.AutomaticId(filePath, lineNumber);
        ExcaliburStyles.Ensure(gui);
        var state = gui.ControlState(id, static () => new MenuBarState());
        state.Classes = classes;
        state.StyleId = id;
        state.PopupId = explicitId;
        PreparePopupMenuFrame(gui, state, id, isOpen, appearance);
        if (state.FrameOpenIndex < 0)
        {
            return;
        }

        var builder = new FlyoutBuilder();
        buildMenu(builder);
        if (builder.Items.Count == 0)
        {
            return;
        }

        if (gui.Pass == Pass.Pass2Render)
        {
            HandleMenuKeyboard(gui, state, builder.Items);
        }

        using (var focusScope = gui.EnterFocusNavigationScope($"{id}/focus"))
        {
            focusScope.SetActiveWithoutAutoFocus();
            RenderMenuGroup(gui, state, id, builder.Items, position, 0,
                fontSize, padding, CascadeMenuZIndex);
        }

        if (gui.Pass == Pass.Pass2Render)
        {
            DismissPopupMenuOutside(gui, state, rightClick: false);
            isOpen = state.OpenIndex >= 0;
        }
    }

    /// <summary>
    /// The width a menu needs for its rows, estimated before layout because a popup is placed and kept on screen from it:
    /// each row's content, check column, shortcut and arrow, at least <paramref name="minWidth"/>.
    /// </summary>
    static float CalculateFlyoutWidth(List<FlyoutItem> items, float fontSize, float padding, float minWidth,
        bool hasCheckColumn = false)
    {
        using var font = new SKFont { Size = fontSize };
        var maxWidth = minWidth;
        var checkColumnWidth = hasCheckColumn ? 18f : 0f;

        foreach (var item in items.Where(static i => !i.IsSeparator))
        {
            var itemWidth = EstimateContentWidth(item.Content, font) + padding * 2 + checkColumnWidth;

            if (!string.IsNullOrEmpty(item.Shortcut))
            {
                font.MeasureText(item.Shortcut, out var shortcutBounds);
                itemWidth += shortcutBounds.Width + padding;
            }

            if (item.HasSubmenu)
            {
                itemWidth += 20; // Space for arrow
            }

            maxWidth = Math.Max(maxWidth, itemWidth);
        }

        return maxWidth;
    }

    /// <summary>
    /// A content's width as text is measured: its label, an icon as wide as the text is tall, a row as the sum of its
    /// parts and their gaps, and widgets without a label a few characters wide.
    /// </summary>
    static float EstimateContentWidth(Content content, SKFont font)
    {
        switch (content.Kind)
        {
            case ContentKind.Empty:
                return 0f;
            case ContentKind.Icon:
                return font.Size;
            case ContentKind.Row:
                var width = 0f;
                var parts = content.Parts;
                for (var i = 0; i < parts.Length; i++)
                {
                    width += EstimateContentWidth(parts[i], font) + (i > 0 ? font.Size / 3f : 0f);
                }

                return width;
            default:
                if (content.Label is not { Length: > 0 } label)
                {
                    return font.Size * 4f;
                }

                font.MeasureText(label, out var bounds);
                return bounds.Width;
        }
    }
}
