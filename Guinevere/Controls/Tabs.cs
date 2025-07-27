using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    private static readonly Dictionary<string, TabsState> TabsStates = new();

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
        float fontSize = 14,
        float borderRadius = 4,
        bool showBorder = true,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var id = gui.NodeId(filePath, lineNumber);
        var state = GetOrCreateTabsState(id, activeTabIndex, tabBarHeight);

        var builder = new TabBuilder();
        buildTabs(builder);
        state.Tabs = builder.GetTabs();

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
        float fontSize = 14,
        float borderRadius = 4,
        bool showBorder = true,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var temp = activeTabIndex;
        gui.Tabs(ref temp, buildTabs, tabBarHeight, backgroundColor, activeTabColor, inactiveTabColor,
            borderColor, textColor, activeTextColor, fontSize, borderRadius, showBorder, filePath, lineNumber);
        return temp;
    }

    /// <summary>
    /// Creates a simple tab bar without content (for manual content management)
    /// </summary>
    public static void TabBar(this Gui gui, string[] tabTitles, ref int activeTabIndex,
        float height = 32,
        Color? backgroundColor = null,
        Color? activeTabColor = null,
        Color? inactiveTabColor = null,
        Color? borderColor = null,
        Color? textColor = null,
        Color? activeTextColor = null,
        float fontSize = 14,
        float borderRadius = 4,
        bool showBorder = true,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        gui.Tabs(ref activeTabIndex, builder =>
            {
                foreach (var title in tabTitles) builder.Tab(title);
            }, height, backgroundColor, activeTabColor, inactiveTabColor, borderColor,
            textColor, activeTextColor, fontSize, borderRadius, showBorder, filePath, lineNumber);
    }

    // Core implementation helpers
    private static TabsState GetOrCreateTabsState(string id, int initialActiveIndex, float tabBarHeight)
    {
        return TabsStates.TryGetValue(id, out var state)
            ? state
            : TabsStates[id] = new TabsState { ActiveTabIndex = initialActiveIndex, TabBarHeight = tabBarHeight };
    }

    private static float CalculateTabsHeight(TabsState state, float tabBarHeight)
    {
        return tabBarHeight + (state.Tabs.Any(t => t.Content != null) ? 200 : 0);
        // Default content height
    }

    private static void RenderTabBar(Gui gui, TabsState state, Color? backgroundColor,
        Color? activeTabColor, Color? inactiveTabColor, Color? borderColor,
        Color? textColor, Color? activeTextColor, float fontSize, float borderRadius, bool showBorder)
    {
        using (gui.Node().Height(state.TabBarHeight).Direction(Axis.Horizontal).Enter())
        {
            if (gui.Pass == Pass.Pass2Render && showBorder)
            {
                var bgColor = backgroundColor ?? Color.FromArgb(255, 250, 250, 250);
                var borderColorFinal = borderColor ?? Color.FromArgb(255, 200, 200, 200);

                gui.DrawBackgroundRect(bgColor, borderRadius);
                gui.DrawRectBorder(gui.CurrentNode.Rect, borderColorFinal, 1f, borderRadius);
            }

            for (var i = 0; i < state.Tabs.Count; i++)
                RenderTabButton(gui, state, i, activeTabColor, inactiveTabColor,
                    textColor, activeTextColor, fontSize, borderRadius);
        }
    }

    private static void RenderTabButton(Gui gui, TabsState state, int tabIndex,
        Color? activeTabColor, Color? inactiveTabColor, Color? textColor,
        Color? activeTextColor, float fontSize, float borderRadius)
    {
        var tab = state.Tabs[tabIndex];
        var isActive = state.ActiveTabIndex == tabIndex;
        var tabWidth = CalculateTabWidth(tab.Title, fontSize);

        using (gui.Node(tabWidth, state.TabBarHeight).Padding(8).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                // Register as focusable for keyboard navigation
                gui.RegisterFocusable(canReceiveFocus: true, isInteractable: tab.Enabled);
                var interactable = gui.GetInteractable();
                var isHovered = interactable.OnHover();
                var isClicked = interactable.OnClick();
                var rect = gui.CurrentNode.Rect;
                var hasFocus = gui.HasFocus();

                // Draw focus indicator if focused
                if (hasFocus)
                {
                    var focusRect = new Rect(rect.X - 2, rect.Y - 2, rect.W + 4, rect.H + 4);
                    gui.DrawRectBorder(focusRect, Color.FromArgb(255, 100, 149, 237), 2f, borderRadius + 2);
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
                if (activated && tab.Enabled) state.ActiveTabIndex = tabIndex;

                var tabColor = GetTabBackgroundColor(isActive, isHovered, tab.BackgroundColor,
                    activeTabColor, inactiveTabColor);

                if (tabColor.HasValue) gui.DrawBackgroundRect(tabColor.Value, borderRadius);

                // Add active tab indicator
                if (isActive) DrawActiveTabIndicator(gui, activeTabColor);
            }

            var finalTextColor = GetTabTextColor(isActive, tab.Enabled, tab.TextColor,
                activeTextColor, textColor);

            gui.DrawText(tab.Title, fontSize, finalTextColor, centerInRect: true);
        }
    }

    private static void RenderActiveTabContent(Gui gui, TabsState state, Color? backgroundColor,
        Color? borderColor, float borderRadius, bool showBorder)
    {
        if (state.ActiveTabIndex < 0 || state.ActiveTabIndex >= state.Tabs.Count) return;

        var activeTab = state.Tabs[state.ActiveTabIndex];
        if (activeTab.Content == null) return;

        using (gui.Node().Expand().Padding(12).Enter())
        {
            if (gui.Pass == Pass.Pass2Render && showBorder)
            {
                var bgColor = backgroundColor ?? Color.White;
                var borderColorFinal = borderColor ?? Color.FromArgb(255, 200, 200, 200);

                gui.DrawBackgroundRect(bgColor, borderRadius);
                gui.DrawRectBorder(gui.CurrentNode.Rect, borderColorFinal, 1f, borderRadius);
            }

            activeTab.Content();
        }
    }

    // Helper functions
    private static float CalculateTabWidth(string title, float fontSize)
    {
        var font = new SKFont { Size = fontSize };
        font.MeasureText(title, out var textBounds);
        return textBounds.Width + 24; // 24 for padding
    }

    private static Color? GetTabBackgroundColor(bool isActive, bool isHovered, Color? tabColor,
        Color? activeTabColor, Color? inactiveTabColor)
    {
        return tabColor ?? (isActive ? activeTabColor ?? Color.White :
            isHovered ? Color.FromArgb(255, 245, 245, 245) :
            inactiveTabColor);
    }

    private static Color GetTabTextColor(bool isActive, bool enabled, Color? tabTextColor,
        Color? activeTextColor, Color? textColor)
    {
        if (!enabled) return Color.Gray;
        return tabTextColor ?? (isActive ? activeTextColor ?? Color.Black : textColor ?? Color.Gray);
    }

    private static void DrawActiveTabIndicator(Gui gui, Color? activeTabColor)
    {
        var rect = gui.CurrentNode.Rect;
        var indicatorColor = activeTabColor ?? Color.FromArgb(255, 100, 149, 237);
        var indicatorRect = new Rect(rect.X, rect.Y + rect.H - 3, rect.W, 3);
        gui.DrawRect(indicatorRect, indicatorColor);
    }

    /// <summary>
    /// Clears all tabs states (useful for cleanup)
    /// </summary>
    public static void ClearTabsStates(this Gui gui)
    {
        TabsStates.Clear();
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
        float fontSize = 14,
        float borderRadius = 4,
        bool showBorder = true,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var id = gui.NodeId(filePath, lineNumber);
        var state = GetOrCreateTabsState(id, activeTabIndex, 32);

        var builder = new TabBuilder();
        buildTabs(builder);
        state.Tabs = builder.GetTabs();

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
        float fontSize = 14,
        float spacing = 8,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var id = gui.NodeId(filePath, lineNumber);
        var state = GetOrCreateTabsState(id, activeTabIndex, tabBarHeight);

        var builder = new TabBuilder();
        buildTabs(builder);
        state.Tabs = builder.GetTabs();

        if (activeTabIndex < 0 || activeTabIndex >= state.Tabs.Count)
            activeTabIndex = state.Tabs.Count > 0 ? 0 : -1;

        state.ActiveTabIndex = activeTabIndex;

        if (state.Tabs.Count == 0) return;

        using (gui.Node().Expand().Direction(Axis.Vertical).Enter())
        {
            RenderPillTabBar(gui, state, activeTabColor ?? Color.FromArgb(255, 100, 149, 237),
                inactiveTabColor ?? Color.Transparent, textColor, activeTextColor, fontSize, spacing);

            RenderActiveTabContent(gui, state, Color.White, Color.FromArgb(255, 200, 200, 200), 4, true);
        }

        activeTabIndex = state.ActiveTabIndex;
    }

    private static void RenderVerticalTabBar(Gui gui, TabsState state, float tabWidth,
        Color? backgroundColor, Color? activeTabColor, Color? inactiveTabColor,
        Color? borderColor, Color? textColor, Color? activeTextColor, float fontSize,
        float borderRadius, bool showBorder)
    {
        using (gui.Node(tabWidth).Expand().Direction(Axis.Vertical).Enter())
        {
            if (gui.Pass == Pass.Pass2Render && showBorder)
            {
                var bgColor = backgroundColor ?? Color.FromArgb(255, 250, 250, 250);
                var borderColorFinal = borderColor ?? Color.FromArgb(255, 200, 200, 200);

                gui.DrawBackgroundRect(bgColor, borderRadius);
                gui.DrawRectBorder(gui.CurrentNode.Rect, borderColorFinal, 1f, borderRadius);
            }

            for (var i = 0; i < state.Tabs.Count; i++)
                RenderVerticalTabButton(gui, state, i, tabWidth, activeTabColor, inactiveTabColor,
                    textColor, activeTextColor, fontSize, borderRadius);
        }
    }

    private static void RenderVerticalTabButton(Gui gui, TabsState state, int tabIndex, float tabWidth,
        Color? activeTabColor, Color? inactiveTabColor, Color? textColor,
        Color? activeTextColor, float fontSize, float borderRadius)
    {
        var tab = state.Tabs[tabIndex];
        var isActive = state.ActiveTabIndex == tabIndex;

        using (gui.Node(tabWidth, 36).Padding(8).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                var interactable = gui.GetInteractable();
                var isHovered = interactable.OnHover();
                var isClicked = interactable.OnClick();

                if (isClicked && tab.Enabled) state.ActiveTabIndex = tabIndex;

                var tabColor = GetTabBackgroundColor(isActive, isHovered, tab.BackgroundColor,
                    activeTabColor, inactiveTabColor);

                if (tabColor.HasValue) gui.DrawBackgroundRect(tabColor.Value, borderRadius);

                if (isActive)
                {
                    var indicatorColor = activeTabColor ?? Color.FromArgb(255, 100, 149, 237);
                    var rect = gui.CurrentNode.Rect;
                    var indicatorRect = new Rect(rect.X, rect.Y, 3, rect.H);
                    gui.DrawRect(indicatorRect, indicatorColor);
                }
            }

            var finalTextColor = GetTabTextColor(isActive, tab.Enabled, tab.TextColor,
                activeTextColor, textColor);

            gui.DrawText(tab.Title, fontSize, finalTextColor, centerInRect: false);
        }
    }

    private static void RenderPillTabBar(Gui gui, TabsState state, Color activeTabColor,
        Color inactiveTabColor, Color? textColor, Color? activeTextColor, float fontSize, float spacing)
    {
        using (gui.Node().Height(state.TabBarHeight).Direction(Axis.Horizontal).Gap(spacing).Padding(spacing).Enter())
        {
            for (var i = 0; i < state.Tabs.Count; i++)
                RenderPillTabButton(gui, state, i, activeTabColor, inactiveTabColor,
                    textColor, activeTextColor, fontSize);
        }
    }

    private static void RenderPillTabButton(Gui gui, TabsState state, int tabIndex,
        Color activeTabColor, Color inactiveTabColor, Color? textColor,
        Color? activeTextColor, float fontSize)
    {
        var tab = state.Tabs[tabIndex];
        var isActive = state.ActiveTabIndex == tabIndex;
        var tabWidth = CalculateTabWidth(tab.Title, fontSize);

        using (gui.Node(tabWidth, state.TabBarHeight - 16).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                var interactable = gui.GetInteractable();
                var isHovered = interactable.OnHover();
                var isClicked = interactable.OnClick();

                if (isClicked && tab.Enabled) state.ActiveTabIndex = tabIndex;

                var tabColor = isActive ? activeTabColor :
                    isHovered ? Color.FromArgb(100, activeTabColor.R, activeTabColor.G, activeTabColor.B) :
                    inactiveTabColor;

                gui.DrawBackgroundRect(tabColor, (state.TabBarHeight - 16) * 0.5f); // Fully rounded
            }

            var finalTextColor = GetTabTextColor(isActive, tab.Enabled, tab.TextColor,
                activeTextColor, textColor);

            gui.DrawText(tab.Title, fontSize, finalTextColor, centerInRect: true);
        }
    }
}
