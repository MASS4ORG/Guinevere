using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers <c>ButtonGroup</c>: the three modes and their selection, joined layout and position classes, disabled
/// items, the single Tab stop with arrow-key roving, and the sheets that round and join the outer edges.
/// </summary>
public sealed class ButtonGroupTests
{
    static readonly Vector2 Away = new(-50, -50);
    static readonly ButtonGroupItem[] Three = ["Left", "Center", "Right"];
    static readonly ButtonGroupItem[] MiddleDisabled = ["One", new("Two", Enabled: false), "Three"];

    static LayoutNode Group(FrameHarness h) => h.Gui.RootNode!.Children[0];

    static LayoutNode Item(FrameHarness h, int index) => Group(h).Children[index];

    static void Key(FrameHarness h, Action<Gui> draw, KeyboardKey key)
    {
        h.Input.MoveTo(Away);
        h.Input.PressKey(key);
        h.Frame(draw);
        h.Input.ReleaseKey(key);
        h.Frame(draw);
    }

    /// <summary>Clicks an item, then runs the frame that applies the focus the click requested.</summary>
    static void FocusItem(FrameHarness h, Action<Gui> draw, int index)
    {
        h.Click(draw, Item(h, index).Center);
        h.Input.MoveTo(Away);
        h.Frame(draw);
    }

    static bool Focused(FrameHarness h, int index) => h.Gui.Focus.CurrentFocusedId == Item(h, index).Id;

    static IReadOnlyList<string> Classes(LayoutNode node) => node.StyleTarget!.Value.Classes;

    /// <summary>One item is alone, two are first and last, and N have first and last around the middle ones.</summary>
    [Theory]
    [InlineData(1, "only")]
    [InlineData(2, "first last")]
    [InlineData(5, "first middle middle middle last")]
    public void ItemsCarryPositionClasses(int count, string positions)
    {
        foreach (var orientation in new[] { Axis.Horizontal, Axis.Vertical })
        {
            using var h = new FrameHarness();
            ButtonGroupItem[] items = [.. Enumerable.Range(0, count).Select(static i => new ButtonGroupItem($"I{i}"))];
            var selected = 0;
            h.Frame(g => g.ButtonGroup(ref selected, items, orientation));

            var expected = positions.Split(' ');
            Assert.Equal(count, Group(h).Children.Count);
            for (var i = 0; i < count; i++)
            {
                var axis = orientation == Axis.Vertical ? "vertical" : "horizontal";
                Assert.Equal([axis, expected[i]], Classes(Item(h, i)));
            }
        }
    }

    /// <summary>Items touch edge to edge along the axis, share a cross size, and the group is the vertical one by class.</summary>
    [Theory]
    [InlineData(Axis.Horizontal)]
    [InlineData(Axis.Vertical)]
    public void ItemsAreAttachedAlongTheOrientation(Axis orientation)
    {
        using var h = new FrameHarness();
        var selected = 0;
        h.Frame(g => g.ButtonGroup(ref selected, Three, orientation));

        var (a, b, c) = (Item(h, 0), Item(h, 1), Item(h, 2));
        if (orientation == Axis.Horizontal)
        {
            Assert.Equal(a.Rect.X + a.Rect.W, b.Rect.X, 0.5f);
            Assert.Equal(b.Rect.X + b.Rect.W, c.Rect.X, 0.5f);
            Assert.Equal(a.Rect.Y, c.Rect.Y, 0.5f);
            Assert.DoesNotContain("vertical", Classes(Group(h)));
        }
        else
        {
            Assert.Equal(a.Rect.Y + a.Rect.H, b.Rect.Y, 0.5f);
            Assert.Equal(b.Rect.Y + b.Rect.H, c.Rect.Y, 0.5f);
            Assert.Equal(a.Rect.X, c.Rect.X, 0.5f);
            Assert.Equal(a.Rect.W, c.Rect.W, 0.5f);
            Assert.True(a.Rect.W > 0f);
            Assert.Contains("vertical", Classes(Group(h)));
        }
    }

    /// <summary>Items take any content, an explicit id names the item, and the index is the default id.</summary>
    [Fact]
    public void ItemsShowAnyContentAndTakeOptionalIds()
    {
        using var h = new FrameHarness();
        var selected = 0;
        var icon = Icon.FromGlyph("+").Labeled("Add");
        ButtonGroupItem[] items =
        [
            "Text", icon, Content.Row(icon, "Both"), new(Content.Build(() => h.Gui.Node(10, 10)), Id: "custom")
        ];
        h.Frame(g => g.ButtonGroup(ref selected, items));

        Assert.All(Group(h).Children, static item => Assert.NotEmpty(item.Children));
        var groupId = Group(h).Id;
        Assert.Equal($"{groupId}/0", Item(h, 0).Id);
        Assert.Equal($"{groupId}/custom", Item(h, 3).Id);
    }

    /// <summary>Clicking another item selects it and reports once; clicking the selected item reports no change.</summary>
    [Fact]
    public void SingleModeSelectsOnClickAndReportsChangeOnlyWhenItMoves()
    {
        using var h = new FrameHarness();
        var selected = 0;
        bool clicked = false, changed = false;
        void Draw(Gui g)
        {
            var node = g.ButtonGroup(ref selected, Three);
            clicked |= node.Clicked;
            changed |= node.Changed;
        }

        h.Frame(Draw);
        h.Click(Draw, Item(h, 2).Center);
        Assert.Equal((2, true, true), (selected, clicked, changed));

        (clicked, changed) = (false, false);
        h.Click(Draw, Item(h, 2).Center);
        Assert.Equal((2, true, false), (selected, clicked, changed));
    }

    /// <summary>Only the selected item matches <c>:checked</c>, and a negative selection checks none.</summary>
    [Fact]
    public void SingleModeMarksTheSelectedItemChecked()
    {
        using var h = new FrameHarness();
        var selected = 1;
        void Draw(Gui g) => g.ButtonGroup(ref selected, Three);

        h.Frame(Draw);
        Assert.Equal([false, true, false], Group(h).Children.Select(static i => HasChecked(i)));
        selected = -1;
        h.Frame(Draw);
        Assert.All(Group(h).Children, static item => Assert.False(HasChecked(item)));
    }

    static bool HasChecked(LayoutNode node) => node.StyleTarget!.Value.Modifiers?.Contains("checked") == true;

    /// <summary>Clicking toggles an item in and out of the caller's set, which every activation edits.</summary>
    [Fact]
    public void MultipleModeTogglesMembership()
    {
        using var h = new FrameHarness();
        var set = new HashSet<int> { 0 };
        var changes = 0;
        void Draw(Gui g) => changes += g.ButtonGroup(set, Three).Changed ? 1 : 0;

        h.Frame(Draw);
        h.Click(Draw, Item(h, 2).Center);
        Assert.Equal([0, 2], set.Order());
        h.Click(Draw, Item(h, 0).Center);
        Assert.Equal([2], set.Order());
        Assert.Equal(2, changes);
        Assert.False(HasChecked(Item(h, 0)));
        Assert.True(HasChecked(Item(h, 2)));
    }

    /// <summary>Action mode reports the activated index and keeps no selection.</summary>
    [Fact]
    public void ActionModeReportsTheClickedIndex()
    {
        using var h = new FrameHarness();
        var seen = new List<int>();
        void Draw(Gui g)
        {
            if (g.ButtonGroup(Three, out var clicked))
            {
                seen.Add(clicked);
            }
        }

        h.Frame(Draw);
        h.Click(Draw, Item(h, 1).Center);
        h.Click(Draw, Item(h, 0).Center);

        Assert.Equal([1, 0], seen);
        Assert.All(Group(h).Children, static item => Assert.False(HasChecked(item)));
    }

    /// <summary>A disabled item, and every item of a disabled group, ignores clicks.</summary>
    [Fact]
    public void DisabledItemsNeverActivate()
    {
        using var h = new FrameHarness();
        var selected = 0;
        var enabled = true;
        void Draw(Gui g) => g.ButtonGroup(ref selected, MiddleDisabled, enabled: enabled);

        h.Frame(Draw);
        h.Click(Draw, Item(h, 1).Center);
        Assert.Equal(0, selected);

        h.Click(Draw, Item(h, 2).Center);
        Assert.Equal(2, selected);

        enabled = false;
        h.Frame(Draw);
        h.Click(Draw, Item(h, 0).Center);
        Assert.Equal(2, selected);
        Assert.True(Group(h).StyleTarget!.Value.State.HasFlag(StyleState.Disabled));
    }

    /// <summary>The items are one Tab stop: Tab enters the group and the next Tab leaves it, with no stop per item.</summary>
    [Fact]
    public void TabEntersAndLeavesTheGroupAsOneStop()
    {
        using var h = new FrameHarness();
        var selected = 0;
        void Draw(Gui g)
        {
            g.Button("Before", id: "before");
            g.ButtonGroup(ref selected, Three, id: "group");
            g.Button("After", id: "after");
        }

        h.Frame(Draw);
        h.Gui.Focus.RequestFocus("before");
        h.Frame(Draw);

        Key(h, Draw, KeyboardKey.Tab);
        Assert.Equal("group/0", h.Gui.Focus.CurrentFocusedId);
        Key(h, Draw, KeyboardKey.Tab);
        Assert.Equal("after", h.Gui.Focus.CurrentFocusedId);
        Key(h, Draw, KeyboardKey.Tab);
        Assert.Equal("before", h.Gui.Focus.CurrentFocusedId);
    }

    /// <summary>A single-select group is entered at its selected item, however far the last focus got.</summary>
    [Fact]
    public void TabLandsOnTheSelectedItem()
    {
        using var h = new FrameHarness();
        var selected = 2;
        void Draw(Gui g)
        {
            g.Button("Before", id: "before");
            g.ButtonGroup(ref selected, Three, id: "group");
        }

        h.Frame(Draw);
        h.Gui.Focus.RequestFocus("before");
        h.Frame(Draw);

        Key(h, Draw, KeyboardKey.Tab);

        Assert.Equal("group/2", h.Gui.Focus.CurrentFocusedId);
    }

    /// <summary>Arrows move focus between items, skipping disabled ones and wrapping, without changing the selection.</summary>
    [Fact]
    public void ArrowsRoveFocusSkippingDisabledItemsWithoutSelecting()
    {
        using var h = new FrameHarness();
        var selected = 0;
        void Draw(Gui g) => g.ButtonGroup(ref selected, MiddleDisabled);

        h.Frame(Draw);
        FocusItem(h, Draw, 0);
        Assert.True(Focused(h, 0));

        Key(h, Draw, KeyboardKey.Right);
        Assert.True(Focused(h, 2));
        Key(h, Draw, KeyboardKey.Right);
        Assert.True(Focused(h, 0));
        Key(h, Draw, KeyboardKey.Left);
        Assert.True(Focused(h, 2));
        Key(h, Draw, KeyboardKey.Left);
        Assert.True(Focused(h, 0));
        Assert.Equal(0, selected);
    }

    /// <summary>Home and End jump to the first and last enabled items.</summary>
    [Fact]
    public void HomeAndEndJumpToTheEnabledEnds()
    {
        using var h = new FrameHarness();
        var selected = 0;
        ButtonGroupItem[] items = [new("A", Enabled: false), "B", "C", "D", new("E", Enabled: false)];
        void Draw(Gui g) => g.ButtonGroup(ref selected, items);

        h.Frame(Draw);
        FocusItem(h, Draw, 2);

        Key(h, Draw, KeyboardKey.End);
        Assert.True(Focused(h, 3));
        Key(h, Draw, KeyboardKey.Home);
        Assert.True(Focused(h, 1));
    }

    /// <summary>A vertical group answers to Up and Down, and leaves Left and Right to ordinary focus navigation.</summary>
    [Fact]
    public void VerticalGroupsUseUpAndDown()
    {
        using var h = new FrameHarness();
        var selected = 0;
        void Draw(Gui g) => g.ButtonGroup(ref selected, Three, Axis.Vertical);

        h.Frame(Draw);
        FocusItem(h, Draw, 0);

        Key(h, Draw, KeyboardKey.Down);
        Assert.True(Focused(h, 1));
        Key(h, Draw, KeyboardKey.Down);
        Key(h, Draw, KeyboardKey.Up);
        Assert.True(Focused(h, 1));
        Key(h, Draw, KeyboardKey.Right);
        Assert.True(Focused(h, 1));
    }

    /// <summary>Space and Enter select the focused item; arrows alone never do.</summary>
    [Theory]
    [InlineData(KeyboardKey.Space)]
    [InlineData(KeyboardKey.Enter)]
    public void SpaceAndEnterActivateTheFocusedItem(KeyboardKey key)
    {
        using var h = new FrameHarness();
        var selected = 0;
        var changed = 0;
        void Draw(Gui g) => changed += g.ButtonGroup(ref selected, Three).Changed ? 1 : 0;

        h.Frame(Draw);
        FocusItem(h, Draw, 0);
        Key(h, Draw, KeyboardKey.Right);
        Assert.Equal((0, 0), (selected, changed));

        Key(h, Draw, key);

        Assert.Equal((1, 1), (selected, changed));
    }

    /// <summary>A click gives the item focus and moves the group's Tab stop to it.</summary>
    [Fact]
    public void ClickMovesTheTabStop()
    {
        using var h = new FrameHarness();
        var set = new HashSet<int>();
        void Draw(Gui g) => g.ButtonGroup(set, Three);

        h.Frame(Draw);
        FocusItem(h, Draw, 1);
        Assert.True(Focused(h, 1));

        Assert.Equal([1], set.Order());

        Key(h, Draw, KeyboardKey.Space);
        Assert.Empty(set);
        Assert.True(Focused(h, 1));
    }

    /// <summary>Native activation of the focused item selects it like Space does.</summary>
    [Fact]
    public void ControlActivationSelectsTheFocusedItem()
    {
        using var h = new FrameHarness();
        var source = Substitute.For<IControlActivationSource>();
        h.Gui.ControlActivation = source;
        var selected = 0;
        void Draw(Gui g) => g.ButtonGroup(ref selected, Three);

        h.Frame(Draw);
        FocusItem(h, Draw, 1);
        Assert.Equal(1, selected);
        Key(h, Draw, KeyboardKey.Right);
        Assert.True(Focused(h, 2));

        source.IsActivated(Item(h, 2).Id).Returns(true);
        h.Frame(Draw);

        Assert.Equal(2, selected);
    }

    /// <summary>Caller classes follow the group's own, so rules like <c>button-group.compact</c> reach the items.</summary>
    [Fact]
    public void CallerClassesJoinTheGroupClasses()
    {
        using var h = new FrameHarness();
        var selected = 0;
        h.Frame(g => g.ButtonGroup(ref selected, Three, Axis.Vertical, classes: ["compact"]));

        Assert.Equal(["vertical", "compact"], Classes(Group(h)));
    }

    /// <summary>The group needs an item list, and a set for multiple selection.</summary>
    [Fact]
    public void NullArgumentsAreRejected()
    {
        using var h = new FrameHarness();
        h.Frame(g =>
        {
            Assert.Throws<ArgumentNullException>(() => g.ButtonGroup(null!, out _));
            Assert.Throws<ArgumentNullException>(() => g.ButtonGroup(new HashSet<int>(), null!));
            Assert.Throws<ArgumentNullException>(() => g.ButtonGroup(null!, Three));
        });
    }

    /// <summary>Items publish a role per mode with their checked state, for assistive technology.</summary>
    [Fact]
    public void ItemsPublishRolesPerMode()
    {
        using var h = new FrameHarness();
        var controls = new List<ControlSemantics>();
        var sink = Substitute.For<IControlSemanticsSink>();
        sink.When(s => s.Publish(Arg.Any<ControlSemantics>())).Do(call => controls.Add(call.Arg<ControlSemantics>()));
        h.Gui.ControlSemantics = sink;
        var selected = 1;
        var set = new HashSet<int> { 0 };
        h.Frame(g =>
        {
            g.ButtonGroup(Three, out _, id: "action");
            g.ButtonGroup(ref selected, Three, id: "single");
            g.ButtonGroup(set, Three, id: "multiple");
        });

        Assert.Equal(ControlRole.Button, controls.Single(c => c.Id == "action/0").Role);
        Assert.Equal(ControlRole.Radio, controls.Single(c => c.Id == "single/1").Role);
        Assert.True(controls.Single(c => c.Id == "single/1").State.HasFlag(ControlVisualState.Checked));
        Assert.Equal(ControlRole.Checkbox, controls.Single(c => c.Id == "multiple/0").Role);
        Assert.False(controls.Single(c => c.Id == "multiple/2").State.HasFlag(ControlVisualState.Checked));
    }

    /// <summary>
    /// Both base sheets round only the outer corners and drop the border an item shares with the previous one, per
    /// orientation, and keep the whole item rounded when it stands alone.
    /// </summary>
    [Theory]
    [InlineData(false, "8")]
    [InlineData(true, "4")]
    public void SheetsJoinCornersAndBorders(bool classic, string radius)
    {
        using var h = new FrameHarness();
        if (classic)
        {
            ExcaliburStyles.SetBaseSheet(h.Gui, ExcaliburStyles.Classic);
        }

        var selected = 0;
        h.Frame(g => g.ButtonGroup(ref selected, Three));
        string? Style(string orientation, string position, string property) =>
            h.Gui.ResolveStyle("button-group-item", [orientation, position]).Get(property);

        Assert.Equal($"{radius} 0 0 {radius}", Style("horizontal", "first", "border-radius"));
        Assert.Equal($"0 {radius} {radius} 0", Style("horizontal", "last", "border-radius"));
        Assert.Equal($"{radius} {radius} 0 0", Style("vertical", "first", "border-radius"));
        Assert.Equal($"0 0 {radius} {radius}", Style("vertical", "last", "border-radius"));
        Assert.Equal(radius, Style("horizontal", "only", "border-radius"));
        Assert.Null(Style("horizontal", "middle", "border-radius"));
        Assert.Equal("0", Style("horizontal", "middle", "border-left-width"));
        Assert.Equal("0", Style("horizontal", "last", "border-left-width"));
        Assert.Equal("0", Style("vertical", "middle", "border-top-width"));
        Assert.Equal("0", Style("vertical", "last", "border-top-width"));
        Assert.Null(Style("horizontal", "first", "border-left-width"));
        Assert.Null(Style("vertical", "first", "border-top-width"));
    }
}
