using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    static readonly string[] PlainClass = ["plain"];
    static readonly string[] VerticalClass = ["vertical"];
    static readonly string[] PillClass = ["pill"];
    static readonly string[] VerticalPlainClasses = ["vertical", "plain"];

    /// <summary>
    /// A tab container with internal state. Styled by the <c>tabbar</c> rules (class <c>plain</c> without a border),
    /// its <c>tab</c> children (<c>:selected</c>, <c>:hover</c>, <c>:focus</c>, <c>:disabled</c>, with a drawn
    /// <c>marker</c> part under the active one), <c>tab-close</c> and the <c>tabpanel</c> holding the content.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="activeTabIndex">The active tab, updated when the user picks or closes one.</param>
    /// <param name="buildTabs">Adds the tabs.</param>
    /// <param name="tabBarHeight">Height of the bar.</param>
    /// <param name="fontSize">Label size.</param>
    /// <param name="showBorder">Whether the bar and panel draw their box; false adds the <c>plain</c> class.</param>
    /// <param name="id">Stable state id; defaults to the call site.</param>
    /// <param name="onTabClosed">Called with the index and title of a tab the user closed.</param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    public static void Tabs(this Gui gui, ref int activeTabIndex, Action<TabBuilder> buildTabs,
        float tabBarHeight = 32,
        float fontSize = ControlMetrics.FontSize,
        bool showBorder = true,
        string id = "",
        Action<int, string>? onTabClosed = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        fontSize = gui.ControlStyle.FontSizeOr(fontSize);
        ExcaliburStyles.Ensure(gui);

        var stateId = string.IsNullOrEmpty(id) ? gui.NodeId(filePath, lineNumber) : id;
        var state = GetOrCreateTabsState(gui, stateId, activeTabIndex, tabBarHeight);

        if (gui.Pass == Pass.Pass1Build && state.RequestedActiveTabIndex is { } requested)
        {
            activeTabIndex = requested;
            state.RequestedActiveTabIndex = null;
        }

        if (!LoadTabs(state, buildTabs, ref activeTabIndex)) return;

        var totalHeight = CalculateTabsHeight(state, tabBarHeight);
        var classes = showBorder ? null : PlainClass;

        using (gui.Node().Expand().Height(totalHeight).Direction(Axis.Vertical).Enter())
        {
            using (gui.StyledNode("tabbar", classes).Height(state.TabBarHeight).Direction(Axis.Horizontal).Enter())
            {
                for (var i = 0; i < state.Tabs.Count; i++) RenderTabButton(gui, state, i, fontSize);
            }

            RenderActiveTabContent(gui, state, classes);
        }

        ApplyTabClose(state, onTabClosed);
        activeTabIndex = state.ActiveTabIndex;
    }

    /// <summary>A tab container that returns the active tab index instead of changing a field.</summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="activeTabIndex">The active tab.</param>
    /// <param name="buildTabs">Adds the tabs.</param>
    /// <param name="tabBarHeight">Height of the bar.</param>
    /// <param name="fontSize">Label size.</param>
    /// <param name="showBorder">Whether the bar and panel draw their box; false adds the <c>plain</c> class.</param>
    /// <param name="id">Stable state id; defaults to the call site.</param>
    /// <param name="onTabClosed">Called with the index and title of a tab the user closed.</param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    /// <returns>The active tab after this frame.</returns>
    public static int Tabs(this Gui gui, int activeTabIndex, Action<TabBuilder> buildTabs,
        float tabBarHeight = 32,
        float fontSize = ControlMetrics.FontSize,
        bool showBorder = true,
        string id = "",
        Action<int, string>? onTabClosed = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var temp = activeTabIndex;
        gui.Tabs(ref temp, buildTabs, tabBarHeight, fontSize, showBorder, id, onTabClosed, filePath, lineNumber);
        return temp;
    }

    /// <summary>A tab bar without content, for callers that draw the active page themselves.</summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="tabTitles">The tab labels.</param>
    /// <param name="activeTabIndex">The active tab, updated when the user picks one.</param>
    /// <param name="height">Height of the bar.</param>
    /// <param name="fontSize">Label size.</param>
    /// <param name="showBorder">Whether the bar draws its box; false adds the <c>plain</c> class.</param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    public static void TabBar(this Gui gui, string[] tabTitles, ref int activeTabIndex,
        float height = ControlMetrics.FieldHeight,
        float fontSize = ControlMetrics.FontSize,
        bool showBorder = true,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        height = gui.ControlStyle.FieldHeightOr(height);

        gui.Tabs(ref activeTabIndex, builder =>
            {
                foreach (var title in tabTitles) builder.Tab(title);
            }, height, fontSize, showBorder,
            filePath: filePath, lineNumber: lineNumber);
    }

    static TabsState GetOrCreateTabsState(Gui gui, string id, int initialActiveIndex, float tabBarHeight) =>
        gui.ControlState(id,
            () => new TabsState { ActiveTabIndex = initialActiveIndex, TabBarHeight = tabBarHeight });

    static float CalculateTabsHeight(TabsState state, float tabBarHeight) =>
        tabBarHeight + (state.Tabs.Any(t => t.Content != null) ? 200 : 0);

    /// <summary>A styled <c>tab</c> node: <c>:selected</c> when active, <c>:disabled</c> when the tab is.</summary>
    static LayoutNode TabNode(Gui gui, TabInfo tab, bool isActive) =>
        gui.StyledNode("tab", tab.Classes, modifiers: isActive ? SelectedModifier : NoModifiers,
            disabled: !tab.Enabled);

    static void RenderTabButton(Gui gui, TabsState state, int tabIndex, float fontSize)
    {
        var tab = state.Tabs[tabIndex];
        var isActive = state.ActiveTabIndex == tabIndex;
        var tabWidth = CalculateTabWidth(tab.Title, fontSize, tab.Closable);

        using (TabNode(gui, tab, isActive).Width(tabWidth).Height(state.TabBarHeight).Direction(Axis.Horizontal)
                   .Enter())
        {
            var behavior = gui.Selectable(isActive, new ControlBehaviorOptions(
                Enabled: tab.Enabled, Role: ControlRole.Tab, Label: tab.Title));
            if (behavior.Activated) state.ActiveTabIndex = tabIndex;

            if (gui.Pass == Pass.Pass2Render)
            {
                var closed = gui.GetInteractable().OnClick(MouseButton.Middle) && tab.Closable;
                if (behavior.Is(ControlVisualState.Focused)) NavigateTabs(gui, state, tabIndex);
                if (closed && tab.Enabled) state.TabToClose = (tabIndex, tab.Title);
                if (isActive) DrawTabMarker(gui, vertical: false);
            }

            using (gui.Node().Expand().Height(state.TabBarHeight).Enter())
                gui.DrawText(tab.Title, fontSize, centerInRect: true);

            if (tab.Closable) RenderTabCloseButton(gui, state, tabIndex);
        }
    }

    /// <summary>Left/Right move the selection to the nearest enabled tab.</summary>
    static void NavigateTabs(Gui gui, TabsState state, int tabIndex)
    {
        if (gui.Input.IsKeyPressed(KeyboardKey.Left))
        {
            for (var i = tabIndex - 1; i >= 0; i--)
                if (state.Tabs[i].Enabled) { state.RequestedActiveTabIndex = i; break; }
        }
        else if (gui.Input.IsKeyPressed(KeyboardKey.Right))
        {
            for (var i = tabIndex + 1; i < state.Tabs.Count; i++)
                if (state.Tabs[i].Enabled) { state.RequestedActiveTabIndex = i; break; }
        }
    }

    static void RenderActiveTabContent(Gui gui, TabsState state, IReadOnlyList<string>? classes)
    {
        if (state.ActiveTabIndex < 0 || state.ActiveTabIndex >= state.Tabs.Count) return;

        var activeTab = state.Tabs[state.ActiveTabIndex];
        if (activeTab.Content == null) return;

        using (gui.StyledNode("tabpanel", classes).Expand().Padding(12).Enter())
            activeTab.Content();
    }

    const float TabCloseButtonSize = 18f;

    static float CalculateTabWidth(string title, float fontSize, bool closable)
    {
        var font = new SKFont { Size = fontSize };
        font.MeasureText(title, out var textBounds);
        return textBounds.Width + 24 + (closable ? TabCloseButtonSize + 6 : 0);
    }

    /// <summary>True when the pointer sits over the "×" that closes a closable tab.</summary>
    static bool OverTabCloseButton(bool closable, Rect rect, Vector2 mousePos)
    {
        if (!closable) return false;
        var closeRect = new Rect(rect.X + rect.W - TabCloseButtonSize - 4,
            rect.Y + (rect.H - TabCloseButtonSize) * 0.5f, TabCloseButtonSize + 8, TabCloseButtonSize + 8);
        return closeRect.Contains(mousePos);
    }

    static void RenderTabCloseButton(Gui gui, TabsState state, int tabIndex)
    {
        var tab = state.Tabs[tabIndex];

        using (gui.StyledNode("tab-close").Width(TabCloseButtonSize).Height(TabCloseButtonSize).Enter())
        {
            if (gui.Pass != Pass.Pass2Render) return;

            var rect = gui.CurrentNode.Rect;
            var font = new SKFont { Size = 12f };
            font.MeasureText("×", out var bounds);
            var pos = new Vector2(rect.X + (rect.W - bounds.Width) * 0.5f,
                rect.Y + (rect.H + bounds.Height) * 0.5f);
            gui.CurrentNode.DrawList.Add(new Text("×", pos, font, new SKPaint
            {
                Color = gui.CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value, IsAntialias = true
            }));

            if (gui.GetInteractable().OnClick() && tab.Enabled) state.TabToClose = (tabIndex, tab.Title);
        }
    }

    /// <summary>Draws the active tab's <c>marker</c> part: a bar under it, or beside it in a vertical bar.</summary>
    static void DrawTabMarker(Gui gui, bool vertical)
    {
        var rect = gui.CurrentNode.Rect;
        var marker = vertical ? new Rect(rect.X, rect.Y, 3, rect.H) : new Rect(rect.X, rect.Y + rect.H - 3, rect.W, 3);
        gui.DrawRect(marker, PartColor(gui, "marker", "background-color"));
    }

    /// <summary>
    /// Clears all tabs states (useful for cleanup)
    /// </summary>
    public static void ClearTabsStates(this Gui gui)
    {
        gui.ClearControlStates<TabsState>();
    }

    /// <summary>
    /// Reopens every tab in a tab stack that was closed with a middle-click. The tab stack must have
    /// been given that <paramref name="id"/> when <c>Tabs</c>/<c>VerticalTabs</c>/<c>PillTabs</c> was called.
    /// </summary>
    public static void RestoreTabs(this Gui gui, string id)
    {
        var state = gui.TryGetControlState<TabsState>(id);
        if (state is null) return;
        state.Closed.Clear();
        state.ActiveTabIndex = 0;
    }
}

/// <summary>
/// Extended tab controls with additional features
/// </summary>
public static partial class ControlsExtensions
{
    /// <summary>Tabs stacked on the side (<c>tabbar.vertical</c>), with the content to their right.</summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="activeTabIndex">The active tab, updated when the user picks or closes one.</param>
    /// <param name="buildTabs">Adds the tabs.</param>
    /// <param name="tabWidth">Width of the bar.</param>
    /// <param name="fontSize">Label size.</param>
    /// <param name="showBorder">Whether the bar and panel draw their box; false adds the <c>plain</c> class.</param>
    /// <param name="id">Stable state id; defaults to the call site.</param>
    /// <param name="onTabClosed">Called with the index and title of a tab the user closed.</param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    public static void VerticalTabs(this Gui gui, ref int activeTabIndex, Action<TabBuilder> buildTabs,
        float tabWidth = 120,
        float fontSize = ControlMetrics.FontSize,
        bool showBorder = true,
        string id = "",
        Action<int, string>? onTabClosed = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        fontSize = gui.ControlStyle.FontSizeOr(fontSize);
        ExcaliburStyles.Ensure(gui);

        var stateId = string.IsNullOrEmpty(id) ? gui.NodeId(filePath, lineNumber) : id;
        var state = GetOrCreateTabsState(gui, stateId, activeTabIndex, 32);

        if (!LoadTabs(state, buildTabs, ref activeTabIndex)) return;

        using (gui.Node().Expand().Direction(Axis.Horizontal).Enter())
        {
            using (gui.StyledNode("tabbar", showBorder ? VerticalClass : VerticalPlainClasses).Width(tabWidth)
                       .Expand().Direction(Axis.Vertical).Enter())
            {
                for (var i = 0; i < state.Tabs.Count; i++) RenderVerticalTabButton(gui, state, i, tabWidth, fontSize);
            }

            RenderActiveTabContent(gui, state, showBorder ? null : PlainClass);
        }

        ApplyTabClose(state, onTabClosed);
        activeTabIndex = state.ActiveTabIndex;
    }

    /// <summary>Rounded tabs in a borderless bar (<c>tabbar.pill</c>).</summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="activeTabIndex">The active tab, updated when the user picks or closes one.</param>
    /// <param name="buildTabs">Adds the tabs.</param>
    /// <param name="tabBarHeight">Height of the bar.</param>
    /// <param name="fontSize">Label size.</param>
    /// <param name="spacing">Gap and padding around the pills.</param>
    /// <param name="id">Stable state id; defaults to the call site.</param>
    /// <param name="onTabClosed">Called with the index and title of a tab the user closed.</param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    public static void PillTabs(this Gui gui, ref int activeTabIndex, Action<TabBuilder> buildTabs,
        float tabBarHeight = 40,
        float fontSize = ControlMetrics.FontSize,
        float spacing = ControlMetrics.Spacing,
        string id = "",
        Action<int, string>? onTabClosed = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        fontSize = gui.ControlStyle.FontSizeOr(fontSize);
        spacing = gui.ControlStyle.SpacingOr(spacing);
        ExcaliburStyles.Ensure(gui);

        var stateId = string.IsNullOrEmpty(id) ? gui.NodeId(filePath, lineNumber) : id;
        var state = GetOrCreateTabsState(gui, stateId, activeTabIndex, tabBarHeight);

        if (!LoadTabs(state, buildTabs, ref activeTabIndex)) return;

        using (gui.Node().Expand().Direction(Axis.Vertical).Enter())
        {
            using (gui.StyledNode("tabbar", PillClass).Height(state.TabBarHeight).Direction(Axis.Horizontal)
                       .Gap(spacing).Padding(spacing).Enter())
            {
                for (var i = 0; i < state.Tabs.Count; i++) RenderPillTabButton(gui, state, i, fontSize);
            }

            RenderActiveTabContent(gui, state, null);
        }

        ApplyTabClose(state, onTabClosed);
        activeTabIndex = state.ActiveTabIndex;
    }

    /// <summary>
    /// Rebuilds the tab list, leaving out tabs the user closed (middle-click closure lives in the widget), and
    /// clamps the active index. Returns whether any tab remains.
    /// </summary>
    static bool LoadTabs(TabsState state, Action<TabBuilder> buildTabs, ref int activeTabIndex)
    {
        var builder = new TabBuilder();
        buildTabs(builder);
        state.Tabs = builder.GetTabs();
        state.Tabs.RemoveAll(tab => state.Closed.Contains(tab.Title));

        if (activeTabIndex < 0 || activeTabIndex >= state.Tabs.Count)
            activeTabIndex = state.Tabs.Count > 0 ? 0 : -1;

        state.ActiveTabIndex = activeTabIndex;
        return state.Tabs.Count > 0;
    }

    /// <summary>Closes the tab requested this frame, keeping the active tab on the same page where possible.</summary>
    static void ApplyTabClose(TabsState state, Action<int, string>? onTabClosed)
    {
        if (state.TabToClose is not { } closeRequest) return;

        state.TabToClose = null;
        state.Closed.Add(closeRequest.Title);

        var openCount = state.Tabs.Count - 1;
        if (closeRequest.Index < state.ActiveTabIndex) state.ActiveTabIndex--;
        else if (closeRequest.Index == state.ActiveTabIndex)
            state.ActiveTabIndex = Math.Min(closeRequest.Index, Math.Max(0, openCount - 1));

        onTabClosed?.Invoke(closeRequest.Index, closeRequest.Title);
    }

    /// <summary>Selects on click (not on the close button) and closes on a middle click; render pass only.</summary>
    static void HandleTabPointer(Gui gui, TabsState state, int tabIndex, TabInfo tab)
    {
        var interactable = gui.GetInteractable();
        if (interactable.OnClick() && tab.Enabled
            && !OverTabCloseButton(tab.Closable, gui.CurrentNode.Rect, gui.Input.MousePosition))
            state.ActiveTabIndex = tabIndex;
        if (interactable.OnClick(MouseButton.Middle) && tab.Enabled && tab.Closable)
            state.TabToClose = (tabIndex, tab.Title);
    }

    static void RenderVerticalTabButton(Gui gui, TabsState state, int tabIndex, float tabWidth, float fontSize)
    {
        var tab = state.Tabs[tabIndex];
        var isActive = state.ActiveTabIndex == tabIndex;

        using (TabNode(gui, tab, isActive).Width(tabWidth).Height(36).Padding(8).Direction(Axis.Horizontal).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                HandleTabPointer(gui, state, tabIndex, tab);
                if (isActive) DrawTabMarker(gui, vertical: true);
            }

            using (gui.Node().Expand().Height(36).Enter())
                gui.DrawText(tab.Title, fontSize, centerInRect: false);

            if (tab.Closable) RenderTabCloseButton(gui, state, tabIndex);
        }
    }

    static void RenderPillTabButton(Gui gui, TabsState state, int tabIndex, float fontSize)
    {
        var tab = state.Tabs[tabIndex];
        var isActive = state.ActiveTabIndex == tabIndex;
        var tabWidth = CalculateTabWidth(tab.Title, fontSize, tab.Closable);

        using (TabNode(gui, tab, isActive).Width(tabWidth).Height(state.TabBarHeight - 16).Direction(Axis.Horizontal)
                   .Enter())
        {
            if (gui.Pass == Pass.Pass2Render) HandleTabPointer(gui, state, tabIndex, tab);

            using (gui.Node().Expand().Height(state.TabBarHeight - 16).Enter())
                gui.DrawText(tab.Title, fontSize, centerInRect: true);

            if (tab.Closable) RenderTabCloseButton(gui, state, tabIndex);
        }
    }
}
