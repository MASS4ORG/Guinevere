using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    static readonly string[] PlainClass = ["plain"];
    static readonly string[] VerticalClass = ["vertical"];
    static readonly string[] PillClass = ["pill"];
    static readonly string[] VerticalPlainClasses = ["vertical", "plain"];

    const float TabPaddingX = 12f;
    const float VerticalTabWidth = 120f;
    const float VerticalTabHeight = 36f;
    const float TabPanelHeight = 200f;

    /// <summary>How a tab container lays out its bar and tabs.</summary>
    enum TabLook { Bar, Vertical, Pill }

    /// <summary>
    /// A tab container with internal state. Styled by the <c>tabs</c> container, the <c>tabbar</c> rules (class
    /// <c>plain</c> without a border), its <c>tab</c> children (<c>:selected</c>, <c>:hover</c>, <c>:focus</c>,
    /// <c>:disabled</c>, with a drawn <c>marker</c> part under the active one), <c>tab-close</c> and the
    /// <c>tabpanel</c> holding the content. Bar height and tab sizes come from the sheet, else the control metrics.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="activeTabIndex">The active tab, updated when the user picks or closes one.</param>
    /// <param name="buildTabs">Adds the tabs.</param>
    /// <param name="showBorder">Whether the bar and panel draw their box; false adds the <c>plain</c> class.</param>
    /// <param name="onTabClosed">Called with the index and key of a tab the user closed.</param>
    /// <param name="enabled">Whether any tab can be picked.</param>
    /// <param name="classes">Stylesheet classes for the tab container.</param>
    /// <param name="id">Stable state id, needed by <see cref="RestoreTabs"/>; defaults to the call site.</param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    /// <returns>The <c>tabs</c> node; <see cref="NodeSignals.Changed"/> on the call that moved the active tab.</returns>
    public static ControlNode Tabs(this Gui gui, ref int activeTabIndex, Action<TabBuilder> buildTabs,
        bool showBorder = true,
        Action<int, string>? onTabClosed = null,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0) =>
        TabSet(gui, ref activeTabIndex, buildTabs, TabLook.Bar, showBorder, onTabClosed, enabled, classes, id,
            filePath, lineNumber);

    /// <summary>Tabs stacked on the side (<c>tabbar.vertical</c>), with the content to their right.</summary>
    /// <inheritdoc cref="Tabs(Gui, ref int, Action{TabBuilder}, bool, Action{int, string}?, bool, IReadOnlyList{string}?, string?, string, int)"/>
    public static ControlNode VerticalTabs(this Gui gui, ref int activeTabIndex, Action<TabBuilder> buildTabs,
        bool showBorder = true,
        Action<int, string>? onTabClosed = null,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0) =>
        TabSet(gui, ref activeTabIndex, buildTabs, TabLook.Vertical, showBorder, onTabClosed, enabled, classes, id,
            filePath, lineNumber);

    /// <summary>Rounded tabs in a borderless bar (<c>tabbar.pill</c>), spaced by the control spacing.</summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="activeTabIndex">The active tab, updated when the user picks or closes one.</param>
    /// <param name="buildTabs">Adds the tabs.</param>
    /// <param name="onTabClosed">Called with the index and key of a tab the user closed.</param>
    /// <param name="enabled">Whether any tab can be picked.</param>
    /// <param name="classes">Stylesheet classes for the tab container.</param>
    /// <param name="id">Stable state id, needed by <see cref="RestoreTabs"/>; defaults to the call site.</param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    /// <returns>The <c>tabs</c> node; <see cref="NodeSignals.Changed"/> on the call that moved the active tab.</returns>
    public static ControlNode PillTabs(this Gui gui, ref int activeTabIndex, Action<TabBuilder> buildTabs,
        Action<int, string>? onTabClosed = null,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0) =>
        TabSet(gui, ref activeTabIndex, buildTabs, TabLook.Pill, true, onTabClosed, enabled, classes, id,
            filePath, lineNumber);

    /// <summary>A tab bar without content, for callers that draw the active page themselves.</summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="activeTabIndex">The active tab, updated when the user picks one.</param>
    /// <param name="titles">What each tab shows.</param>
    /// <param name="showBorder">Whether the bar draws its box; false adds the <c>plain</c> class.</param>
    /// <param name="enabled">Whether any tab can be picked.</param>
    /// <param name="classes">Stylesheet classes for the tab container.</param>
    /// <param name="id">Stable state id; defaults to the call site.</param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    /// <returns>The <c>tabs</c> node; <see cref="NodeSignals.Changed"/> on the call that moved the active tab.</returns>
    public static ControlNode TabBar(this Gui gui, ref int activeTabIndex, IReadOnlyList<Content> titles,
        bool showBorder = true,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(titles);
        return TabSet(gui, ref activeTabIndex, builder =>
        {
            foreach (var title in titles)
            {
                builder.Tab(title);
            }
        }, TabLook.Bar, showBorder, null, enabled, classes, id, filePath, lineNumber);
    }

    static ControlNode TabSet(Gui gui, ref int activeTabIndex, Action<TabBuilder> buildTabs, TabLook look,
        bool showBorder, Action<int, string>? onTabClosed, bool enabled, IReadOnlyList<string>? classes, string? id,
        string filePath, int lineNumber)
    {
        ExcaliburStyles.Ensure(gui);
        var stateId = id ?? gui.AutomaticId(filePath, lineNumber);
        var initial = activeTabIndex;
        var state = gui.ControlState(stateId, () => new TabsState { ActiveTabIndex = initial });

        if (gui.Pass == Pass.Pass1Build && state.RequestedActiveTabIndex is { } requested)
        {
            activeTabIndex = requested;
            state.RequestedActiveTabIndex = null;
        }

        var hasTabs = LoadTabs(state, buildTabs, ref activeTabIndex, enabled);
        var node = gui.StyledControl("tabs", classes, stateId, NoModifiers, disabled: !enabled,
            filePath: filePath, lineNumber: lineNumber);
        UseControlFontSize(gui, node);
        SizeTabSet(gui, node, state, look, hasTabs);

        if (hasTabs)
        {
            using (node.Enter())
            {
                RenderTabBar(gui, state, look, showBorder);
                RenderActiveTabContent(gui, state, look == TabLook.Pill || showBorder ? null : PlainClass);
            }
        }

        if (gui.Pass == Pass.Pass1Build)
        {
            state.SuppressActivation = false;
        }

        ApplyTabClose(state, onTabClosed);
        if (state.ActiveTabIndex != initial)
        {
            node.Raise(NodeSignals.Changed);
        }

        activeTabIndex = state.ActiveTabIndex;
        return node;
    }

    /// <summary>The container's size where the sheet leaves it unset: a bar row plus a page, or the bars' side.</summary>
    static void SizeTabSet(Gui gui, LayoutNode node, TabsState state, TabLook look, bool hasTabs)
    {
        if (!hasTabs)
        {
            SizeUnlessStyled(node, Size.Pixels(0), Size.Pixels(0));
            return;
        }

        var page = state.Tabs.Exists(static t => t.Content is not null) ? TabPanelHeight : 0f;
        SizeUnlessStyled(node, Size.Expand(),
            look == TabLook.Bar ? Size.Pixels(gui.ControlStyle.FieldHeight + page) : Size.Expand());
        node.Direction(look == TabLook.Vertical ? Axis.Horizontal : Axis.Vertical);
    }

    static void RenderTabBar(Gui gui, TabsState state, TabLook look, bool showBorder)
    {
        var spacing = gui.ControlStyle.Spacing;
        var bar = gui.StyledNode("tabbar", look switch
        {
            TabLook.Vertical => showBorder ? VerticalClass : VerticalPlainClasses,
            TabLook.Pill => PillClass,
            _ => showBorder ? null : PlainClass
        });
        switch (look)
        {
            case TabLook.Vertical:
                SizeUnlessStyled(bar, Size.Pixels(VerticalTabWidth), Size.Expand());
                bar.Direction(Axis.Vertical);
                break;
            case TabLook.Pill:
                SizeUnlessStyled(bar, null, Size.Pixels(gui.ControlStyle.CompactHeight + spacing * 2));
                bar.Direction(Axis.Horizontal).Gap(spacing).Padding(spacing);
                break;
            default:
                SizeUnlessStyled(bar, null, Size.Pixels(gui.ControlStyle.FieldHeight));
                bar.Direction(Axis.Horizontal);
                break;
        }

        using (bar.Enter())
        {
            for (var i = 0; i < state.Tabs.Count; i++)
            {
                RenderTab(gui, state, i, look);
            }
        }
    }

    /// <summary>
    /// One tab: its title and, when closable, the close button. Bar tabs are keyboard selectable; vertical and pill
    /// tabs select on click. Sized to the title unless the sheet sets a size.
    /// </summary>
    static void RenderTab(Gui gui, TabsState state, int tabIndex, TabLook look)
    {
        var tab = state.Tabs[tabIndex];
        var isActive = state.ActiveTabIndex == tabIndex;
        var node = gui.StyledNode("tab", tab.Classes, modifiers: isActive ? SelectedModifier : NoModifiers,
            disabled: !tab.Enabled);
        if (look == TabLook.Vertical)
        {
            SizeUnlessStyled(node, Size.Percentage(1f), Size.Pixels(VerticalTabHeight));
            PadUnlessStyled(node, gui.ControlStyle.Spacing, 0);
        }
        else
        {
            SizeUnlessStyled(node, Size.FitContent(), Size.Percentage(1f));
            PadUnlessStyled(node, TabPaddingX, 0);
            node.AlignContentX(0.5f);
        }

        using (node.Direction(Axis.Horizontal).Gap(TabCloseGap).AlignContentY(0.5f).Enter())
        {
            ControlBehaviorResult? behavior = null;
            if (look == TabLook.Bar)
            {
                behavior = gui.Selectable(isActive, new ControlBehaviorOptions(
                    Enabled: tab.Enabled, Role: ControlRole.Tab, Label: tab.Title.Label));
                if (behavior.Value.Activated && !state.SuppressActivation)
                {
                    state.ActiveTabIndex = tabIndex;
                }
            }

            if (gui.Pass == Pass.Pass2Render && isActive && look != TabLook.Pill)
            {
                DrawTabMarker(gui, vertical: look == TabLook.Vertical);
            }

            RenderTabTitle(gui, tab, look);
            var overClose = tab.Closable && RenderTabCloseButton(gui, state, tabIndex);
            if (gui.Pass != Pass.Pass2Render)
            {
                return;
            }

            if (behavior is { } result)
            {
                HandleTabBehavior(gui, state, tabIndex, tab, result);
            }
            else
            {
                HandleTabPointer(gui, state, tabIndex, tab, overClose);
            }
        }
    }

    /// <summary>The title; a vertical tab's title takes the free width so the close button sits at the edge.</summary>
    static void RenderTabTitle(Gui gui, TabInfo tab, TabLook look)
    {
        if (look != TabLook.Vertical)
        {
            gui.Content(tab.Title);
            return;
        }

        using (gui.Node().ExpandWidth().Height(Size.FitContent()).Direction(Axis.Horizontal).AlignContentY(0.5f)
                   .Enter())
        {
            gui.Content(tab.Title);
        }
    }

    static void HandleTabBehavior(Gui gui, TabsState state, int tabIndex, TabInfo tab, ControlBehaviorResult behavior)
    {
        var closed = gui.GetInteractable().OnClick(MouseButton.Middle) && tab.Closable;
        if (behavior.Is(ControlVisualState.Focused))
        {
            NavigateTabs(gui, state, tabIndex);
        }

        if (closed && tab.Enabled)
        {
            state.TabToClose = (tabIndex, tab.Key!);
        }
    }

    /// <summary>Selects on click (not on the close button) and closes on a middle click; render pass only.</summary>
    static void HandleTabPointer(Gui gui, TabsState state, int tabIndex, TabInfo tab, bool overClose)
    {
        var interactable = gui.GetInteractable();
        if (interactable.OnClick() && tab.Enabled && !overClose)
        {
            state.ActiveTabIndex = tabIndex;
        }

        if (interactable.OnClick(MouseButton.Middle) && tab.Enabled && tab.Closable)
        {
            state.TabToClose = (tabIndex, tab.Key!);
        }
    }

    /// <summary>Left/Right move the selection to the nearest enabled tab.</summary>
    static void NavigateTabs(Gui gui, TabsState state, int tabIndex)
    {
        if (gui.Input.IsKeyPressed(KeyboardKey.Left))
        {
            for (var i = tabIndex - 1; i >= 0; i--)
            {
                if (state.Tabs[i].Enabled) { state.RequestedActiveTabIndex = i; break; }
            }
        }
        else if (gui.Input.IsKeyPressed(KeyboardKey.Right))
        {
            for (var i = tabIndex + 1; i < state.Tabs.Count; i++)
            {
                if (state.Tabs[i].Enabled) { state.RequestedActiveTabIndex = i; break; }
            }
        }
    }

    static void RenderActiveTabContent(Gui gui, TabsState state, IReadOnlyList<string>? classes)
    {
        if (state.ActiveTabIndex < 0 || state.ActiveTabIndex >= state.Tabs.Count)
        {
            return;
        }

        var activeTab = state.Tabs[state.ActiveTabIndex];
        if (activeTab.Content == null)
        {
            return;
        }

        using (gui.StyledNode("tabpanel", classes).Expand().Padding(12).Enter())
        {
            activeTab.Content();
        }
    }

    /// <summary>Draws the active tab's <c>marker</c> part: a bar under it, or beside it in a vertical bar.</summary>
    static void DrawTabMarker(Gui gui, bool vertical)
    {
        var rect = gui.CurrentNode.Rect;
        var marker = vertical ? new Rect(rect.X, rect.Y, 3, rect.H) : new Rect(rect.X, rect.Y + rect.H - 3, rect.W, 3);
        gui.DrawStyledBox(gui.ResolvePart("marker"), marker);
    }

    /// <summary>
    /// Rebuilds the tab list, keys each tab, leaves out tabs the user closed (closure lives in the widget), and clamps
    /// the active index. Returns whether any tab remains.
    /// </summary>
    static bool LoadTabs(TabsState state, Action<TabBuilder> buildTabs, ref int activeTabIndex, bool enabled)
    {
        var builder = new TabBuilder();
        buildTabs(builder);
        state.Tabs = builder.GetTabs();
        for (var i = 0; i < state.Tabs.Count; i++)
        {
            var tab = state.Tabs[i];
            tab.Key ??= tab.Title.Label ?? $"#{i}";
            tab.Enabled &= enabled;
        }

        state.Tabs.RemoveAll(tab => state.Closed.Contains(tab.Key!));

        if (activeTabIndex < 0 || activeTabIndex >= state.Tabs.Count)
        {
            activeTabIndex = state.Tabs.Count > 0 ? 0 : -1;
        }

        state.ActiveTabIndex = activeTabIndex;
        return state.Tabs.Count > 0;
    }

    /// <summary>Closes the tab requested this frame, keeping the active tab on the same page where possible.</summary>
    static void ApplyTabClose(TabsState state, Action<int, string>? onTabClosed)
    {
        if (state.TabToClose is not { } closeRequest)
        {
            return;
        }

        state.TabToClose = null;
        state.SuppressActivation = true;
        state.Closed.Add(closeRequest.Key);

        var openCount = state.Tabs.Count - 1;
        if (closeRequest.Index < state.ActiveTabIndex)
        {
            state.ActiveTabIndex--;
        }
        else if (closeRequest.Index == state.ActiveTabIndex)
        {
            state.ActiveTabIndex = Math.Min(closeRequest.Index, Math.Max(0, openCount - 1));
        }

        onTabClosed?.Invoke(closeRequest.Index, closeRequest.Key);
    }

    /// <summary>Clears all tabs states (useful for cleanup).</summary>
    public static void ClearTabsStates(this Gui gui) => gui.ClearControlStates<TabsState>();

    /// <summary>
    /// Reopens every tab in a tab container that the user closed. The container must have been given that
    /// <paramref name="id"/> when <c>Tabs</c>/<c>VerticalTabs</c>/<c>PillTabs</c> was called.
    /// </summary>
    public static void RestoreTabs(this Gui gui, string id)
    {
        var state = gui.TryGetControlState<TabsState>(id);
        if (state is null)
        {
            return;
        }

        state.Closed.Clear();
        state.ActiveTabIndex = 0;
    }
}
