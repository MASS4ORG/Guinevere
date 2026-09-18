using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the menu bar as a real widget: titles open their dropdown on click, hover switches the
/// open menu, items trigger their action and close the menu, check items toggle their value, disabled
/// items stay inert, submenus cascade on hover, and Escape / an outside click dismiss the menu.
/// </summary>
public class MenuBarTests
{
    const int Width = 600;
    const int Height = 400;
    const float BarHeight = 30f;

    static Gui CreateGui()
    {
        var gui = new TestableGui { Input = NoInput() };
        gui.SetScreenRect(Width, Height);
        return gui;
    }

    static void Frame(Gui gui, List<string> log, List<bool>? toggle = null, IInputHandler? input = null)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));

        gui.Input = input ?? NoInput();
        gui.Time.Update(0.016);
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        Menus(gui, log, toggle);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        Menus(gui, log, toggle);
        gui.Render();
        gui.EndFrame();
    }

    static void Menus(Gui gui, List<string> log, List<bool>? toggle = null)
    {
        gui.MenuBar(bar =>
        {
            bar.Menu("File", file =>
            {
                file.Item("Open", () => log.Add("open"), "Ctrl+O");
                file.Item("Exit", () => log.Add("exit"));
                file.Separator();
            });

            bar.Menu("Edit", edit =>
            {
                edit.Item("Copy", () => log.Add("copy"), "Ctrl+C");
                if (toggle is not null)
                    edit.CheckItem("Mode", () => toggle[0], value => toggle[0] = value);
                edit.Separator();
                edit.Item("Bold", () => log.Add("bold"), enabled: false);
                edit.Submenu("Zoom", zoom => zoom.Item("Zoom In", () => log.Add("zoom-in")));
            });
        });
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

    static IInputHandler At(Vector2 position, bool pressed = true)
    {
        var input = NoInput();
        input.MousePosition.Returns(position);
        input.PrevMousePosition.Returns(position);
        if (pressed) input.IsMouseButtonPressed(MouseButton.Left).Returns(true);
        return input;
    }

    /// <summary>The bar node is the only node whose children are all ~30px tall titles.</summary>
    static LayoutNode FindBar(LayoutNode root)
    {
        LayoutNode? found = null;
        Visit(root);
        return found!;

        void Visit(LayoutNode node)
        {
            if (found == null && node.Children.Count == 2 &&
                node.Children.All(child => MathF.Abs(child.Rect.H - BarHeight) < 0.05f && child.Rect.W < 100f))
                found = node;

            foreach (var child in node.Children) Visit(child);
        }
    }

    static IEnumerable<LayoutNode> MenuNodes(LayoutNode node)
    {
        if (node.Id.StartsWith("/menubar/", StringComparison.Ordinal)) yield return node;

        foreach (var child in node.Children)
            foreach (var descendant in MenuNodes(child))
                yield return descendant;
    }

    /// <summary>
    /// A dropdown must take part in the layout pass of the frame it appears in. Opening it from the
    /// render pass alone created rows the layout never saw, and their labels flashed at the window's
    /// origin for one frame before settling.
    /// </summary>
    [Fact]
    public void AnOpeningMenuNeverDrawsBeforeItIsLaidOut()
    {
        var gui = CreateGui();
        var log = new List<string>();

        Frame(gui, log);
        var title = FindBar(gui.RootNode!).Children[0].Rect;

        // The frame the click lands on: whatever exists must already have a rect.
        Frame(gui, log, input: At(new Vector2(title.X + 5, title.Y + 5)));
        foreach (var node in MenuNodes(gui.RootNode!))
            Assert.True(node.Rect is { W: > 0, H: > 0 },
                $"{node.Id} was created after layout and would draw at the origin.");

        // And by the next frame the menu is there, under its title.
        Frame(gui, log);
        var rows = MenuNodes(gui.RootNode!).Where(node => node.Id.Contains("/i", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(rows);
        foreach (var row in rows)
            Assert.True(row.Rect.Y >= title.Y + BarHeight - 0.5f,
                $"Menu row {row.Id} drew at {row.Rect}, above the bar it drops from.");
    }

    [Fact]
    public void TitleTextIsMeasuredAndDrawsInsideEachTitle()
    {
        var gui = CreateGui();
        var log = new List<string>();

        Frame(gui, log);
        var bar = FindBar(gui.RootNode!);
        Assert.NotEmpty(bar.Children);

        // The text node must be built during Pass1 layout, not only in Pass2 — otherwise its rect is
        // (0,0,0,0) and the glyphs are drawn at the canvas origin instead of inside the title.
        foreach (var title in bar.Children)
        {
            var text = Assert.Single(title.Children);
            var r = title.Rect;
            var t = text.Rect;
            Assert.True(t is { W: > 0, H: > 0 }, "Title text node must be measured during Pass1.");
            Assert.True(r.X <= t.X && r.Y <= t.Y && t.X + t.W <= r.X + r.W && t.Y + t.H <= r.Y + r.H,
                $"Title text must render inside the title rect (title={r}, text={t}).");
        }
    }

    [Fact]
    public void ClickingATitleOpensItsMenu()
    {
        var gui = CreateGui();
        var log = new List<string>();

        Frame(gui, log);
        var bar = FindBar(gui.RootNode!);

        Frame(gui, log, input: At(bar.Children[1].Center));
        Frame(gui, log);

        Assert.NotNull(gui.RootNode!.FindChildById("/menubar/Edit/v0"));
        Assert.Null(gui.RootNode!.FindChildById("/menubar/File/v0"));
    }

    [Fact]
    public void ClickingAMenuItemRunsItsActionAndClosesTheMenu()
    {
        var gui = CreateGui();
        var log = new List<string>();

        Frame(gui, log);
        var bar = FindBar(gui.RootNode!);
        Frame(gui, log, input: At(bar.Children[1].Center));
        Frame(gui, log);
        var group = gui.RootNode!.FindChildById("/menubar/Edit/v0");
        Assert.NotNull(group);

        Frame(gui, log, input: At(group.Children[0].Center));
        Frame(gui, log);

        Assert.Equal("copy", Assert.Single(log));
        Assert.Null(gui.RootNode!.FindChildById("/menubar/Edit/v0"));

        // Reopening works and runs the action again.
        Frame(gui, log, input: At(bar.Children[1].Center));
        Frame(gui, log);
        var reopened = gui.RootNode!.FindChildById("/menubar/Edit/v0");
        Assert.NotNull(reopened);
        Frame(gui, log, input: At(reopened.Children[0].Center));

        Assert.Equal(2, log.Count);
        Assert.All(log, entry => Assert.Equal("copy", entry));
    }

    [Fact]
    public void HoveringAnotherTitleSwitchesTheOpenMenu()
    {
        var gui = CreateGui();
        var log = new List<string>();

        Frame(gui, log);
        var bar = FindBar(gui.RootNode!);
        Frame(gui, log, input: At(bar.Children[0].Center));
        Frame(gui, log);

        // Hover (without pressing) the second title: the open menu follows the pointer.
        Frame(gui, log, input: At(bar.Children[1].Center, pressed: false));
        Frame(gui, log);

        var switched = gui.RootNode!.FindChildById("/menubar/Edit/v0");
        Assert.NotNull(switched);
        Assert.Equal(bar.Children[1].Rect.X, switched.Rect.X, 1f);
    }

    [Fact]
    public void ClickingACheckItemTogglesItsValueBackAndForth()
    {
        var gui = CreateGui();
        var log = new List<string>();
        var toggle = new List<bool> { false };

        Frame(gui, log);
        var bar = FindBar(gui.RootNode!);
        Frame(gui, log, toggle, At(bar.Children[1].Center));
        Frame(gui, log, toggle);
        var group = gui.RootNode!.FindChildById("/menubar/Edit/v0");
        Assert.NotNull(group);

        Frame(gui, log, toggle, At(group.Children[1].Center));
        Assert.True(toggle[0]);

        Frame(gui, log, toggle, At(bar.Children[1].Center));
        Frame(gui, log, toggle);
        var reopened = gui.RootNode!.FindChildById("/menubar/Edit/v0");
        Assert.NotNull(reopened);
        Frame(gui, log, toggle, At(reopened.Children[1].Center));

        Assert.False(toggle[0]);
        Assert.Empty(log);
    }

    [Fact]
    public void DisabledItemNeverRunsItsAction()
    {
        var gui = CreateGui();
        var log = new List<string>();

        Frame(gui, log);
        var bar = FindBar(gui.RootNode!);
        Frame(gui, log, input: At(bar.Children[1].Center));
        Frame(gui, log);
        var group = gui.RootNode!.FindChildById("/menubar/Edit/v0");
        Assert.NotNull(group);

        // Without a toggle the rows are [Copy(0), Separator(1), Bold(2), Zoom(3)].
        Frame(gui, log, input: At(group.Children[2].Center));
        Frame(gui, log);

        Assert.Empty(log);
        Assert.NotNull(gui.RootNode!.FindChildById("/menubar/Edit/v0"));
    }

    [Fact]
    public void HoveringASubmenuRowCascadesItOpen()
    {
        var gui = CreateGui();
        var log = new List<string>();

        Frame(gui, log);
        var bar = FindBar(gui.RootNode!);
        Frame(gui, log, input: At(bar.Children[1].Center));
        Frame(gui, log);
        var group = gui.RootNode!.FindChildById("/menubar/Edit/v0");
        Assert.NotNull(group);

        // Without a toggle the "Mode" check item is skipped, so the rows are
        // [Copy(0), Separator(1), Bold(2), Zoom(3)].
        Assert.Collection(group.Children,
            n => Assert.EndsWith("/i0", n.Id),
            n => Assert.EndsWith("/s1", n.Id),
            n => Assert.EndsWith("/i2", n.Id),
            n => Assert.EndsWith("/i3", n.Id));

        Frame(gui, log, input: At(group.Children[3].Center, pressed: false));

        var submenu = gui.RootNode!.FindChildById("/menubar/Edit/Zoom/v1");
        Assert.NotNull(submenu);
        Assert.Equal(group.Rect.W, submenu.Rect.X - group.Rect.X, 1f);
    }

    [Fact]
    public void ClickingOutsideClosesTheMenu()
    {
        var gui = CreateGui();
        var log = new List<string>();

        Frame(gui, log);
        var bar = FindBar(gui.RootNode!);
        Frame(gui, log, input: At(bar.Children[1].Center));
        Frame(gui, log);
        var group = gui.RootNode!.FindChildById("/menubar/Edit/v0");
        Assert.NotNull(group);

        Frame(gui, log, input: At(new Vector2(Width - 10, Height - 10)));
        Frame(gui, log);

        Assert.Null(gui.RootNode!.FindChildById("/menubar/Edit/v0"));

        // A stray click where the Copy row used to be must not trigger its action.
        Frame(gui, log, input: At(group.Children[0].Center));
        Assert.Empty(log);
    }

    [Fact]
    public void EscapeClosesTheMenuAndKeyboardNavigates()
    {
        var gui = CreateGui();
        var log = new List<string>();

        Frame(gui, log);
        var bar = FindBar(gui.RootNode!);
        Frame(gui, log, input: At(bar.Children[1].Center));
        Frame(gui, log);

        // Down selects the first enabled row, Enter activates it.
        var keyboard = NoInput();
        keyboard.IsKeyPressed(KeyboardKey.Down).Returns(true);
        Frame(gui, log, input: keyboard);
        var enter = NoInput();
        enter.IsKeyPressed(KeyboardKey.Enter).Returns(true);
        Frame(gui, log, input: enter);
        Frame(gui, log);

        Assert.Equal("copy", Assert.Single(log));
        Assert.Null(gui.RootNode!.FindChildById("/menubar/Edit/v0"));

        // Reopen, then Escape dismisses without an action.
        Frame(gui, log, input: At(bar.Children[1].Center));
        Frame(gui, log);
        Assert.NotNull(gui.RootNode!.FindChildById("/menubar/Edit/v0"));

        var escape = NoInput();
        escape.IsKeyPressed(KeyboardKey.Escape).Returns(true);
        Frame(gui, log, input: escape);
        Frame(gui, log);

        Assert.Null(gui.RootNode!.FindChildById("/menubar/Edit/v0"));
        Assert.Single(log);
    }
}
