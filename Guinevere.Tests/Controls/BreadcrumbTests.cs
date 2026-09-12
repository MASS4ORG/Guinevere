using System.Text.RegularExpressions;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the breadcrumb widget: the node layout it produces, and that only interactive crumbs --
/// links, not the current page -- answer the pointer.
/// </summary>
public class BreadcrumbTests
{
    private const int Width = 400;
    private const int Height = 200;

    private static readonly List<string> NavigationLog = [];

    private static readonly IReadOnlyList<BreadcrumbItem> Items =
    [
        new BreadcrumbItem("Home", () => NavigationLog.Add("home")),
        new BreadcrumbItem("Docs", () => NavigationLog.Add("docs")),
        new BreadcrumbItem("Controls", IsCurrent: true)
    ];

    private static Gui RunFrame(Action<Gui> draw, IInputHandler? input = null)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));

        input ??= NoInput();
        var gui = new TestableGui { Input = input };
        gui.Input = input;
        gui.SetScreenRect(Width, Height);

        gui.Time.Update(0.016);
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        draw(gui);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        draw(gui);
        gui.Render();
        gui.EndFrame();

        return gui;
    }

    private static IInputHandler NoInput()
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

    private static LayoutNode? Crumb(Gui gui, int index)
    {
        LayoutNode? found = null;
        Visit(gui.RootNode!);
        return found;

        void Visit(LayoutNode node)
        {
            if (Regex.IsMatch(node.Id, $@"^breadcrumb/{index}$")) found = node;
            foreach (var child in node.Children) Visit(child);
        }
    }

    private static LayoutNode? Separator(Gui gui, int index)
    {
        LayoutNode? found = null;
        Visit(gui.RootNode!);
        return found;

        void Visit(LayoutNode node)
        {
            if (Regex.IsMatch(node.Id, $@"^breadcrumb/sep/{index}$")) found = node;
            foreach (var child in node.Children) Visit(child);
        }
    }

    private static void Click(Gui gui, IReadOnlyList<BreadcrumbItem> items, int index,
        MouseButton button = MouseButton.Left)
    {
        var crumb = Crumb(gui, index);
        Assert.NotNull(crumb);

        var centre = new Vector2(crumb.Rect.X + crumb.Rect.W * 0.5f, crumb.Rect.Y + crumb.Rect.H * 0.5f);
        gui.Input.MousePosition.Returns(centre);
        gui.Input.IsMouseButtonPressed(button).Returns(true);

        RunFrame(draw: g => g.Breadcrumb(items), input: gui.Input);
    }

    [Fact]
    public void RendersThreeCrumbsAndTwoChevrons()
    {
        var gui = RunFrame(g => g.Breadcrumb(Items));

        var crumbs = 0;
        Visit(gui.RootNode!);
        Assert.Equal(3, crumbs);

        // The breadcrumb container is the only node with crumbs for children.
        int? chevrons = null;
        CountChevrons(gui.RootNode!);
        Assert.Equal(2, chevrons);

        void Visit(LayoutNode node)
        {
            if (Regex.IsMatch(node.Id, @"^breadcrumb/\d+$")) crumbs++;
            foreach (var child in node.Children) Visit(child);
        }

        void CountChevrons(LayoutNode node)
        {
            var crumbsHere = node.Children.Count(child => Regex.IsMatch(child.Id, @"^breadcrumb/\d+$"));
            if (crumbsHere > 0) chevrons = node.Children.Count - crumbsHere;
            foreach (var child in node.Children) CountChevrons(child);
        }
    }

    [Fact]
    public void ClickingAnInteractiveCrumbRunsItsActionOnce()
    {
        NavigationLog.Clear();
        var gui = RunFrame(g => g.Breadcrumb(Items));

        Click(gui, Items, 1);

        Assert.Equal(["docs"], NavigationLog);
    }

    [Fact]
    public void ClickingTheCurrentPageDoesNotRunItsAction()
    {
        NavigationLog.Clear();

        var withCurrentAction = new List<BreadcrumbItem>
        {
            new("Home", () => NavigationLog.Add("home")),
            new("Controls", OnClick: () => NavigationLog.Add("current"), IsCurrent: true)
        };

        var gui = RunFrame(g => g.Breadcrumb(withCurrentAction));
        Click(gui, withCurrentAction, 1);

        Assert.Empty(NavigationLog);
    }

    [Fact]
    public void ClickingAwayFromTheTrailFiresNothing()
    {
        NavigationLog.Clear();
        RunFrame(g => g.Breadcrumb(Items));

        var input = NoInput();
        input.MousePosition.Returns(new Vector2(Width - 1, Height - 1));
        input.IsMouseButtonPressed(MouseButton.Left).Returns(true);
        RunFrame(g => g.Breadcrumb(Items), input);

        Assert.Empty(NavigationLog);
    }

    [Fact]
    public void AnIconWidensTheCrumbToFitIt()
    {
        var withIcon = new List<BreadcrumbItem>
        {
            new("Home", () => NavigationLog.Add("home"), Icon: "📁"),
            new("Docs", IsCurrent: true)
        };
        var plain = new List<BreadcrumbItem>
        {
            new("Home", () => NavigationLog.Add("home")),
            new("Docs", IsCurrent: true)
        };

        var iconGui = RunFrame(g => g.Breadcrumb(withIcon));
        var plainGui = RunFrame(g => g.Breadcrumb(plain));

        Assert.True(Crumb(iconGui, 0)!.Rect.W > Crumb(plainGui, 0)!.Rect.W);
    }

    [Fact]
    public void ClickingAChildBearingChevronOpensAMenuWhoseItemNavigates()
    {
        NavigationLog.Clear();

        var withChildren = new List<BreadcrumbItem>
        {
            new("Home", null, false, null,
            [
                new BreadcrumbItem("Child A", () => NavigationLog.Add("child-a")),
                new BreadcrumbItem("Child B", () => NavigationLog.Add("child-b"))
            ]),
            new("Reports", IsCurrent: true)
        };

        var gui = CreateGui();
        var draw = (Gui g) => g.Breadcrumb(withChildren);

        FrameOn(gui, draw);
        var separator = Separator(gui, 1);
        Assert.NotNull(separator);

        // Frame 1: click the chevron. The menu opens but is hidden this frame (deferred).
        FrameOn(gui, draw, ClickAt(separator.Rect, MouseButton.Left));

        // Frame 2: the menu is visible under the chevron.
        FrameOn(gui, draw);

        // Frame 3: click the first item, which sits one row below the anchor.
        var item = new Rect(separator.Rect.X, separator.Rect.Y + separator.Rect.H, 120, 12);
        FrameOn(gui, draw, ClickAt(item, MouseButton.Left));

        Assert.Equal(["child-a"], NavigationLog);
    }

    private static Gui CreateGui()
    {
        var gui = new TestableGui { Input = NoInput() };
        gui.SetScreenRect(Width, Height);
        return gui;
    }

    private static void FrameOn(Gui gui, Action<Gui> draw, IInputHandler? input = null)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));

        gui.Input = input ?? NoInput();
        gui.Time.Update(0.016);
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        draw(gui);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        draw(gui);
        gui.Render();
        gui.EndFrame();
    }

    private static IInputHandler ClickAt(Rect rect, MouseButton button)
    {
        var input = NoInput();
        var centre = new Vector2(rect.X + rect.W * 0.5f, rect.Y + rect.H * 0.5f);
        input.MousePosition.Returns(centre);
        input.PrevMousePosition.Returns(centre);
        input.IsMouseButtonPressed(button).Returns(true);
        return input;
    }
}