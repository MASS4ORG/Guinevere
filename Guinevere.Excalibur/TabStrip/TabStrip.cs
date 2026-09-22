using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    const float NavigationButtonWidth = 18f;

    /// <summary>
    /// A row of tabs with an active one, optional icons, unsaved markers and close affordances. The
    /// dock space draws its panel tabs with this, so a host's own tabs match without copying the look.
    /// </summary>
    /// <param name="gui">The GUI instance.</param>
    /// <param name="items">The tabs, in order.</param>
    /// <param name="activeId">The id of the active tab, or null.</param>
    /// <param name="theme">Colors and metrics. Defaults to <see cref="TabStripTheme.Default"/>.</param>
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

        theme ??= TabStripTheme.FromStyle(gui.ControlStyle);
        var result = TabStripResult.None;
        var state = gui.ControlState($"{idPrefix}/overflow", () => new TabStripState());
        var widths = items.Select(item => MeasureTab(item, theme)).ToArray();
        var overflowing = state.ViewportWidth > 0 && widths.Sum() > state.ViewportWidth;
        var availableWidth = Math.Max(0, state.ViewportWidth - (overflowing ? NavigationButtonWidth * 2 : 0));
        var activeIndex = items.ToList().FindIndex(item => item.Id == activeId);

        if (overflowing) EnsureActiveIsVisible(state, activeIndex, widths, availableWidth);
        else state.FirstVisible = 0;

        var visible = VisibleRange(state.FirstVisible, widths, availableWidth, overflowing);

        using (gui.Node(-1, theme.Height, idPrefix, filePath, lineNumber)
                   .ExpandWidth().Direction(Axis.Horizontal).Enter())
        {
            if (gui.Pass == Pass.Pass2Render) gui.DrawBackgroundRect(theme.Strip);

            if (overflowing && Navigation(gui, $"{idPrefix}/previous", "<", state.FirstVisible > 0, theme))
                state.FirstVisible = PreviousRange(state.FirstVisible, widths, availableWidth);

            for (var index = visible.Start; index < visible.End; index++)
            {
                var item = items[index];
                result = RenderTab(gui, item, item.Id == activeId, theme, $"{idPrefix}/{item.Id}",
                    onDragSource, result);
            }

            // Always built, callback or not, so the strip's structure does not change when a host
            // adds or drops one.
            using (gui.Node(-1, theme.Height, $"{idPrefix}/actions")
                       .ExpandWidth().Direction(Axis.Horizontal).ContentAlignX(1f).Enter())
                trailing?.Invoke(gui);

            if (overflowing && Navigation(gui, $"{idPrefix}/next", ">", visible.End < items.Count, theme))
                state.FirstVisible = visible.End;

            if (gui.Pass == Pass.Pass2Render) state.ViewportWidth = gui.CurrentNode.Rect.W;
        }

        return result;
    }

    static TabStripResult RenderTab(Gui gui, TabStripItem item, bool isActive, TabStripTheme theme,
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

    static TabStripResult RenderClose(Gui gui, TabStripItem item, bool isActive, TabStripTheme theme,
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

    static float MeasureTab(TabStripItem item, TabStripTheme theme)
    {
        var font = new SKFont { Size = theme.FontSize };
        font.MeasureText(item.Label, out var bounds);

        return bounds.Width + 18
                            + (item.Closable ? 18 : 0)
                            + (item.Icon is null ? 0 : theme.IconSize + 6);
    }

    static (int Start, int End) VisibleRange(int first, IReadOnlyList<float> widths, float available,
        bool overflowing)
    {
        if (!overflowing) return (0, widths.Count);

        first = Math.Clamp(first, 0, Math.Max(0, widths.Count - 1));
        var end = first;
        var used = 0f;
        while (end < widths.Count && (end == first || used + widths[end] <= available)) used += widths[end++];
        return (first, end);
    }

    static void EnsureActiveIsVisible(TabStripState state, int activeIndex, IReadOnlyList<float> widths,
        float available)
    {
        if (activeIndex < 0) return;

        var range = VisibleRange(state.FirstVisible, widths, available, overflowing: true);
        if (activeIndex < range.Start || activeIndex >= range.End) state.FirstVisible = activeIndex;
    }

    static int PreviousRange(int first, IReadOnlyList<float> widths, float available)
    {
        if (first == 0) return 0;

        var previous = first - 1;
        while (previous > 0 && VisibleRange(previous - 1, widths, available, overflowing: true).End >= first)
            previous--;
        return previous;
    }

    static bool Navigation(Gui gui, string id, string label, bool enabled, TabStripTheme theme)
    {
        using (gui.Node(NavigationButtonWidth, theme.Height, id).BlockInput().Enter())
        {
            if (gui.Pass != Pass.Pass2Render) return false;

            var interactable = gui.GetInteractable();
            if (enabled && interactable.OnHover()) gui.DrawBackgroundRect(theme.Hover);
            gui.DrawText(label, theme.FontSize, enabled ? theme.Ink : theme.InkDim, centerInRect: true);
            return enabled && interactable.OnClick();
        }
    }

    sealed class TabStripState
    {
        public int FirstVisible { get; set; }

        public float ViewportWidth { get; set; }
    }
}
