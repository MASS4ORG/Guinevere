using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    const float NavigationButtonWidth = 18f;

    /// <summary>What the user did to one strip tab this frame.</summary>
    readonly record struct StripOutcome(string? Activated, string? Closed);

    /// <summary>
    /// A row of tabs with an active one, optional icons, unsaved markers and close affordances, built with the same
    /// <see cref="TabBuilder"/> as <c>Tabs</c> (a tab's page content is ignored). The dock space draws its panel tabs
    /// with this, so a host's own tabs match without copying the look. Styled by the <c>tabstrip</c> rules,
    /// <c>tab</c> (<c>:selected</c> for the active one, with its drawn <c>marker</c> part), <c>tab-close</c> (its
    /// unsaved dot is the <c>dirty</c> part) and <c>tab-nav</c>. The strip fills its parent's width and is a compact
    /// control plus 2 high with compact text unless a sheet or the returned node says otherwise.
    /// </summary>
    /// <param name="gui">The GUI instance.</param>
    /// <param name="activeKey">The key of the active tab, or null; written when the user picks another tab.</param>
    /// <param name="build">Adds the tabs, in order; called every frame.</param>
    /// <param name="trailing">Draws into the width the tabs leave over, flowing from the right edge.</param>
    /// <param name="onDragSource">Receives a tab's key and node id so the caller can start a drag from it; return true
    /// when it did.</param>
    /// <param name="enabled">When false the strip matches <c>:disabled</c> and no tab can be picked or closed.</param>
    /// <param name="classes">Stylesheet classes for the strip.</param>
    /// <param name="id">Id of the strip's node and prefix of its tabs' ids; needed when a frame draws several strips
    /// from one call site; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>The strip's node, <see cref="LayoutNode.Changed"/> (and as a <c>bool</c>) on the frame the active tab moved.</returns>
    public static ControlNode TabStrip(this Gui gui, ref string? activeKey, Action<TabBuilder> build,
        Action<Gui>? trailing = null, Func<string, string, bool>? onDragSource = null, bool enabled = true,
        IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(build);

        ExcaliburStyles.Ensure(gui);
        var prefix = id ?? gui.AutomaticId(filePath, lineNumber);
        var state = gui.ControlState($"{prefix}/overflow", static () => new TabStripState());
        var tabs = LoadStripTabs(state, build, enabled);
        var strip = gui.StyledControl("tabstrip", classes, prefix, disabled: !enabled);
        UseFontSize(gui, strip, gui.ControlStyle.CompactFontSize);
        SizeUnlessStyled(strip, Size.Expand(), gui.ControlStyle.CompactHeight + 2f);
        var outcome = RenderStrip(gui, strip, state, tabs, activeKey, prefix, trailing, onDragSource);
        ApplyStripOutcome(state, tabs, strip, outcome, ref activeKey);
        return strip;
    }

    /// <summary>Lays out the visible tabs between the paging buttons and returns what the user did to them.</summary>
    static StripOutcome RenderStrip(Gui gui, ControlNode strip, TabStripState state, List<TabInfo> tabs,
        string? active, string prefix, Action<Gui>? trailing, Func<string, string, bool>? onDragSource)
    {
        StripOutcome outcome;
        using (strip.Direction(Axis.Horizontal).Enter())
        {
            var (widths, availableWidth, overflowing) = PrepareTabWidths(gui, tabs, active, state, prefix);
            var visible = VisibleRange(state.FirstVisible, widths, availableWidth, overflowing);
            PreviousTabPage(gui, prefix, state, widths, availableWidth, overflowing);
            outcome = RenderStripTabs(gui, tabs, active, widths, visible, prefix, onDragSource);
            RenderTabActions(gui, prefix, trailing);

            if (overflowing && Navigation(gui, $"{prefix}/next", ">", visible.End < tabs.Count))
            {
                state.FirstVisible = visible.End;
            }

            if (gui.Pass == Pass.Pass2Render)
            {
                state.ViewportWidth = gui.CurrentNode.Rect.W;
            }
        }

        return outcome;
    }

    /// <summary>Renders the visible tabs in order and folds what the user did to each into one outcome.</summary>
    static StripOutcome RenderStripTabs(Gui gui, List<TabInfo> tabs, string? active, float[] widths,
        (int Start, int End) visible, string prefix, Func<string, string, bool>? onDragSource)
    {
        var outcome = new StripOutcome(null, null);
        for (var index = visible.Start; index < visible.End; index++)
        {
            var tab = tabs[index];
            outcome = RenderTab(gui, tab, tab.Key == active, widths[index], $"{prefix}/{tab.Key}", onDragSource,
                outcome);
        }

        return outcome;
    }

    /// <summary>Applies the frame's close, then its activation; a moved active tab signals a change.</summary>
    static void ApplyStripOutcome(TabStripState state, List<TabInfo> tabs, ControlNode strip, StripOutcome outcome,
        ref string? activeKey)
    {
        if (outcome.Closed is { } closed)
        {
            CloseStripTab(state, tabs, closed);
        }

        if (outcome.Activated is { } activated && activated != activeKey)
        {
            activeKey = activated;
            strip.Raise(NodeSignals.Changed);
        }
    }

    /// <summary>Builds the tab list, keys each tab (explicit key, title label, else position) and drops the ones the strip closed itself.</summary>
    static List<TabInfo> LoadStripTabs(TabStripState state, Action<TabBuilder> build, bool enabled)
    {
        var builder = new TabBuilder();
        build(builder);
        var tabs = builder.GetTabs();
        for (var i = 0; i < tabs.Count; i++)
        {
            tabs[i].Key ??= tabs[i].Title.Label ?? $"#{i}";
            tabs[i].Enabled &= enabled;
        }

        tabs.RemoveAll(tab => state.Closed.Contains(tab.Key!));
        return tabs;
    }

    /// <summary>Hands a closed tab to its <c>onClose</c>, or hides it in the strip when the caller gave none.</summary>
    static void CloseStripTab(TabStripState state, List<TabInfo> tabs, string key)
    {
        var tab = tabs.Find(candidate => candidate.Key == key);
        if (tab?.OnClose is { } onClose)
        {
            onClose();
        }
        else
        {
            state.Closed.Add(key);
        }
    }

    static void PreviousTabPage(Gui gui, string prefix, TabStripState state, float[] widths, float availableWidth,
        bool overflowing)
    {
        if (overflowing && Navigation(gui, $"{prefix}/previous", "<", state.FirstVisible > 0))
        {
            state.FirstVisible = PreviousRange(state.FirstVisible, widths, availableWidth);
        }
    }

    static (float[] Widths, float Available, bool Overflowing) PrepareTabWidths(Gui gui, List<TabInfo> tabs,
        string? activeKey, TabStripState state, string prefix)
    {
        var ancestors = new List<StyleTarget>();
        for (var parent = gui.CurrentNode; parent is not null; parent = parent.Parent)
        {
            if (parent.StyleTarget is { } ancestor)
            {
                ancestors.Add(ancestor);
            }
        }

        var widths = tabs.Select(tab => PreviewTabWidth(gui, tab, $"{prefix}/{tab.Key}", tab.Key == activeKey,
            ancestors)).ToArray();
        var overflowing = state.ViewportWidth > 0 && widths.Sum() > state.ViewportWidth;
        var available = Math.Max(0, state.ViewportWidth - (overflowing ? NavigationButtonWidth * 2 : 0));
        var activeIndex = tabs.FindIndex(tab => tab.Key == activeKey);
        if (overflowing)
        {
            EnsureActiveIsVisible(state, activeIndex, widths, available);
        }
        else
        {
            state.FirstVisible = 0;
        }

        return (widths, available, overflowing);
    }

    /// <summary>
    /// A tab's width before layout, which paging the overflowing strip needs: its content measured in the tab's font
    /// (an icon is as wide as the text is tall, parts of a row are a third of that apart), plus its padding and close cross.
    /// </summary>
    static float PreviewTabWidth(Gui gui, TabInfo tab, string id, bool isActive, IReadOnlyList<StyleTarget> ancestors)
    {
        var style = gui.ResolveStyle("tab", id: id, modifiers: isActive ? SelectedModifier : NoModifiers,
            ancestors: ancestors);
        var font = gui.GetStyleFont(style).Resized(gui.ControlStyle.CompactFontSize * gui.FontScale);
        var emoji = gui.CurrentNodeScope.Get<LayoutNodeScopeIconFont>().Value.Resized(font.Size);
        return TabContentWidth(gui, tab.Title, font, emoji) + 18 + (tab.Closable ? 18 : 0);
    }

    static float TabContentWidth(Gui gui, Content content, Font font, Font emoji)
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
                    width += TabContentWidth(gui, parts[i], font, emoji) + (i > 0 ? font.Size / 3f : 0f);
                }

                return width;
            default:
                // Widgets without a label are estimated as one icon slot; label them to size them like text.
                return content.Label is { Length: > 0 } label
                    ? gui.MeasureLineWidth(label, font, emoji)
                    : font.Size * 1.25f;
        }
    }

    /// <summary>The trailing action scope exists in both passes, including when no callback is supplied.</summary>
    static void RenderTabActions(Gui gui, string prefix, Action<Gui>? trailing)
    {
        using (gui.Node(-1, -1, $"{prefix}/actions")
                   .ExpandWidth().ExpandHeight().Direction(Axis.Horizontal).AlignContentX(1f).Enter())
        {
            trailing?.Invoke(gui);
        }
    }

    static StripOutcome RenderTab(Gui gui, TabInfo tab, bool isActive, float width, string id,
        Func<string, string, bool>? onDragSource, StripOutcome outcome)
    {
        var node = gui.StyledNode("tab", tab.Classes, id, modifiers: isActive ? SelectedModifier : NoModifiers,
            disabled: !tab.Enabled);

        using (node.Width(width).ExpandHeight().Direction(Axis.Horizontal).Padding(8, 0).Gap(6f).AlignContentY(0.5f)
                   .Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                outcome = HandleStripTab(gui, tab, isActive, id, onDragSource, outcome);
            }

            gui.Content(tab.Title);

            if (tab.Closable)
            {
                outcome = RenderClose(gui, tab, id, outcome);
            }
        }

        return outcome;
    }

    static StripOutcome HandleStripTab(Gui gui, TabInfo tab, bool isActive, string id,
        Func<string, string, bool>? onDragSource, StripOutcome outcome)
    {
        var interactable = gui.GetInteractable();
        if (isActive)
        {
            var rect = gui.CurrentNode.Rect;
            gui.DrawStyledBox(gui.ResolvePart("marker"), new Rect(rect.X, rect.Y, rect.W, 2));
        }

        if (!tab.Enabled)
        {
            return outcome;
        }

        if (interactable.OnClick())
        {
            outcome = outcome with { Activated = tab.Key };
        }

        if (tab.Closable && interactable.OnClick(MouseButton.Middle))
        {
            outcome = outcome with { Closed = tab.Key };
        }

        onDragSource?.Invoke(tab.Key!, id);
        return outcome;
    }

    static StripOutcome RenderClose(Gui gui, TabInfo tab, string id, StripOutcome outcome)
    {
        // Blocks the tab underneath, so closing never also activates.
        using (gui.StyledNode("tab-close", id: $"{id}/close", disabled: !tab.Enabled).Width(12)
                   .Height(Math.Max(1, gui.ControlStyle.CompactHeight - 6))
                   .AlignContentX(0.5f).AlignContentY(0.5f).BlockInput()
                   .Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                if (tab.Enabled && gui.GetInteractable().OnClick())
                {
                    outcome = outcome with { Closed = tab.Key };
                }

                if (tab.Modified)
                {
                    var rect = gui.CurrentNode.Rect;
                    gui.DrawCircleFilled(new Vector2(rect.X + (rect.W / 2f), rect.Y + (rect.H / 2f)), 3.5f,
                        PartColor(gui, "dirty", "background-color"));
                }
            }

            // Always built: a node that exists in only one pass never gets a rect.
            gui.DrawText(tab.Modified ? " " : WidgetIcons.Xmark);
        }

        return outcome;
    }

    static (int Start, int End) VisibleRange(int first, IReadOnlyList<float> widths, float available,
        bool overflowing)
    {
        if (!overflowing)
        {
            return (0, widths.Count);
        }

        first = Math.Clamp(first, 0, Math.Max(0, widths.Count - 1));
        var end = first;
        var used = 0f;
        while (end < widths.Count && (end == first || used + widths[end] <= available))
        {
            used += widths[end++];
        }

        return (first, end);
    }

    static void EnsureActiveIsVisible(TabStripState state, int activeIndex, IReadOnlyList<float> widths,
        float available)
    {
        if (activeIndex < 0)
        {
            return;
        }

        var range = VisibleRange(state.FirstVisible, widths, available, overflowing: true);
        if (activeIndex < range.Start || activeIndex >= range.End)
        {
            state.FirstVisible = activeIndex;
        }
    }

    static int PreviousRange(int first, IReadOnlyList<float> widths, float available)
    {
        if (first == 0)
        {
            return 0;
        }

        var previous = first - 1;
        while (previous > 0 && VisibleRange(previous - 1, widths, available, overflowing: true).End >= first)
        {
            previous--;
        }

        return previous;
    }

    static bool Navigation(Gui gui, string id, string label, bool enabled)
    {
        using (gui.StyledNode("tab-nav", id: id, disabled: !enabled).Width(NavigationButtonWidth).ExpandHeight()
                   .BlockInput().Enter())
        {
            if (gui.Pass != Pass.Pass2Render)
            {
                return false;
            }

            gui.DrawText(label, centerInRect: true);
            return enabled && gui.GetInteractable().OnClick();
        }
    }

    sealed class TabStripState
    {
        public int FirstVisible { get; set; }

        public float ViewportWidth { get; set; }

        /// <summary>Keys of the tabs the strip closed itself because the caller gave them no <c>onClose</c>.</summary>
        public HashSet<string> Closed { get; } = [];
    }
}
