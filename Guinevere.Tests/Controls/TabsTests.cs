using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the tab bar's middle-click close as an opt-in widget behavior: closed titles are added to
/// the widget's own state, so the rebuilt tab list drops them on the following frames. Tabs are not
/// closable unless requested. Also covers the caller-facing close notification and the active-tab
/// write-back after a closure.
/// </summary>
public class TabsTests
{
    const int Width = 400;
    const int Height = 200;

    static Gui CreateGui()
    {
        var gui = new TestableGui { Input = NoInput() };
        gui.SetScreenRect(Width, Height);
        return gui;
    }

    static readonly (string Title, bool Closable)[] DefaultTabs =
        [("A", true), ("B", true)];

    static void Frame(Gui gui, ref int activeTab, List<(int, string)> closed,
        IInputHandler? input = null, (string Title, bool Closable)[]? specs = null)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));

        gui.Input = input ?? NoInput();
        gui.Time.Update(0.016);
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        Tabs(gui, ref activeTab, closed, specs ?? DefaultTabs);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        Tabs(gui, ref activeTab, closed, specs ?? DefaultTabs);
        gui.Render();
        gui.EndFrame();
    }

    static void Tabs(Gui gui, ref int activeTab, List<(int, string)> closed,
        (string Title, bool Closable)[] specs)
    {
        gui.Tabs(ref activeTab, tabs =>
        {
            foreach (var (title, closable) in specs)
                tabs.Tab(title, closable: closable);
        }, onTabClosed: (index, title) => closed.Add((index, title)));
    }

    static IInputHandler NoInput()
    {
        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(-100, -100));
        input.PrevMousePosition.Returns(new Vector2(-100, -100));
        input.MouseDelta.Returns(Vector2.Zero);
        input.MouseWheelDelta.Returns(0f);
        input.IsMouseButtonPressed(Arg.Any<MouseButton>()).Returns(false);
        input.IsMouseButtonDown(Arg.Any<MouseButton>()).Returns(false);
        input.IsAnyKeyDown.Returns(false);
        input.IsKeyPressed(Arg.Any<KeyboardKey>()).Returns(false);
        return input;
    }

    static IInputHandler MouseAt(LayoutNode tab, MouseButton button)
    {
        var input = NoInput();
        var center = new Vector2(tab.Rect.X + tab.Rect.W * 0.5f, tab.Rect.Y + tab.Rect.H * 0.5f);
        input.MousePosition.Returns(center);
        input.PrevMousePosition.Returns(center);
        input.IsMouseButtonPressed(button).Returns(true);
        return input;
    }

    /// <summary>
    /// The bar is the node whose only children are the tab buttons: one row tall and each only as
    /// wide as its label. The container node's single child is the bar itself, which is full width,
    /// so the width check keeps the two apart once a closure leaves exactly one tab.
    /// </summary>
    static LayoutNode FindTabBar(LayoutNode root, int tabCount)
    {
        LayoutNode? found = null;
        Visit(root);
        return found!;

        void Visit(LayoutNode node)
        {
            if (found == null && node.Children.Count == tabCount &&
                node.Children.All(child =>
                    MathF.Abs(child.Rect.H - 32f) < 0.05f && child.Rect.W < 80f))
                found = node;

            foreach (var child in node.Children) Visit(child);
        }
    }

    [Fact]
    public void MiddleClickingAClosableTabClosesIt()
    {
        var gui = CreateGui();
        var closed = new List<(int, string)>();
        var activeTab = 0;

        Frame(gui, ref activeTab, closed);
        var bar = FindTabBar(gui.RootNode!, 2);
        Frame(gui, ref activeTab, closed, MouseAt(bar.Children[0], MouseButton.Middle));

        var (index, title) = Assert.Single(closed);
        Assert.Equal(0, index);
        Assert.Equal("A", title);
        Assert.Equal(0, activeTab);

        Frame(gui, ref activeTab, closed);
        Assert.Single(FindTabBar(gui.RootNode!, 1).Children);
    }

    [Fact]
    public void TabsAreNotClosableByDefault_SoMiddleClickSurvives()
    {
        var gui = CreateGui();
        var closed = new List<(int, string)>();
        var activeTab = 0;

        InlineFrame(gui, ref activeTab, closed);
        var bar = FindTabBar(gui.RootNode!, 2);
        InlineFrame(gui, ref activeTab, closed, MouseAt(bar.Children[0], MouseButton.Middle));

        Assert.Empty(closed);
        Assert.Equal(0, activeTab);

        InlineFrame(gui, ref activeTab, closed);
        Assert.Equal(2, FindTabBar(gui.RootNode!, 2).Children.Count);

        void InlineFrame(Gui g, ref int active, List<(int, string)> closedList,
            IInputHandler? input = null)
        {
            using var surface = SKSurface.Create(new SKImageInfo(Width, Height));

            g.Input = input ?? NoInput();
            g.Time.Update(0.016);
            g.SetStage(Pass.Pass1Build);
            g.BeginFrame(surface.Canvas);
            g.Tabs(ref active, tabs =>
            {
                tabs.Tab("A");
                tabs.Tab("B");
            }, onTabClosed: (i, t) => closedList.Add((i, t)));
            g.CalculateLayout();
            g.SetStage(Pass.Pass2Render);
            g.Tabs(ref active, tabs =>
            {
                tabs.Tab("A");
                tabs.Tab("B");
            }, onTabClosed: (i, t) => closedList.Add((i, t)));
            g.Render();
            g.EndFrame();
        }
    }

    [Fact]
    public void ClosingTheActiveTabMovesSelectionToTheRemainingOne()
    {
        var gui = CreateGui();
        var closed = new List<(int, string)>();
        var activeTab = 0;

        Frame(gui, ref activeTab, closed);
        var bar = FindTabBar(gui.RootNode!, 2);
        Frame(gui, ref activeTab, closed, MouseAt(bar.Children[1], MouseButton.Left));
        Assert.Equal(1, activeTab);

        Frame(gui, ref activeTab, closed, MouseAt(bar.Children[1], MouseButton.Middle));

        var (index, title) = Assert.Single(closed);
        Assert.Equal(1, index);
        Assert.Equal("B", title);
        Assert.Equal(0, activeTab);

        Frame(gui, ref activeTab, closed);
        Assert.Single(FindTabBar(gui.RootNode!, 1).Children);
        Assert.Equal(0, activeTab);
    }

    [Fact]
    public void LeftClickingAnotherTabActivatesItWithoutClosing()
    {
        var gui = CreateGui();
        var closed = new List<(int, string)>();
        var activeTab = 0;

        Frame(gui, ref activeTab, closed);
        var bar = FindTabBar(gui.RootNode!, 2);
        Frame(gui, ref activeTab, closed, MouseAt(bar.Children[1], MouseButton.Left));

        Assert.Empty(closed);
        Assert.Equal(1, activeTab);
    }

    [Fact]
    public void ClickingTheCloseButtonClosesTheTab()
    {
        var gui = CreateGui();
        var closed = new List<(int, string)>();
        var activeTab = 0;

        Frame(gui, ref activeTab, closed);
        var tab = FindTabBar(gui.RootNode!, 2).Children[0];

        // The "×" sits at the tab's right edge, roughly 60% of the way across its size.
        var closeCentre = new Vector2(tab.Rect.X + tab.Rect.W - 13f, tab.Rect.Y + tab.Rect.H * 0.5f);
        var input = NoInput();
        input.MousePosition.Returns(closeCentre);
        input.PrevMousePosition.Returns(closeCentre);
        input.IsMouseButtonPressed(MouseButton.Left).Returns(true);
        Frame(gui, ref activeTab, closed, input);

        var (index, title) = Assert.Single(closed);
        Assert.Equal(0, index);
        Assert.Equal("A", title);
        Assert.Equal(0, activeTab);

        Frame(gui, ref activeTab, closed);
        Assert.Single(FindTabBar(gui.RootNode!, 1).Children);
    }

    [Fact]
    public void NonClosableTabSurvivesAMiddleClick()
    {
        var gui = CreateGui();
        var closed = new List<(int, string)>();
        var activeTab = 0;

        Frame(gui, ref activeTab, closed, null, [("A", false), ("B", true)]);
        var bar = FindTabBar(gui.RootNode!, 2);
        Frame(gui, ref activeTab, closed, MouseAt(bar.Children[0], MouseButton.Middle), [("A", false), ("B", true)]);

        Assert.Empty(closed);
        Assert.Equal(0, activeTab);

        Frame(gui, ref activeTab, closed, null, [("A", false), ("B", true)]);
        Assert.Equal(2, FindTabBar(gui.RootNode!, 2).Children.Count);
    }
}
