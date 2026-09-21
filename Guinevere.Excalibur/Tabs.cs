using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// Creates a tab container that manages multiple tabs with internal state management
    /// </summary>
    public static void Tabs(this Gui gui, ref int activeTabIndex, Action<TabBuilder> buildTabs,
        float tabBarHeight = 32,
        Color? backgroundColor = null,
        Color? activeTabColor = null,
        Color? inactiveTabColor = null,
        Color? borderColor = null,
        Color? textColor = null,
        Color? activeTextColor = null,
        float fontSize = ControlMetrics.FontSize,
        float borderRadius = ControlMetrics.CornerRadius,
        bool showBorder = true,
        string id = "",
        Action<int, string>? onTabClosed = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var stateId = string.IsNullOrEmpty(id) ? gui.NodeId(filePath, lineNumber) : id;
        var state = GetOrCreateTabsState(gui, stateId, activeTabIndex, tabBarHeight);

        var builder = new TabBuilder();
        buildTabs(builder);
        state.Tabs = builder.GetTabs();

        // Middle-click closure lives in the widget: tabs the user closed stay out of the rebuilt list.
        state.Tabs.RemoveAll(tab => state.Closed.Contains(tab.Title));

        // Ensure active tab index is valid
        if (activeTabIndex < 0 || activeTabIndex >= state.Tabs.Count)
            activeTabIndex = state.Tabs.Count > 0 ? 0 : -1;

        state.ActiveTabIndex = activeTabIndex;

        if (state.Tabs.Count == 0) return;

        var totalHeight = CalculateTabsHeight(state, tabBarHeight);

        using (gui.Node().Expand().Height(totalHeight).Direction(Axis.Vertical).Enter())
        {
            RenderTabBar(gui, state, backgroundColor, activeTabColor, inactiveTabColor,
                borderColor, textColor, activeTextColor, fontSize, borderRadius, showBorder);

            RenderActiveTabContent(gui, state, backgroundColor, borderColor, borderRadius, showBorder);
        }

        if (state.TabToClose is { } closeRequest)
        {
            state.TabToClose = null;
            state.Closed.Add(closeRequest.Title);

            var openCount = state.Tabs.Count - 1;
            if (closeRequest.Index < state.ActiveTabIndex) state.ActiveTabIndex--;
            else if (closeRequest.Index == state.ActiveTabIndex)
                state.ActiveTabIndex = Math.Min(closeRequest.Index, Math.Max(0, openCount - 1));

            onTabClosed?.Invoke(closeRequest.Index, closeRequest.Title);
        }

        activeTabIndex = state.ActiveTabIndex;
    }

    /// <summary>
    /// Creates a tab container that returns the active tab index without modifying the input
    /// </summary>
    public static int Tabs(this Gui gui, int activeTabIndex, Action<TabBuilder> buildTabs,
        float tabBarHeight = 32,
        Color? backgroundColor = null,
        Color? activeTabColor = null,
        Color? inactiveTabColor = null,
        Color? borderColor = null,
        Color? textColor = null,
        Color? activeTextColor = null,
        float fontSize = ControlMetrics.FontSize,
        float borderRadius = ControlMetrics.CornerRadius,
        bool showBorder = true,
        string id = "",
        Action<int, string>? onTabClosed = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var temp = activeTabIndex;
        gui.Tabs(ref temp, buildTabs, tabBarHeight, backgroundColor, activeTabColor, inactiveTabColor,
            borderColor, textColor, activeTextColor, fontSize, borderRadius, showBorder, id, onTabClosed,
            filePath, lineNumber);
        return temp;
    }

    /// <summary>
    /// Creates a simple tab bar without content (for manual content management)
    /// </summary>
    public static void TabBar(this Gui gui, string[] tabTitles, ref int activeTabIndex,
        float height = ControlMetrics.FieldHeight,
        Color? backgroundColor = null,
        Color? activeTabColor = null,
        Color? inactiveTabColor = null,
        Color? borderColor = null,
        Color? textColor = null,
        Color? activeTextColor = null,
        float fontSize = ControlMetrics.FontSize,
        float borderRadius = ControlMetrics.CornerRadius,
        bool showBorder = true,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        gui.Tabs(ref activeTabIndex, builder =>
            {
                foreach (var title in tabTitles) builder.Tab(title);
            }, height, backgroundColor, activeTabColor, inactiveTabColor, borderColor,
            textColor, activeTextColor, fontSize, borderRadius, showBorder,
            filePath: filePath, lineNumber: lineNumber);
    }

    // Core implementation helpers
    static TabsState GetOrCreateTabsState(Gui gui, string id, int initialActiveIndex, float tabBarHeight) =>
        gui.ControlState(id,
            () => new TabsState { ActiveTabIndex = initialActiveIndex, TabBarHeight = tabBarHeight });

    static float CalculateTabsHeight(TabsState state, float tabBarHeight)
    {
        return tabBarHeight + (state.Tabs.Any(t => t.Content != null) ? 200 : 0);
        // Default content height
    }

    static void RenderTabBar(Gui gui, TabsState state, Color? backgroundColor,
        Color? activeTabColor, Color? inactiveTabColor, Color? borderColor,
        Color? textColor, Color? activeTextColor, float fontSize, float borderRadius, bool showBorder)
    {
        using (gui.Node().Height(state.TabBarHeight).Direction(Axis.Horizontal).Enter())
        {
            if (gui.Pass == Pass.Pass2Render && showBorder)
            {
                var bgColor = backgroundColor ?? gui.Controls.SurfaceHover;
                var borderColorFinal = borderColor ?? gui.Controls.Border;

                gui.DrawBackgroundRect(bgColor, borderRadius);
                gui.DrawRectBorder(gui.CurrentNode.Rect, borderColorFinal, 1f, borderRadius);
            }

            for (var i = 0; i < state.Tabs.Count; i++)
                RenderTabButton(gui, state, i, activeTabColor, inactiveTabColor,
                    textColor, activeTextColor, fontSize, borderRadius);
        }
    }

    static void RenderTabButton(Gui gui, TabsState state, int tabIndex,
        Color? activeTabColor, Color? inactiveTabColor, Color? textColor,
        Color? activeTextColor, float fontSize, float borderRadius)
    {
        var tab = state.Tabs[tabIndex];
        var isActive = state.ActiveTabIndex == tabIndex;
        var tabWidth = CalculateTabWidth(tab.Title, fontSize, tab.Closable);

        using (gui.Node(tabWidth, state.TabBarHeight).Direction(Axis.Horizontal).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                // Register as focusable for keyboard navigation
                gui.RegisterFocusable(canReceiveFocus: true, isInteractable: tab.Enabled);
                var interactable = gui.GetInteractable();
                var isHovered = interactable.OnHover();
                var isClicked = interactable.OnClick();
                var closed = interactable.OnClick(MouseButton.Middle) && tab.Closable;
                var rect = gui.CurrentNode.Rect;
                var hasFocus = gui.HasFocus();

                // Only the tab surface activates the tab; the "×" close button in the corner is separate.
                var overClose = OverTabCloseButton(tab.Closable, rect, gui.Input.MousePosition);

                // Draw focus indicator if focused
                if (hasFocus)
                {
                    var focusRect = new Rect(rect.X - 2, rect.Y - 2, rect.W + 4, rect.H + 4);
                    gui.DrawRectBorder(focusRect, gui.Controls.Accent, 2f, borderRadius + 2);
                }

                // Keyboard navigation: Left/Right to move, Enter/Space to activate
                var activated = isClicked;
                if (hasFocus)
                {
                    if (gui.Input.IsKeyPressed(KeyboardKey.Left))
                    {
                        var prev = tabIndex - 1;
                        for (var i = prev; i >= 0; i--) if (state.Tabs[i].Enabled) { state.ActiveTabIndex = i; break; }
                    }
                    else if (gui.Input.IsKeyPressed(KeyboardKey.Right))
                    {
                        var next = tabIndex + 1;
                        for (var i = next; i < state.Tabs.Count; i++) if (state.Tabs[i].Enabled) { state.ActiveTabIndex = i; break; }
                    }
                    else if (gui.Input.IsKeyPressed(KeyboardKey.Space) || gui.Input.IsKeyPressed(KeyboardKey.Enter))
                    {
                        activated = true;
                    }
                }
                if (activated && tab.Enabled && !overClose) state.ActiveTabIndex = tabIndex;
                if (closed && tab.Enabled) state.TabToClose = (tabIndex, tab.Title);

                var tabColor = GetTabBackgroundColor(gui, isActive, isHovered, tab.BackgroundColor,
                    activeTabColor, inactiveTabColor);

                if (tabColor.HasValue) gui.DrawBackgroundRect(tabColor.Value, borderRadius);

                // Add active tab indicator
                if (isActive) DrawActiveTabIndicator(gui, activeTabColor);
            }

            var finalTextColor = GetTabTextColor(gui, isActive, tab.Enabled, tab.TextColor,
                activeTextColor, textColor);

            using (gui.Node().Expand().Height(state.TabBarHeight).Enter())
                gui.DrawText(tab.Title, fontSize, finalTextColor, centerInRect: true);

            if (tab.Closable) RenderTabCloseButton(gui, state, tabIndex);
        }
    }

    static void RenderActiveTabContent(Gui gui, TabsState state, Color? backgroundColor,
        Color? borderColor, float borderRadius, bool showBorder)
    {
        if (state.ActiveTabIndex < 0 || state.ActiveTabIndex >= state.Tabs.Count) return;

        var activeTab = state.Tabs[state.ActiveTabIndex];
        if (activeTab.Content == null) return;

        using (gui.Node().Expand().Padding(12).Enter())
        {
            if (gui.Pass == Pass.Pass2Render && showBorder)
            {
                var bgColor = backgroundColor ?? gui.Controls.Popup;
                var borderColorFinal = borderColor ?? gui.Controls.Border;

                gui.DrawBackgroundRect(bgColor, borderRadius);
                gui.DrawRectBorder(gui.CurrentNode.Rect, borderColorFinal, 1f, borderRadius);
            }

            activeTab.Content();
        }
    }

    // Helper functions
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

        using (gui.Node(TabCloseButtonSize, TabCloseButtonSize).Enter())
        {
            if (gui.Pass != Pass.Pass2Render) return;

            var interactable = gui.GetInteractable();
            var rect = gui.CurrentNode.Rect;
            var hovered = interactable.OnHover();
            var clicked = interactable.OnClick();

            if (hovered || clicked)
                gui.DrawBackgroundRect(gui.Controls.SurfaceHover, TabCloseButtonSize * 0.5f);

            var markColor = hovered || clicked ? gui.Controls.Text : gui.Controls.TextDim;

            var font = new SKFont { Size = 12f };
            font.MeasureText("×", out var bounds);
            var pos = new Vector2(rect.X + (rect.W - bounds.Width) * 0.5f,
                rect.Y + (rect.H + bounds.Height) * 0.5f);
            gui.CurrentNode.DrawList.Add(new Text("×", pos, font,
                new SKPaint { Color = markColor, IsAntialias = true }));

            if (clicked && tab.Enabled) state.TabToClose = (tabIndex, tab.Title);
        }
    }

    static Color? GetTabBackgroundColor(Gui gui, bool isActive, bool isHovered, Color? tabColor,
        Color? activeTabColor, Color? inactiveTabColor)
    {
        return tabColor ?? (isActive ? activeTabColor ?? gui.Controls.Surface :
            isHovered ? gui.Controls.SurfaceHover :
            inactiveTabColor);
    }

    static Color GetTabTextColor(Gui gui, bool isActive, bool enabled, Color? tabTextColor,
        Color? activeTextColor, Color? textColor)
    {
        if (!enabled) return gui.Controls.TextDim;
        return tabTextColor ?? (isActive ? activeTextColor ?? gui.Controls.Text : textColor ?? gui.Controls.TextDim);
    }

    static void DrawActiveTabIndicator(Gui gui, Color? activeTabColor)
    {
        var rect = gui.CurrentNode.Rect;
        var indicatorColor = activeTabColor ?? gui.Controls.Accent;
        var indicatorRect = new Rect(rect.X, rect.Y + rect.H - 3, rect.W, 3);
        gui.DrawRect(indicatorRect, indicatorColor);
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
    /// <summary>
    /// Creates vertical tabs (tabs on the side)
    /// </summary>
    public static void VerticalTabs(this Gui gui, ref int activeTabIndex, Action<TabBuilder> buildTabs,
        float tabWidth = 120,
        Color? backgroundColor = null,
        Color? activeTabColor = null,
        Color? inactiveTabColor = null,
        Color? borderColor = null,
        Color? textColor = null,
        Color? activeTextColor = null,
        float fontSize = ControlMetrics.FontSize,
        float borderRadius = ControlMetrics.CornerRadius,
        bool showBorder = true,
        string id = "",
        Action<int, string>? onTabClosed = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var stateId = string.IsNullOrEmpty(id) ? gui.NodeId(filePath, lineNumber) : id;
        var state = GetOrCreateTabsState(gui, stateId, activeTabIndex, 32);

        var builder = new TabBuilder();
        buildTabs(builder);
        state.Tabs = builder.GetTabs();
        state.Tabs.RemoveAll(tab => state.Closed.Contains(tab.Title));

        if (activeTabIndex < 0 || activeTabIndex >= state.Tabs.Count)
            activeTabIndex = state.Tabs.Count > 0 ? 0 : -1;

        state.ActiveTabIndex = activeTabIndex;

        if (state.Tabs.Count == 0) return;

        using (gui.Node().Expand().Direction(Axis.Horizontal).Enter())
        {
            RenderVerticalTabBar(gui, state, tabWidth, backgroundColor, activeTabColor,
                inactiveTabColor, borderColor, textColor, activeTextColor, fontSize, borderRadius, showBorder);

            RenderActiveTabContent(gui, state, backgroundColor, borderColor, borderRadius, showBorder);
        }

        if (state.TabToClose is { } closeRequest)
        {
            state.TabToClose = null;
            state.Closed.Add(closeRequest.Title);

            var openCount = state.Tabs.Count - 1;
            if (closeRequest.Index < state.ActiveTabIndex) state.ActiveTabIndex--;
            else if (closeRequest.Index == state.ActiveTabIndex)
                state.ActiveTabIndex = Math.Min(closeRequest.Index, Math.Max(0, openCount - 1));

            onTabClosed?.Invoke(closeRequest.Index, closeRequest.Title);
        }

        activeTabIndex = state.ActiveTabIndex;
    }

    /// <summary>
    /// Creates pill-style tabs (rounded tabs)
    /// </summary>
    public static void PillTabs(this Gui gui, ref int activeTabIndex, Action<TabBuilder> buildTabs,
        float tabBarHeight = 40,
        Color? activeTabColor = null,
        Color? inactiveTabColor = null,
        Color? textColor = null,
        Color? activeTextColor = null,
        float fontSize = ControlMetrics.FontSize,
        float spacing = ControlMetrics.Spacing,
        string id = "",
        Action<int, string>? onTabClosed = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var stateId = string.IsNullOrEmpty(id) ? gui.NodeId(filePath, lineNumber) : id;
        var state = GetOrCreateTabsState(gui, stateId, activeTabIndex, tabBarHeight);

        var builder = new TabBuilder();
        buildTabs(builder);
        state.Tabs = builder.GetTabs();
        state.Tabs.RemoveAll(tab => state.Closed.Contains(tab.Title));

        if (activeTabIndex < 0 || activeTabIndex >= state.Tabs.Count)
            activeTabIndex = state.Tabs.Count > 0 ? 0 : -1;

        state.ActiveTabIndex = activeTabIndex;

        if (state.Tabs.Count == 0) return;

        using (gui.Node().Expand().Direction(Axis.Vertical).Enter())
        {
            RenderPillTabBar(gui, state, activeTabColor ?? gui.Controls.Selected,
                inactiveTabColor ?? Color.Transparent, textColor, activeTextColor, fontSize, spacing);

            RenderActiveTabContent(gui, state, gui.Controls.Popup, gui.Controls.Border, 4, true);
        }

        if (state.TabToClose is { } closeRequest)
        {
            state.TabToClose = null;
            state.Closed.Add(closeRequest.Title);

            var openCount = state.Tabs.Count - 1;
            if (closeRequest.Index < state.ActiveTabIndex) state.ActiveTabIndex--;
            else if (closeRequest.Index == state.ActiveTabIndex)
                state.ActiveTabIndex = Math.Min(closeRequest.Index, Math.Max(0, openCount - 1));

            onTabClosed?.Invoke(closeRequest.Index, closeRequest.Title);
        }

        activeTabIndex = state.ActiveTabIndex;
    }

    static void RenderVerticalTabBar(Gui gui, TabsState state, float tabWidth,
        Color? backgroundColor, Color? activeTabColor, Color? inactiveTabColor,
        Color? borderColor, Color? textColor, Color? activeTextColor, float fontSize,
        float borderRadius, bool showBorder)
    {
        using (gui.Node(tabWidth).Expand().Direction(Axis.Vertical).Enter())
        {
            if (gui.Pass == Pass.Pass2Render && showBorder)
            {
                var bgColor = backgroundColor ?? gui.Controls.SurfaceHover;
                var borderColorFinal = borderColor ?? gui.Controls.Border;

                gui.DrawBackgroundRect(bgColor, borderRadius);
                gui.DrawRectBorder(gui.CurrentNode.Rect, borderColorFinal, 1f, borderRadius);
            }

            for (var i = 0; i < state.Tabs.Count; i++)
                RenderVerticalTabButton(gui, state, i, tabWidth, activeTabColor, inactiveTabColor,
                    textColor, activeTextColor, fontSize, borderRadius);
        }
    }

    static void RenderVerticalTabButton(Gui gui, TabsState state, int tabIndex, float tabWidth,
        Color? activeTabColor, Color? inactiveTabColor, Color? textColor,
        Color? activeTextColor, float fontSize, float borderRadius)
    {
        var tab = state.Tabs[tabIndex];
        var isActive = state.ActiveTabIndex == tabIndex;

        using (gui.Node(tabWidth, 36).Padding(8).Direction(Axis.Horizontal).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                var interactable = gui.GetInteractable();
                var isHovered = interactable.OnHover();
                var isClicked = interactable.OnClick();
                var rect = gui.CurrentNode.Rect;

                if (isClicked && tab.Enabled && !OverTabCloseButton(tab.Closable, rect, gui.Input.MousePosition))
                    state.ActiveTabIndex = tabIndex;
                if (interactable.OnClick(MouseButton.Middle) && tab.Enabled && tab.Closable)
                    state.TabToClose = (tabIndex, tab.Title);

                var tabColor = GetTabBackgroundColor(gui, isActive, isHovered, tab.BackgroundColor,
                    activeTabColor, inactiveTabColor);

                if (tabColor.HasValue) gui.DrawBackgroundRect(tabColor.Value, borderRadius);

                if (isActive)
                {
                    var indicatorColor = activeTabColor ?? gui.Controls.Accent;
                    var indicatorRect = new Rect(rect.X, rect.Y, 3, rect.H);
                    gui.DrawRect(indicatorRect, indicatorColor);
                }
            }

            var finalTextColor = GetTabTextColor(gui, isActive, tab.Enabled, tab.TextColor,
                activeTextColor, textColor);

            using (gui.Node().Expand().Height(36).Enter())
                gui.DrawText(tab.Title, fontSize, finalTextColor, centerInRect: false);

            if (tab.Closable) RenderTabCloseButton(gui, state, tabIndex);
        }
    }

    static void RenderPillTabBar(Gui gui, TabsState state, Color activeTabColor,
        Color inactiveTabColor, Color? textColor, Color? activeTextColor, float fontSize, float spacing)
    {
        using (gui.Node().Height(state.TabBarHeight).Direction(Axis.Horizontal).Gap(spacing).Padding(spacing).Enter())
        {
            for (var i = 0; i < state.Tabs.Count; i++)
                RenderPillTabButton(gui, state, i, activeTabColor, inactiveTabColor,
                    textColor, activeTextColor, fontSize);
        }
    }

    static void RenderPillTabButton(Gui gui, TabsState state, int tabIndex,
        Color activeTabColor, Color inactiveTabColor, Color? textColor,
        Color? activeTextColor, float fontSize)
    {
        var tab = state.Tabs[tabIndex];
        var isActive = state.ActiveTabIndex == tabIndex;
        var tabWidth = CalculateTabWidth(tab.Title, fontSize, tab.Closable);

        using (gui.Node(tabWidth, state.TabBarHeight - 16).Direction(Axis.Horizontal).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                var interactable = gui.GetInteractable();
                var isHovered = interactable.OnHover();
                var isClicked = interactable.OnClick();
                var rect = gui.CurrentNode.Rect;

                if (isClicked && tab.Enabled && !OverTabCloseButton(tab.Closable, rect, gui.Input.MousePosition))
                    state.ActiveTabIndex = tabIndex;
                if (interactable.OnClick(MouseButton.Middle) && tab.Enabled && tab.Closable)
                    state.TabToClose = (tabIndex, tab.Title);

                var tabColor = isActive ? activeTabColor :
                    isHovered ? Color.FromArgb(100, activeTabColor.R, activeTabColor.G, activeTabColor.B) :
                    inactiveTabColor;

                gui.DrawBackgroundRect(tabColor, (state.TabBarHeight - 16) * 0.5f); // Fully rounded
            }

            var finalTextColor = GetTabTextColor(gui, isActive, tab.Enabled, tab.TextColor,
                activeTextColor, textColor);

            using (gui.Node().Expand().Height(state.TabBarHeight - 16).Enter())
                gui.DrawText(tab.Title, fontSize, finalTextColor, centerInRect: true);

            if (tab.Closable) RenderTabCloseButton(gui, state, tabIndex);
        }
    }
}
