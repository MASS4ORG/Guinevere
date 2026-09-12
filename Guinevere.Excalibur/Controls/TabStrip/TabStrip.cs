using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// A row of tabs with an active one, optional icons, unsaved markers and close affordances. The
    /// dock space draws its panel tabs with this, so a host's own tabs match without copying the look.
    /// </summary>
    /// <param name="gui">The GUI instance.</param>
    /// <param name="items">The tabs, in order.</param>
    /// <param name="activeId">The id of the active tab, or null.</param>
    /// <param name="theme">Colours and metrics. Defaults to <see cref="TabStripTheme.Default"/>.</param>
    /// <param name="idPrefix">Id of the strip's node and prefix for its tabs; needed when a frame draws several strips.</param>
    /// <param name="trailing">Draws into the width the tabs leave over, flowing from the right edge.</param>
    /// <param name="onDragSource">Lets the caller start a drag from a tab; return true when it did.</param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    /// <returns>Which tab was activated, closed or dragged this frame.</returns>
    public static TabStripResult TabStrip(this Gui gui, IReadOnlyList<TabStripItem> items, string? activeId,
        TabStripTheme? theme = null, string idPrefix = "tabstrip",
        Action<Gui>? trailing = null,
        Func<TabStripItem, string, bool>? onDragSource = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(items);

        theme ??= TabStripTheme.Default;
        var result = TabStripResult.None;

        using (gui.Node(-1, theme.Height, idPrefix, filePath, lineNumber)
                   .ExpandWidth().Direction(Axis.Horizontal).Enter())
        {
            if (gui.Pass == Pass.Pass2Render) gui.DrawBackgroundRect(theme.Strip);

            foreach (var item in items)
                result = RenderTab(gui, item, item.Id == activeId, theme, $"{idPrefix}/{item.Id}",
                    onDragSource, result);

            // Always built, callback or not, so the strip's structure does not change when a host
            // adds or drops one.
            using (gui.Node(-1, theme.Height, $"{idPrefix}/actions")
                       .ExpandWidth().Direction(Axis.Horizontal).ContentAlignX(1f).Enter())
                trailing?.Invoke(gui);
        }

        return result;
    }

    private static TabStripResult RenderTab(Gui gui, TabStripItem item, bool isActive, TabStripTheme theme,
        string id, Func<TabStripItem, string, bool>? onDragSource, TabStripResult result)
    {
        var width = MeasureTab(item, theme);

        using (gui.Node(width, theme.Height, id).Direction(Axis.Horizontal).Padding(8, 0).Gap(6f)
                   .ContentAlignY(0.5f).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                var interactable = gui.GetInteractable();

                gui.DrawBackgroundRect(isActive ? theme.Active : interactable.OnHover() ? theme.Hover : theme.Tab);

                if (isActive)
                {
                    var rect = gui.CurrentNode.Rect;
                    gui.DrawRect(new Rect(rect.X, rect.Y, rect.W, 2), theme.Accent);
                }

                if (interactable.OnClick()) result = result with { Activated = item };

                // Middle-click closes, as every tabbed editor does; the close button is the
                // discoverable half of the same gesture.
                if (item.Closable && interactable.OnClick(MouseButton.Middle))
                    result = result with { Closed = item };

                if (onDragSource?.Invoke(item, id) == true) result = result with { Dragged = item };
            }

            if (item.Icon is { } icon)
                using (gui.Node(theme.IconSize, theme.IconSize, $"{id}/icon").Enter())
                    icon(gui);

            gui.DrawText(item.Label, theme.FontSize, isActive ? theme.Ink : theme.InkDim, centerInRect: false);

            if (item.Closable) result = RenderClose(gui, item, isActive, theme, id, result);
        }

        return result;
    }

    private static TabStripResult RenderClose(Gui gui, TabStripItem item, bool isActive, TabStripTheme theme,
        string id, TabStripResult result)
    {
        // Blocks the tab underneath, so closing never also activates.
        using (gui.Node(12, theme.Height, $"{id}/close").BlockInput().Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                var close = gui.GetInteractable();
                if (close.OnHover()) gui.DrawBackgroundRect(theme.Hover, 2);
                if (close.OnClick()) result = result with { Closed = item };

                if (item.Modified)
                {
                    var rect = gui.CurrentNode.Rect;
                    gui.DrawCircleFilled(
                        new Vector2(rect.X + (rect.W / 2f), rect.Y + (rect.H / 2f)), 3.5f, theme.Accent);
                }
            }

            // Always built: a node that exists in only one pass never gets a rect.
            gui.DrawText(item.Modified ? " " : "×", theme.FontSize, isActive ? theme.Ink : theme.InkDim);
        }

        return result;
    }

    private static float MeasureTab(TabStripItem item, TabStripTheme theme)
    {
        var font = new SKFont { Size = theme.FontSize };
        font.MeasureText(item.Label, out var bounds);

        return bounds.Width + 18
                            + (item.Closable ? 18 : 0)
                            + (item.Icon is null ? 0 : theme.IconSize + 6);
    }
}
