using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    const float NavigationButtonWidth = 18f;

    /// <summary>
    /// A row of tabs with an active one, optional icons, unsaved markers and close affordances. The
    /// dock space draws its panel tabs with this, so a host's own tabs match without copying the look. Styled by the
    /// <c>tabstrip</c> rules, <c>tab</c> (<c>:selected</c> for the active one, with its drawn <c>marker</c> part),
    /// <c>tab-close</c> and <c>tab-nav</c>.
    /// </summary>
    /// <param name="gui">The GUI instance.</param>
    /// <param name="items">The tabs, in order.</param>
    /// <param name="activeId">The id of the active tab, or null.</param>
    /// <param name="theme">Metrics. Defaults to <see cref="TabStripTheme.Default"/>.</param>
    /// <param name="idPrefix">Id of the strip's node and prefix for its tabs; needed when a frame draws several strips.</param>
    /// <param name="trailing">Draws into the width the tabs leave over, flowing from the right edge.</param>
    /// <param name="onDragSource">Lets the caller start a drag from a tab; return true when it did.</param>
    /// <param name="classes">Stylesheet classes for the strip.</param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    /// <returns>Which tab was activated, closed or dragged this frame.</returns>
    public static TabStripResult TabStrip(this Gui gui, IReadOnlyList<TabStripItem> items, string? activeId,
        TabStripTheme? theme = null, string idPrefix = "tabstrip",
        Action<Gui>? trailing = null,
        Func<TabStripItem, string, bool>? onDragSource = null,
        IReadOnlyList<string>? classes = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(items);

        theme ??= TabStripTheme.Default;
        ExcaliburStyles.Ensure(gui);
        var result = TabStripResult.None;
        var state = gui.ControlState($"{idPrefix}/overflow", () => new TabStripState());
        using (gui.StyledNode("tabstrip", classes, id: idPrefix, filePath: filePath, lineNumber: lineNumber)
                   .Height(theme.Height).ExpandWidth().Direction(Axis.Horizontal).Enter())
        {
            var (widths, availableWidth, overflowing) = PrepareTabWidths(gui, items, activeId, theme, state, idPrefix);
            var visible = VisibleRange(state.FirstVisible, widths, availableWidth, overflowing);
            PreviousTabPage(gui, idPrefix, theme, state, widths, availableWidth, overflowing);

            for (var index = visible.Start; index < visible.End; index++)
            {
                var item = items[index];
                result = RenderTab(gui, item, item.Id == activeId, theme, $"{idPrefix}/{item.Id}",
                    onDragSource, result);
            }

            RenderTabActions(gui, theme, idPrefix, trailing);

            if (overflowing && Navigation(gui, $"{idPrefix}/next", ">", visible.End < items.Count, theme))
                state.FirstVisible = visible.End;

            if (gui.Pass == Pass.Pass2Render) state.ViewportWidth = gui.CurrentNode.Rect.W;
        }

        return result;
    }

    static void PreviousTabPage(Gui gui, string idPrefix, TabStripTheme theme, TabStripState state,
        float[] widths, float availableWidth, bool overflowing)
    {
        if (overflowing && Navigation(gui, $"{idPrefix}/previous", "<", state.FirstVisible > 0, theme))
            state.FirstVisible = PreviousRange(state.FirstVisible, widths, availableWidth);
    }

    static (float[] Widths, float Available, bool Overflowing) PrepareTabWidths(Gui gui,
        IReadOnlyList<TabStripItem> items, string? activeId, TabStripTheme theme, TabStripState state, string idPrefix)
    {
        var ancestors = new List<StyleTarget>();
        for (var parent = gui.CurrentNode; parent is not null; parent = parent.Parent)
            if (parent.StyleTarget is { } ancestor) ancestors.Add(ancestor);
        var widths = items.Select(item => PreviewTabWidth(gui, item, theme, $"{idPrefix}/{item.Id}",
            item.Id == activeId, ancestors)).ToArray();
        var overflowing = state.ViewportWidth > 0 && widths.Sum() > state.ViewportWidth;
        var available = Math.Max(0, state.ViewportWidth - (overflowing ? NavigationButtonWidth * 2 : 0));
        var activeIndex = items.ToList().FindIndex(item => item.Id == activeId);
        if (overflowing) EnsureActiveIsVisible(state, activeIndex, widths, available);
        else state.FirstVisible = 0;
        return (widths, available, overflowing);
    }

    static float PreviewTabWidth(Gui gui, TabStripItem item, TabStripTheme theme, string id, bool isActive,
        IReadOnlyList<StyleTarget> ancestors)
    {
        var style = gui.ResolveStyle("tab", id: id, modifiers: isActive ? SelectedModifier : NoModifiers,
            ancestors: ancestors);
        var font = gui.GetStyleFont(style).Resized(theme.FontSize * gui.FontScale);
        var emoji = gui.CurrentNodeScope.Get<LayoutNodeScopeIconFont>().Value.Resized(font.Size);
        return TabWidth(item, theme, gui.MeasureLineWidth(item.Label, font, emoji));
    }

    /// <summary>The trailing action scope exists in both passes, including when no callback is supplied.</summary>
    static void RenderTabActions(Gui gui, TabStripTheme theme, string idPrefix, Action<Gui>? trailing)
    {
        using (gui.Node(-1, theme.Height, $"{idPrefix}/actions")
                   .ExpandWidth().Direction(Axis.Horizontal).ContentAlignX(1f).Enter())
            trailing?.Invoke(gui);
    }

    static TabStripResult RenderTab(Gui gui, TabStripItem item, bool isActive, TabStripTheme theme,
        string id, Func<TabStripItem, string, bool>? onDragSource, TabStripResult result)
    {
        var node = gui.StyledNode("tab", id: id, modifiers: isActive ? SelectedModifier : NoModifiers);
        var width = TabWidth(item, theme, gui.MeasureTextWidth(item.Label, theme.FontSize, node.Scope));

        using (node.Width(width)
                   .Height(theme.Height).Direction(Axis.Horizontal).Padding(8, 0).Gap(6f).ContentAlignY(0.5f).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
                result = HandleStripTab(gui, item, isActive, id, onDragSource, result);

            if (item.Icon is { } icon)
                using (gui.Node(theme.IconSize, theme.IconSize, $"{id}/icon").Enter())
                    icon(gui);

            gui.DrawText(item.Label, theme.FontSize, centerInRect: false);

            if (item.Closable) result = RenderClose(gui, item, theme, id, result);
        }

        return result;
    }

    static TabStripResult HandleStripTab(Gui gui, TabStripItem item, bool isActive, string id,
        Func<TabStripItem, string, bool>? onDragSource, TabStripResult result)
    {
        var interactable = gui.GetInteractable();
        if (isActive)
        {
            var rect = gui.CurrentNode.Rect;
            gui.DrawStyledBox(gui.ResolvePart("marker"), new Rect(rect.X, rect.Y, rect.W, 2));
        }
        if (interactable.OnClick()) result = result with { Activated = item };
        if (item.Closable && interactable.OnClick(MouseButton.Middle)) result = result with { Closed = item };
        if (onDragSource?.Invoke(item, id) == true) result = result with { Dragged = item };
        return result;
    }

    static TabStripResult RenderClose(Gui gui, TabStripItem item, TabStripTheme theme, string id,
        TabStripResult result)
    {
        // Blocks the tab underneath, so closing never also activates.
        using (gui.StyledNode("tab-close", id: $"{id}/close").Width(12).Height(Math.Max(1, theme.Height - 8))
                   .ContentAlignX(0.5f).ContentAlignY(0.5f).BlockInput()
                   .Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                var close = gui.GetInteractable();
                if (close.OnClick()) result = result with { Closed = item };

                if (item.Modified)
                {
                    var rect = gui.CurrentNode.Rect;
                    gui.DrawCircleFilled(new Vector2(rect.X + (rect.W / 2f), rect.Y + (rect.H / 2f)), 3.5f,
                        PartColor(gui, "marker", "background-color"));
                }
            }

            // Always built: a node that exists in only one pass never gets a rect.
            gui.DrawText(item.Modified ? " " : WidgetIcons.Xmark, theme.FontSize);
        }

        return result;
    }

    static float TabWidth(TabStripItem item, TabStripTheme theme, float textWidth)
    {
        return textWidth + 18
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
        using (gui.StyledNode("tab-nav", id: id, disabled: !enabled).Width(NavigationButtonWidth).Height(theme.Height)
                   .BlockInput().Enter())
        {
            if (gui.Pass != Pass.Pass2Render) return false;

            gui.DrawText(label, theme.FontSize, centerInRect: true);
            return enabled && gui.GetInteractable().OnClick();
        }
    }

    sealed class TabStripState
    {
        public int FirstVisible { get; set; }

        public float ViewportWidth { get; set; }
    }
}
